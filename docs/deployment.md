# Deploy the workshop site

The workshop's website is the dashboard in this repository, running on Azure App Service. The **Deploy workshop site** workflow, `.github/workflows/deploy-site.yml`, runs every test, publishes the dashboard, signs in to Azure with OpenID Connect (OIDC), deploys, and then checks that the site reports the commit it just built.

You start the workflow by hand. The site doesn't change when `main` changes, so run the workflow again after you merge lesson or app changes.

The job runs only in `sabajamalian/ghaction-workshop`. In copies of the template it's skipped, and Lab 1 tells learners they can ignore the workflow. Learners who want to deploy their own copy follow [Lab 7](../lessons/explore/azure-oidc.md), which uses the same approach with their own app.

The site runs at [ghaction-workshop.azurewebsites.net](https://ghaction-workshop.azurewebsites.net), on a B1 plan in Central US. Its resources are in the resource group `rg-github-workshop`, so `az group delete --name rg-github-workshop` deletes them.

## How the sign-in works

GitHub stores no Azure secret for this workflow. Instead:

- A user-assigned managed identity in Azure has a federated credential that trusts GitHub's OIDC tokens with the issuer `https://token.actions.githubusercontent.com`, the audience `api://AzureADTokenExchange`, and the subject `repo:sabajamalian@46538514/ghaction-workshop@1423905088:environment:production`.
- The job asks GitHub for a token (that's what `id-token: write` allows), and `azure/login` exchanges it for a short-lived Azure token.
- The identity has the **Website Contributor** role on the web app and nothing else, so it can deploy to this one app but can't read or change anything else in the subscription.

The numbers in the subject are the IDs of the `sabajamalian` account and of this repository. GitHub adds them to the subject for repositories created after July 15, 2026, so a repository that later takes the same name can't get a matching token. To print the part of the subject before `:environment`, run:

```bash
gh api repos/sabajamalian/ghaction-workshop/actions/oidc/customization/sub --jq .sub_claim_prefix
```

The identity isn't attached to the web app, and the app's settings hold no secrets, so the public site itself has no credentials that could change GitHub repositories or Azure resources.

GitHub only issues a token with that subject to a job that uses the `production` environment in this repository. Restricting that environment to the `main` branch, as described below, means a workflow on any other branch can't sign in.

## What the setup creates

| Resource | Default name | Purpose |
| --- | --- | --- |
| Resource group | `rg-APPNAME` | Holds everything below, so one command deletes it all. |
| App Service plan | `plan-APPNAME` | The Linux compute the site runs on. B1 by default. |
| Web app | `APPNAME` | The site itself. HTTPS only, with basic authentication off, so it has no FTP password or publish profile that could leak. |
| User-assigned managed identity | `id-APPNAME` | The identity the workflow signs in as. |
| Federated credential | On the identity | Trusts tokens for jobs that use this repository's `production` environment, and nothing else. |
| Role assignment | On the web app | Gives the identity **Website Contributor** on the web app. |

`APPNAME` is the web app's name. It must be unique across Azure, because it becomes part of the site's address.

## Before you start

You need:

- An Azure subscription, and the Azure CLI signed in to it. Run `az login`, then `az account set --subscription SUBSCRIPTION_ID`. Azure Cloud Shell works too, because it has Bash and a signed-in Azure CLI.
- Permission to create resources and assign roles, on the subscription or on an existing resource group:
  - **Owner**, or
  - **Contributor** plus either **User Access Administrator** or **Role Based Access Control Administrator**.

  Contributor covers the plan, the web app, the managed identity, and its federated credential (`Microsoft.ManagedIdentity/userAssignedIdentities/write` and `Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials/write`). The role assignment needs `Microsoft.Authorization/roleAssignments/write`, which Contributor doesn't include. If someone else creates the resource group for you, these roles on that group are enough. Pass its name with `--resource-group`.
- The `Microsoft.Web` and `Microsoft.ManagedIdentity` resource providers registered on the subscription. Check with `az provider show --namespace Microsoft.Web --query registrationState`. If either isn't `Registered`, someone with Contributor on the subscription can run `az provider register --namespace NAMESPACE`.
- Admin access to `sabajamalian/ghaction-workshop`, to create the environment and the variables. Running the workflow needs write access.
- A public repository. On GitHub Free, environments work only in public repositories. Private repositories need GitHub Pro, GitHub Team, or GitHub Enterprise.

## Create the Azure resources

From the repository root, preview the commands first, then run them:

```bash
bash scripts/azure/site-setup.sh --app-name APPNAME --dry-run
bash scripts/azure/site-setup.sh --app-name APPNAME
```

`--dry-run` prints the Azure CLI commands without running them. By default, the script uses a B1 plan, the resource group `rg-APPNAME`, the identity `id-APPNAME`, the repository `sabajamalian/ghaction-workshop`, and the environment `production`. Change them with `--sku`, `--resource-group`, `--location`, `--repo`, and `--environment`. If the script can't read the repository's OIDC subject prefix from GitHub, for example because the repository is private and the GitHub CLI isn't signed in, pass it with `--subject-prefix`. Run `bash scripts/azure/site-setup.sh --help` to see every option and the default location. [scripts/azure/README.md](../scripts/azure/README.md) describes all three setup scripts.

When it finishes, the script prints the values for the four variables, the site's address, the `gh variable set` commands, and the cleanup command. Keep that output until you've finished the next section.

## Configure the repository

1. Create the environment before the first run. If a workflow refers to an environment that doesn't exist, GitHub creates it without any protection rules.
   1. In the repository, select **Settings**, then **Environments**, then **New environment**.
   2. Name it `production` and select **Configure environment**.
   3. In the **Deployment branches** dropdown, select **Selected branches and tags**. Select **Add deployment branch or tag rule**, select **Branch** in the "Ref type" dropdown, type `main` as the name pattern, and select **Add rule**.
   4. Optional: select **Required reviewers**, add yourself or another maintainer, and select **Save protection rules**, so each deployment waits for approval.
2. Add the variables. Select **Settings**, then **Secrets and variables**, then **Actions**. On the **Variables** tab, select **New repository variable** for each of these, using the values the script printed:
   - `AZURE_CLIENT_ID`: the managed identity's client ID.
   - `AZURE_TENANT_ID`: the Microsoft Entra tenant ID.
   - `AZURE_SUBSCRIPTION_ID`: the subscription ID.
   - `AZURE_WEBAPP_NAME`: the web app's name.

   You can run the `gh variable set` commands the script printed instead. These are variables, not secrets: they identify the identity and the app, but they don't grant access on their own. You can also add them to the `production` environment instead of the repository.

## Deploy

1. Select the **Actions** tab, then **Deploy workshop site**.
2. Select **Run workflow**, leave the branch set to `main`, and select the green **Run workflow** button.

The **Test and deploy** job checks that the four variables are set, runs all the tests, publishes the dashboard, signs in to Azure, and deploys. Then it calls the site's `/health` page every 15 seconds, up to 10 times, until the page reports the commit the run built. The run's summary shows the site's address and the commit, and the `production` environment's deployment history links to the site.

## Check the site

- `/health` returns JSON. `status` is `healthy`, `commit` is the full SHA of the commit you deployed, `lessons` is the number of lessons, and `environment` is `Production`.
- The footer on every page shows the version, the short commit linked to it on GitHub, the build time, and the environment.
- If the site's address changes, update the link near the top of `README.md`.

## Costs

- **B1**, the default: a Linux Basic plan. In East US it's $0.017 an hour, about $12.41 for a 730-hour month (Azure retail price, checked in October 2026). Prices vary by region, so check [App Service pricing](https://azure.microsoft.com/pricing/details/app-service/linux/) for yours. The plan costs the same whether or not anyone visits the site.
- **F1**: free, with a daily CPU quota, and no Always On, so the app unloads when it's idle and the next visit is slow. It's fine for a trial, not for a workshop with many learners at once. Use `--sku F1`.

The managed identity, its federated credential, and the role assignment cost nothing. Standard GitHub-hosted runners are free in public repositories, so the workflow doesn't use Actions minutes.

## Delete the site

```bash
az group delete --name rg-APPNAME
```

That deletes the plan, the web app, the managed identity, its federated credential, and the role assignment. Then remove the variables, either on the **Variables** tab or with `gh variable delete NAME --repo sabajamalian/ghaction-workshop`, delete the `production` environment if you won't deploy again, and remove the site link from `README.md`.

## If something fails

If the job is skipped, the run isn't in `sabajamalian/ghaction-workshop`. If the repository was renamed or transferred, update the `if:` condition in `deploy-site.yml` and the federated credential's subject.

If the first step reports missing variables, add the ones it lists, as described in [Configure the repository](#configure-the-repository).

If the run waits, or says the branch isn't allowed, the `production` environment's rules apply: a required reviewer has to approve the deployment, or the run started from a branch other than `main`.

If **Sign in to Azure** fails with `AADSTS700213: No matching federated identity record found for presented assertion subject`, the token's subject doesn't match the federated credential. The step's log shows the subject GitHub sent, on the line that starts with `subject claim -`. Compare it with the credential's subject, which `az identity federated-credential list --identity-name id-APPNAME --resource-group rg-APPNAME --query "[].subject"` lists. They must match exactly: `repo:sabajamalian@46538514/ghaction-workshop@1423905088:environment:production`.

If the error is `AADSTS70021: No matching federated identity record found for presented assertion.` and you created the credential in the last few minutes, wait a few minutes and run the workflow again, because [a new federated credential takes time to propagate](https://learn.microsoft.com/entra/workload-id/workload-identity-federation-considerations#time-for-federated-credential-changes-to-propagate).

If **Deploy to Azure App Service** fails with an authorization error, check the role assignment with `az role assignment list --assignee CLIENT_ID --all --output table`. A new role assignment can take a few minutes to take effect, so rerunning the workflow may be enough.

If **Check that the site runs this commit** never sees the new commit, the app may still be restarting, or failing to start. Check its log stream in the Azure portal, or run `az webapp log tail --name APPNAME --resource-group rg-APPNAME`. If `/health` returns status 503 with `unhealthy`, the deployed app found no lessons.
