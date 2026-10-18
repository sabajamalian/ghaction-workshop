---
title: "Lab 7: Deploy to Azure with OIDC"
summary: "Deploy the workshop website to Azure App Service from a workflow that signs in to Azure with OpenID Connect instead of a stored secret."
duration: 45
difficulty: Intermediate
order: 7
requires: "An Azure subscription where you can create resources and assign roles, or Azure values from your instructor."
---

# Lab 7: Deploy to Azure with OIDC

In this lab, a workflow builds the workshop website and deploys it to your own web app in Azure App Service. To deploy, the workflow has to sign in to Azure, and it does that without a password or key stored anywhere. GitHub gives the job a short-lived OpenID Connect (OIDC) token that names your repository and branch, and Azure accepts the token because you've told it to trust tokens from your repository's `main` branch. When you're done, your site's footer shows the commit it was built from.

## Before you start

You need the repository you created in [Lab 0](../foundation/00-create-repository.md). You'll add a workflow file the way you did in [Lab 2](../foundation/02-test-pull-request.md) and start it the way you did in [Lab 1](../foundation/01-build-application.md).

You also need one of these:

- An Azure subscription where your account has the Owner role, or Contributor plus User Access Administrator or Role Based Access Control Administrator. The setup gives a new identity a role, and Contributor alone can't assign roles. [Permissions](../../scripts/azure/README.md#permissions) lists what each part needs. You'll run the setup in Azure Cloud Shell, which runs in your browser, or in a terminal with Bash, Git, and the Azure CLI 2.58.0 or later.
- Four values from your instructor, if they set up Azure for your class. Then you don't need a subscription, and you'll follow [Get values from your instructor](#get-values-from-your-instructor) instead of the setup.

The setup uses App Service's Free (F1) tier, and managed identities have no cost, so the Azure side is free. The workflow only runs when you start it. If your repository is public, the runs are free. If it's private, each run uses a few of the 2,000 Actions minutes a month that GitHub Free includes, because the workflow has two jobs and GitHub [rounds each job up to a whole minute](https://docs.github.com/en/billing/reference/actions-runner-pricing). [GitHub Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions) lists the minutes other plans include.

## How the workflow signs in to Azure

Azure App Service runs web apps without you managing the servers under them. Each web app belongs to an App Service plan, which sets its pricing tier and how much CPU and memory it gets. See the [App Service overview](https://learn.microsoft.com/azure/app-service/overview).

To deploy, the workflow signs in to Microsoft Entra ID, the identity service Azure uses, as a user-assigned managed identity. That's an identity for software rather than a person, and you create it in your subscription like any other resource. It doesn't have a password or key that anyone could copy. Instead, it has a federated credential, which tells Entra ID to trust tokens from another identity provider, here GitHub Actions. The identity isn't attached to your web app. It exists so the workflow has something to sign in as. See [managed identities](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/overview) and [workload identity federation](https://learn.microsoft.com/entra/workload-id/workload-identity-federation).

When the `deploy` job runs:

1. **Sign in to Azure** asks GitHub for an OIDC token with the audience `api://AzureADTokenExchange`, the value Entra ID expects. A job can only ask for a token if the workflow gives it the `id-token: write` permission. That permission lets the job fetch the token and nothing else. It doesn't let the job write to your repository. See [OIDC workflow permissions](https://docs.github.com/en/actions/reference/security/oidc#workflow-permissions-for-the-requesting-the-oidc-token).
2. GitHub returns a signed token. Its issuer is `https://token.actions.githubusercontent.com`, and its subject names your repository and the branch the run uses.
3. The step sends the token to Entra ID with the identity's client ID. Entra ID checks GitHub's signature, then compares the token's issuer, audience, and subject with the identity's federated credentials. When one matches exactly, it returns a short-lived Azure access token for the identity.
4. **Deploy to App Service** uses that access token to upload the app. Azure role-based access control (Azure RBAC) allows it, because the identity has the Website Contributor role on your web app.

Steps 1 to 3 are authentication: Azure finds out who the job is. Step 4 is authorization: Azure decides what that identity is allowed to do. [Website Contributor](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/web-and-mobile#website-contributor) lets an identity manage web apps, including deploying to them, but not their App Service plans, and not who has access to them. Your identity has it on one web app only, so a job that signs in as it can't change anything else in your subscription. The two checks fail in different steps. A subject that doesn't match fails **Sign in to Azure**, and a role on the wrong web app fails **Deploy to App Service**. [OpenID Connect](https://docs.github.com/en/actions/concepts/security/openid-connect) in the GitHub docs and [What is Azure RBAC](https://learn.microsoft.com/azure/role-based-access-control/overview) go into more depth.

### The subject names your repository

Here's the subject of a token for a job that runs from `main` without an environment:

```text
repo:mona@123456/ghaction-workshop@456789:ref:refs/heads/main
```

`mona` is the account that owns the repository, and `123456` is that account's numeric ID. `ghaction-workshop` is the repository, and `456789` is its ID. `ref:refs/heads/main` says the run used the `main` branch. GitHub includes the IDs for repositories created after July 15, 2026, so if a name is freed up and someone else takes it, their tokens don't match yours. Older repositories use `repo:OWNER/REPO` without IDs, unless someone opted them in. Guides that show subjects without IDs, including the `azure/login` README, were written for those older repositories. See [Immutable subject claims](https://docs.github.com/en/actions/reference/security/oidc#immutable-subject-claims).

Every copy of the workshop is a new repository with its own ID. A federated credential that your instructor or another learner set up for their copy doesn't match your tokens, and knowing their identity's client ID doesn't help, because Azure checks the subject of the token your job presents. That's why each copy needs its own trust relationship in Azure. The branch is part of the subject too, so a run from any other branch can't sign in.

If a job uses a deployment environment, the subject names the environment instead of the branch, as in `repo:mona@123456/ghaction-workshop@456789:environment:staging`. An environment is a deployment target, such as `staging` or `production`, with its own rules and values. [Lab 8](environments.md) sends this workflow's `deploy` job through a `staging` environment and adds a federated credential for that subject.

### Find your subject prefix

Everything before `:ref` is your repository's subject prefix. The setup script looks it up for you. For the portal steps, or if your instructor asks for it, get it in one of these ways:

- In a terminal where the GitHub CLI is signed in, run this command with your own `OWNER/REPO`. Azure Cloud Shell has the GitHub CLI, and `gh auth login` signs it in.

  ```bash
  gh api repos/OWNER/REPO/actions/oidc/customization/sub --jq .sub_claim_prefix
  ```

- If your repository is public, open `https://api.github.com/repos/OWNER/REPO/actions/oidc/customization/sub` in your browser, with your own `OWNER/REPO`, and copy the `sub_claim_prefix` value.

After your first run, the log of the **Sign in to Azure** step shows the whole subject too, on the line that starts with `subject claim -`.

If someone customized the subject format for your repository, the API's response shows `"use_default": false`, and the setup script stops with a message about a custom OIDC subject template. The subjects in this lab don't apply then. [Custom subject claims](../../scripts/azure/README.md#custom-subject-claims) shows how to set up the credential anyway.

## Set up Azure

### Get values from your instructor

If your instructor set up Azure for your class, they need your repository's `OWNER/REPO`, plus its [subject prefix](#find-your-subject-prefix) if your repository is private. They'll send you four values, `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, and `AZURE_WEBAPP_NAME`, and your site's address. The identity they created for you already trusts your `main` branch, and your `staging` environment for Lab 8. Continue at [Add the repository variables](#add-the-repository-variables).

### Choose a name

Your web app's name is part of its address, `https://APPNAME.azurewebsites.net`, so it has to be unique across Azure. Use 2 to 55 lowercase letters, digits, and hyphens, starting and ending with a letter or digit, such as `gha-lab7-` followed by your GitHub username in lowercase. The setup names everything else after it:

| Resource | Name |
| --- | --- |
| Resource group, which holds everything else | `rg-APPNAME` |
| App Service plan, Linux, Free (F1) | `plan-APPNAME` |
| Web app, .NET 10 | `APPNAME` |
| User-assigned managed identity | `id-APPNAME` |
| Federated credential on the identity | `github-branch-main` |

In the rest of this lab, replace `APPNAME` with your name.

The script creates everything in East US unless you add `--location` with another region, such as `--location westus2`. A subscription can have at most 10 Free plans in a region. If you've reached that, use another region, or add `--sku B1` for a Basic plan, which Azure bills by the hour until you delete it. [Costs](../../scripts/azure/README.md#costs) has the prices and the Free tier's limits.

### Run the setup script

1. Open [Azure Cloud Shell](https://shell.azure.com), or sign in to the [Azure portal](https://portal.azure.com) and select **Cloud Shell** in the top navigation. The first time, choose **Bash**. In the **Getting started** pane, select **No storage account required**, choose your subscription, and select **Apply**.

   You can use your own terminal instead if it has Bash, Git, and the Azure CLI 2.58.0 or later, and you've signed in with `az login`.

2. Clone your repository and change to its folder, with your own `OWNER/REPO`:

   ```bash
   git clone https://github.com/OWNER/REPO.git
   cd REPO
   ```

   If your repository is private, run `gh auth login` first and follow its prompts, then clone it with `gh repo clone OWNER/REPO` instead.

3. Preview the setup:

   ```bash
   bash scripts/azure/learner-setup.sh --app-name APPNAME --repo OWNER/REPO --dry-run
   ```

   The script checks that the Azure CLI is signed in, shows which subscription it'll use, looks up your subject prefix, and checks that your app name is free. Then it prints each command it would run, with your values filled in. Nothing changes yet. To use another subscription, run `az account set --subscription NAME-OR-ID`, then preview again.

4. Run the same command without `--dry-run`:

   ```bash
   bash scripts/azure/learner-setup.sh --app-name APPNAME --repo OWNER/REPO
   ```

   It takes a few minutes. When it finishes, it prints the four repository variables with their values, your site's address, and the subject that `id-APPNAME` trusts. Keep the output open for the next section.

If the script stops with an error, fix what it describes and run the same command again. It reuses what it already created.

### Set up Azure in the portal instead

The script runs steps you can also do in the [Azure portal](https://portal.azure.com). You need your [subject prefix](#find-your-subject-prefix) first.

1. Create the web app. Search for **App Services**, select it under **Services**, then select **Create** > **Web App**. On the **Basics** tab:
   - Under **Resource group**, select **Create new**, and enter `rg-APPNAME`.
   - In **Name**, enter `APPNAME`.
   - For **Publish**, select **Code**. For **Runtime stack**, select .NET 10. For **Operating System**, select **Linux**. For **Region**, pick one near you.
   - Under **App Service Plan**, select **Create new**, and enter `plan-APPNAME`. For **Pricing plan**, select **Free F1**.

   Leave the other tabs as they are, and select **Review + create**, then **Create**. The portal gives the app an address in the form `APPNAME-HASH.REGION.azurewebsites.net`. The workflow reads the address from Azure, so that works too.

2. Create the identity. Search for **Managed Identities**, and select it under **Services**. Create a new identity, and in the **Create User Assigned Managed Identity** pane, choose your subscription, the resource group `rg-APPNAME`, the web app's region, and the name `id-APPNAME`. Select **Review + create**, then **Create**.

3. Add the federated credential. Open `id-APPNAME`. In the left menu, under **Settings**, select **Federated credentials**, then **Add Credential**. In **Federated credential scenario**, choose **Other issuer**, so you can paste the whole subject, IDs included. Then fill in:
   - **Issuer**: `https://token.actions.githubusercontent.com`
   - **Subject identifier**: your subject prefix followed by `:ref:refs/heads/main`
   - **Name**: `github-branch-main`. You can't change the name later.

   If the form has an audience field, keep its default, `api://AzureADTokenExchange`. Select **Add**.

4. Give the identity a role on the web app. Open the web app `APPNAME`, and select **Access control (IAM)**, then **Add** > **Add role assignment**. On the **Role** tab, under **Job function roles**, select **Website Contributor**, then **Next**. On the **Members** tab, select **Managed identity**, then **Select members**. In the **Select managed identities** pane, choose the user-assigned managed identity type, select `id-APPNAME`, and select **Select**. Then select **Review + assign** twice, once to review and once to assign.

5. Get the values. In Cloud Shell, run these commands. They print the identity's client ID, your tenant ID, and your subscription ID, in that order. If you have more than one subscription, check that the last one is the subscription you used.

   ```bash
   az identity show --name id-APPNAME --resource-group rg-APPNAME --query clientId --output tsv
   az account show --query tenantId --output tsv
   az account show --query id --output tsv
   ```

## Add the repository variables

The workflow reads four repository variables. Their values come from the setup script, the portal steps, or your instructor:

| Name | Value |
| --- | --- |
| `AZURE_CLIENT_ID` | The client ID of your managed identity |
| `AZURE_TENANT_ID` | The ID of your Microsoft Entra tenant |
| `AZURE_SUBSCRIPTION_ID` | The ID of your Azure subscription |
| `AZURE_WEBAPP_NAME` | Your web app's name |

1. In your repository on GitHub, select the **Settings** tab. In the left sidebar, in the "Security" section, select **Secrets and variables**, then **Actions**.
2. Select the **Variables** tab, then **New repository variable**.
3. In **Name**, type `AZURE_CLIENT_ID`. In **Value**, paste its value.
4. Select **Add variable**.
5. Repeat steps 2 to 4 for `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, and `AZURE_WEBAPP_NAME`.

If the GitHub CLI is signed in where you ran the setup script, you can run the `gh variable set` commands it printed instead.

None of the four values is a credential. They say which identity to sign in as and where to deploy, but only a token with your repository's subject can sign in as that identity. GitHub shows variable values in logs as they are, though, so the three IDs appear in the log of **Sign in to Azure**, which anyone can read in a public repository. [Microsoft's guide to deploying with GitHub Actions](https://learn.microsoft.com/azure/app-service/deploy-github-actions) stores the IDs as secrets, which GitHub hides in logs. If you'd rather do that, add `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` on the **Secrets** tab instead, and in the workflow, change `vars.` to `secrets.` everywhere it reads those three. Keep `AZURE_WEBAPP_NAME` as a variable, because the name is part of your site's address anyway. See [Variables](https://docs.github.com/en/actions/concepts/workflows-and-actions/variables) in the GitHub docs.

## Add the workflow

1. Select the **Code** tab, and make sure the branch menu shows `main`.
2. Select **Add file**, then **Create new file**.
3. In the file name box, type `.github/workflows/deploy-azure.yml`.
4. Copy this workflow into the editor. Keep the spaces exactly as they are.

```yaml source=solutions/azure-deployment/deploy-azure.yml
name: Deploy to Azure

# Lab 7: tests and publishes the dashboard, signs in to Azure with OpenID Connect,
# and deploys to your App Service web app. The vars.AZURE_* values are repository
# variables. See lessons/explore/azure-oidc.md.

on:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  build:
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - name: Check out the repository
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1

      - name: Set up .NET
        uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json

      - name: Run the tests
        run: dotnet test tests/WorkshopCore.Tests

      - name: Restore dependencies
        run: dotnet restore src/WorkshopDashboard/WorkshopDashboard.csproj

      - name: Publish the dashboard
        run: dotnet publish src/WorkshopDashboard/WorkshopDashboard.csproj --configuration Release --no-restore --output publish

      - name: Upload the published app
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: workshop-dashboard
          path: publish
          if-no-files-found: error
          retention-days: 7

  deploy:
    needs: build
    runs-on: ubuntu-latest
    timeout-minutes: 15
    permissions:
      contents: read
      # Lets the job ask GitHub for an OIDC token, which azure/login trades for an Azure token.
      id-token: write
    steps:
      - name: Check the Azure variables
        env:
          AZURE_CLIENT_ID: ${{ vars.AZURE_CLIENT_ID }}
          AZURE_TENANT_ID: ${{ vars.AZURE_TENANT_ID }}
          AZURE_SUBSCRIPTION_ID: ${{ vars.AZURE_SUBSCRIPTION_ID }}
          AZURE_WEBAPP_NAME: ${{ vars.AZURE_WEBAPP_NAME }}
        run: |
          missing=""
          [ -n "$AZURE_CLIENT_ID" ] || missing="$missing AZURE_CLIENT_ID"
          [ -n "$AZURE_TENANT_ID" ] || missing="$missing AZURE_TENANT_ID"
          [ -n "$AZURE_SUBSCRIPTION_ID" ] || missing="$missing AZURE_SUBSCRIPTION_ID"
          [ -n "$AZURE_WEBAPP_NAME" ] || missing="$missing AZURE_WEBAPP_NAME"
          if [ -n "$missing" ]; then
            echo "::error::Missing repository variables:$missing. Add them as Lab 7 describes, then run the workflow again."
            exit 1
          fi
          echo "All four Azure variables are set."

      - name: Download the published app
        uses: actions/download-artifact@9000827ccba6bdab643e8b6fd33ac0654aef8333 # v8.0.2
        with:
          name: workshop-dashboard
          path: publish

      - name: Sign in to Azure
        uses: azure/login@a641126d1b8aa4d1fa005f4f92df94a3a4c4c906 # v3.1.0
        with:
          client-id: ${{ vars.AZURE_CLIENT_ID }}
          tenant-id: ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}

      - name: Deploy to App Service
        id: deploy-to-webapp
        uses: azure/webapps-deploy@02a81bead70021f5284939794bcec79c271ab383 # v3.0.8
        with:
          app-name: ${{ vars.AZURE_WEBAPP_NAME }}
          package: publish

      - name: Check the deployed site
        env:
          WEBAPP_URL: ${{ steps.deploy-to-webapp.outputs.webapp-url }}
          EXPECTED_COMMIT: ${{ github.sha }}
        run: |
          # The action reports the address from the app's publishing profile, which can start with http://.
          site_url="${WEBAPP_URL%/}"
          site_url="https://${site_url#*://}"
          echo "### Deploy to Azure" >> "$GITHUB_STEP_SUMMARY"
          echo "- Site: $site_url" >> "$GITHUB_STEP_SUMMARY"

          # The app restarts after a deployment, and a Free (F1) app can take a minute or two to start.
          for attempt in 1 2 3 4 5 6 7 8 9 10 11 12; do
            rm -f health.json
            code=$(curl --silent --location --max-time 30 --output health.json --write-out '%{http_code}' "$site_url/health" || true)
            commit=$(jq -r '.commit // empty' health.json 2>/dev/null || true)
            if [ "$code" = "200" ] && [ "$commit" = "$EXPECTED_COMMIT" ]; then
              echo "$site_url is running commit $commit."
              echo "- The site is running commit \`$commit\`." >> "$GITHUB_STEP_SUMMARY"
              exit 0
            fi
            echo "Attempt $attempt of 12: /health returned HTTP $code and commit '${commit:-none}'."
            if [ "$attempt" -lt 12 ]; then
              sleep 15
            fi
          done

          echo "::error::$site_url/health didn't report commit $EXPECTED_COMMIT after 12 attempts."
          echo "- The site didn't report commit \`$EXPECTED_COMMIT\` in time." >> "$GITHUB_STEP_SUMMARY"
          exit 1
```

5. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `main` branch, then select **Commit changes**.

### What's in the workflow

- `on: workflow_dispatch` means the workflow only runs when you start it. A push to `main` doesn't deploy anything.
- The `build` job runs the tests from Lab 2, then publishes the app and saves the `publish` folder as an artifact named `workshop-dashboard`, like the build in Lab 1. If a test fails, the job stops, and nothing is deployed.
- `needs: build` makes `deploy` wait for `build`, and skips it if `build` fails. The jobs run on different runners, so **Download the published app** gets the artifact that `build` saved. [Lab 5](jobs-and-artifacts.md) covers passing files between jobs.
- `permissions` on the `deploy` job replaces the workflow's `permissions` for that job. It keeps `contents: read` and adds `id-token: write`, so only `deploy` can ask for an OIDC token, and the `build` job, which runs your repository's tests, can't.
- **Check the Azure variables** stops the job with a list of the variables that are missing. A variable that doesn't exist reads as empty text, and without this step, the job would fail later with an error about the action's inputs instead of your variables.
- **Sign in to Azure** runs `azure/login` with the three IDs. There's no client secret, so it signs in with OIDC.
- **Deploy to App Service** runs `azure/webapps-deploy`, which zips the `publish` folder and uploads it to the web app that `AZURE_WEBAPP_NAME` names. Its `id`, `deploy-to-webapp`, lets later steps read its outputs, such as `webapp-url`, the site's address.
- **Check the deployed site** asks the site's `/health` address which commit it's running, up to 12 times, 15 seconds apart, because the app restarts after a deployment and a Free app can take a minute or two to start. The step writes the site's address to the run's summary, and fails if the site doesn't report this run's commit, `github.sha`, in time.

The `deploy` job has no `environment`, so when you run the workflow from `main`, the subject of its token ends with `:ref:refs/heads/main`.

## Run the deployment

1. Select the **Actions** tab, then **Deploy to Azure** in the left sidebar. **Deploy workshop site** is a different workflow, which only deploys from the original workshop repository.
2. Select **Run workflow**. In the menu that opens, leave the branch set to `main`, then select the green **Run workflow** button.
3. Select the new run. The `build` job takes under a minute, then `deploy` starts.
4. Select `deploy`, and expand **Sign in to Azure**. Partway down, the log shows details from the token that GitHub issued:

   ```text
   Federated token details:
    issuer - https://token.actions.githubusercontent.com
    subject claim - repo:mona@123456/ghaction-workshop@456789:ref:refs/heads/main
    audience - api://AzureADTokenExchange
    job_workflow_ref - mona/ghaction-workshop/.github/workflows/deploy-azure.yml@refs/heads/main
   ```

   Your subject has your own names and IDs. A few lines later, `Azure CLI login succeeds by using OIDC.` means Azure accepted the token.

5. When the run finishes, select **Summary** in the left sidebar. The "Deploy to Azure" section lists your site's address and the commit it's running.

## What happened

Nothing in this lab stored a password, key, or publish profile. The `deploy` job got a short-lived token from GitHub that named your repository and branch, and Entra ID exchanged it for an Azure access token, because the subject matched the identity's federated credential. At the end of the job, `azure/login` signed the Azure CLI out with `az account clear`, and GitHub then removed the runner's virtual machine. If someone copied your four variables into their own repository, their jobs' tokens would name their repository, and Azure would refuse them.

The footer's details come from the `build` job. When the app is built in GitHub Actions, `src/WorkshopDashboard/WorkshopDashboard.csproj` stores the build time, the commit, and the repository's address in the app, using variables that GitHub sets in every job:

```xml excerpt=src/WorkshopDashboard/WorkshopDashboard.csproj
<ItemGroup Condition="'$(GITHUB_ACTIONS)' == 'true'">
  <AssemblyMetadata Include="BuildTimestamp" Value="$([System.DateTime]::UtcNow.ToString('o'))" />
  <AssemblyMetadata Include="CommitSha" Value="$(GITHUB_SHA)" />
  <AssemblyMetadata Include="RepositoryUrl" Value="$(GITHUB_SERVER_URL)/$(GITHUB_REPOSITORY)" />
</ItemGroup>
```

`/health` reports the same commit, which is how **Check the deployed site** knew that your new version was running.

Right now, anyone who can start the workflow on `main` can deploy. [Lab 8](environments.md) sends the `deploy` job through a `staging` environment, which can require an approval first.

## Check your result

Open your site from the run's summary. At the bottom of each page, the footer shows:

- **Version**: `1.0.0`.
- **Commit**: the first seven characters of the commit the run deployed, linked to that commit in your repository. They match the start of the commit in the summary's "Deploy to Azure" section.
- **Built**: when the `build` job ran, in UTC.
- **Environment**: `Production`.

Add `/health` to the site's address to see the same details as JSON, with `"status": "healthy"` and the full commit SHA.

## If something fails

If the setup script stops with `Azure says the name APPNAME can't be used`, someone already has a web app with that name somewhere in Azure. Run the script again with another `--app-name`.

If the setup script stops with `Azure didn't let you assign the role`, your account can create resources but can't assign roles. Ask someone with Owner, User Access Administrator, or Role Based Access Control Administrator on the subscription to run the `az role assignment create` command the script printed, or to give you one of those roles. Then run the script again. It reuses what it already created.

If the run fails with an error saying an action isn't allowed, your copy belongs to an organization that limits which actions can run. Ask an organization owner to allow `azure/login` and `azure/webapps-deploy`, as [Actions the labs use](../../docs/instructor-guide.md#actions-the-labs-use) describes, or create a copy under your personal account.

If **Check the Azure variables** fails with `Missing repository variables:` and a list of names, those variables don't exist or are empty. Check their names on the **Variables** tab. A secret with the same name doesn't count, because the workflow reads `vars.`, not `secrets.`.

If **Sign in to Azure** fails with `Failed to fetch federated token from GitHub. Please make sure to give write permissions to id-token in the workflow.`, the `deploy` job doesn't have `id-token: write`. Compare its `permissions` with the workflow above.

If **Sign in to Azure** fails with `Unable to get authority configuration` and `AADSTS90002: Tenant`, followed by an ID and `not found`, `AZURE_TENANT_ID` doesn't hold your tenant's ID. Run `az account show --query tenantId --output tsv` and update the variable. The error ends by suggesting you check `auth-type`, but the workflow doesn't set it, so you can ignore that part.

If **Sign in to Azure** fails with `AADSTS700213: No matching federated identity record found for presented assertion subject`, the token's subject doesn't match any federated credential on the identity that `AZURE_CLIENT_ID` names. The error shows the subject GitHub sent, and so does the `subject claim -` line above it. Compare it with the subjects the identity trusts:

```bash
az identity federated-credential list --identity-name id-APPNAME --resource-group rg-APPNAME --query "[].subject" --output tsv
```

The usual causes are a subject without the numeric IDs, a run from a branch other than `main`, and a client ID that belongs to another identity. If the error is `AADSTS7002138` instead, the subjects differ only in uppercase and lowercase letters, which Azure treats as different. To fix the credential, run the setup script again, which updates its subject, or edit it in the portal.

If the error is `AADSTS70021: No matching federated identity record found for presented assertion.` and you created the credential in the last few minutes, wait a few minutes and run the workflow again, because [a new federated credential takes time to propagate](https://learn.microsoft.com/entra/workload-id/workload-identity-federation-considerations#time-for-federated-credential-changes-to-propagate).

If **Sign in to Azure** fails with `No subscriptions found for` and a client ID, Azure accepted the token, but the identity has no role in the subscription yet. Check its role assignments:

```bash
az role assignment list --assignee "$(az identity show --name id-APPNAME --resource-group rg-APPNAME --query principalId --output tsv)" --all --query "[].[roleDefinitionName, scope]" --output tsv
```

You should see `Website Contributor` with a scope that ends in `/sites/APPNAME`. If it's there, wait up to 10 minutes, because [a new role assignment can take that long to apply](https://learn.microsoft.com/azure/role-based-access-control/troubleshooting#symptom---role-assignment-changes-are-not-being-detected), then run the workflow again. If it's missing, run the setup script again, or add the role in the portal.

If **Sign in to Azure** fails with `The subscription of` and `doesn't exist in cloud 'AzureCloud'`, `AZURE_SUBSCRIPTION_ID` doesn't match the subscription your web app is in. Run `az account list --query "[].[name, id]" --output tsv` to list your subscriptions' names and IDs, and set the variable to the ID of the one that holds your web app.

If **Deploy to App Service** fails with `Resource APPNAME of type Microsoft.Web/Sites doesn't exist.`, the identity signed in but can't see a web app with that name. Check that `AZURE_WEBAPP_NAME` is your web app's name, and that the identity's Website Contributor role is on that web app.

If **Check the deployed site** fails with `didn't report commit`, read the attempts above the error. HTTP 403 means App Service stopped your Free app because it went over a quota, such as 60 CPU minutes a day, and the app stays stopped until the quota resets. See [App Service quotas](https://learn.microsoft.com/azure/app-service/web-sites-monitor). If the attempts show an older commit, or HTTP 000, which means the site didn't respond, the new version hadn't started yet. Open your site's `/health` address after a minute or two. If it shows the run's commit, the deployment worked, and only the check ran out of time.

If your instructor set up Azure for you and **Sign in to Azure** or **Deploy to App Service** fails, compare your four variables with the values they sent. If those match, send your instructor the error, because only they can see the Azure side.

## Clean up

If you plan to do the Azure section of [Lab 8](environments.md), do it before you clean up, because it uses the same web app and identity.

1. In Cloud Shell, or a terminal where you've run `az login`, delete the resource group. That deletes the web app, the plan, the identity, and its federated credentials. Type `y` when it asks you to confirm.

   ```bash
   az group delete --name rg-APPNAME
   ```

   If you created the web app in the portal and it turned on Application Insights, the portal may also have created a Log Analytics workspace named `DefaultWorkspace-` followed by your subscription ID and a region code, in a resource group named `DefaultResourceGroup-` followed by the same code. Deleting `rg-APPNAME` doesn't delete it. If nothing else uses that workspace, delete it too.

2. In your repository, select the **Settings** tab, then **Secrets and variables** and **Actions** in the left sidebar. On the **Variables** tab, delete `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, and `AZURE_WEBAPP_NAME`. If you stored the IDs as secrets instead, delete them on the **Secrets** tab.

If your instructor set up Azure for you, only do step 2. Your instructor deletes the Azure resources after the workshop.

If you used `--sku B1`, Azure bills the plan by the hour until you delete the resource group, so don't wait. `.github/workflows/deploy-azure.yml` can stay, because it only runs when you start it. Without the variables, a run stops at **Check the Azure variables**.

## Next step

The optional labs can be done in any order, so pick your next one from [the lesson list](../../README.md). [Lab 8: Add a staging environment](environments.md) continues from this one: it sends your Azure deployment through a `staging` environment that can require an approval.
