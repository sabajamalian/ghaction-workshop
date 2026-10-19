---
title: "Lab 8: Add a staging environment"
summary: "Deploy a practice workflow through a staging environment with its own variables, secrets, branch rules, and approvals."
duration: 40
difficulty: Intermediate
order: 8
requires: "A public copy or GitHub Enterprise Cloud for every step, and Lab 7 for the last section."
---

# Lab 8: Add a staging environment

An environment is a named place your workflows deploy to, such as `staging` or `production`. It can have its own variables and secrets, limit which branches can deploy to it, and hold a deployment until someone approves it. You'll add a practice workflow that deploys to a `staging` environment without sending anything outside GitHub, then give `staging` its own settings, limit it to `main`, and require an approval. If you finished Lab 7, you can then send your Azure deployment through the same environment.

## Before you start

Finish the foundation labs, [Lab 0](../foundation/00-create-repository.md) to [Lab 2](../foundation/02-test-pull-request.md), first. They cover the workflow basics this lab builds on.

This lab works in the repository you created in Lab 0. To configure an environment, you must be the repository's owner, or have admin access if the repository belongs to an organization. You'll start runs the way you did in [Lab 1](../foundation/01-build-application.md). The last section also needs the Azure deployment from [Lab 7](azure-oidc.md).

What you can configure depends on your copy's visibility and your GitHub plan:

| Your repository | Environment variables, secrets, and branch rules | Required reviewers and wait timer |
| --- | --- | --- |
| Public, on any plan | Available | Available |
| Private, on GitHub Pro or Team | Available | Not available |
| Private, on GitHub Enterprise Cloud | Available | Available |
| Private, on GitHub Free | Not available | Not available |

[Deployments and environments](https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments) in the GitHub docs lists the plans for each feature.

If your copy is private on GitHub Free, the practice workflow still runs and GitHub still records its deployments, but GitHub's documentation says you can't configure the environment. Do the first two sections, then continue at [Add a manual gate without protection rules](#add-a-manual-gate-without-protection-rules). To do every step instead, change your copy to public in its **Settings**. [Public or private](../foundation/00-create-repository.md#public-or-private) in Lab 0 explains what that changes.

Runs in a public repository are free. In a private one, each run of the practice workflow uses up to 2 of your plan's included minutes, because GitHub rounds each job up to a whole minute.

## Add the practice workflow

1. Select the **Code** tab of your repository, and make sure the branch menu shows `main`.
2. Select **Add file**, then **Create new file**.
3. In the file name box, type `.github/workflows/practice-deploy.yml`. Each `/` you type starts a new folder.
4. Copy this workflow into the editor. YAML uses indentation to show structure, so keep the spaces exactly as they are.

```yaml source=solutions/environments/practice-deploy.yml
name: Practice deployment

on:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  build:
    name: Build
    runs-on: ubuntu-latest
    timeout-minutes: 5
    steps:
      - name: Show the settings this job can read
        env:
          LOG_LEVEL: ${{ vars.LOG_LEVEL || 'not set' }}
          HAS_DEPLOY_TOKEN: ${{ secrets.DEPLOY_TOKEN != '' }}
        run: |
          echo "LOG_LEVEL is $LOG_LEVEL"
          echo "DEPLOY_TOKEN is available: $HAS_DEPLOY_TOKEN"

  deploy:
    name: Deploy to staging
    needs: build
    runs-on: ubuntu-latest
    timeout-minutes: 5
    environment:
      name: staging
      url: ${{ steps.deploy.outputs.url }}
    steps:
      - name: Show the settings this job can read
        env:
          LOG_LEVEL: ${{ vars.LOG_LEVEL || 'not set' }}
          HAS_DEPLOY_TOKEN: ${{ secrets.DEPLOY_TOKEN != '' }}
        run: |
          echo "LOG_LEVEL is $LOG_LEVEL"
          echo "DEPLOY_TOKEN is available: $HAS_DEPLOY_TOKEN"

      - name: Pretend to deploy
        id: deploy
        run: |
          echo "Deploying commit $GITHUB_SHA to staging. Nothing leaves GitHub."
          echo "url=$GITHUB_SERVER_URL/$GITHUB_REPOSITORY/tree/$GITHUB_SHA" >> "$GITHUB_OUTPUT"
```

5. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `main` branch, then select **Commit changes**.

### What's in the workflow

- `on: workflow_dispatch` is the only trigger, so the workflow runs only when you start it.
- The `build` job stands in for a real build. The `deploy` job waits for it to succeed, because of `needs: build`. Neither job checks out your code or contacts anything outside GitHub. **Pretend to deploy** only prints a message.
- `environment` makes `deploy` a deployment job, and `name: staging` is the environment it deploys to. If your repository has no environment with that name, GitHub creates one the first time the job runs.
- `url` is the address of what the job deployed, and GitHub shows it with the deployment. Here it comes from the `url` output of the step with `id: deploy`. There's no real site, so that step points the address at your commit's files on GitHub. `GITHUB_SERVER_URL`, `GITHUB_REPOSITORY`, and `GITHUB_SHA` are variables that GitHub sets on every runner, and a line written to the `GITHUB_OUTPUT` file becomes a step output.
- **Show the settings this job can read** reads a variable, `LOG_LEVEL`, and a secret, `DEPLOY_TOKEN`. You'll create both later. `vars.LOG_LEVEL || 'not set'` gives the text `not set` while the variable doesn't exist. A secret that doesn't exist reads as an empty string, so `secrets.DEPLOY_TOKEN != ''` is `true` only when the job can read the secret. The log shows `true` or `false`, never the secret itself.

Committing the file doesn't start the workflow.

## Run it and find the deployment

1. Select the **Actions** tab, then **Practice deployment** in the left sidebar.
2. Select **Run workflow**. In the menu that opens, leave the branch set to `main`, then select the green **Run workflow** button.
3. Select the new run. When **Build** and **Deploy to staging** both have a green check mark, select **Deploy to staging** under **Jobs** in the left sidebar, and expand **Show the settings this job can read**. Neither setting exists yet:

```text
LOG_LEVEL is not set
DEPLOY_TOKEN is available: false
```

4. Select the **Code** tab. The repository's main page now shows your environment. In the right-hand sidebar, select **Deployments**.
5. On the "Deployments" page, select `staging` in the "Environments" section of the left sidebar. The list shows your deployment with the message of the commit it deployed.
6. To the right of the commit message, select the external link icon. It opens the deployment's URL, which is your commit's files on GitHub.
7. Go back to the "Deployments" page. To the right of the commit message, select the three dots, then **View logs**. It opens the run that made the deployment.

If your copy is private on GitHub Free, continue at [Add a manual gate without protection rules](#add-a-manual-gate-without-protection-rules).

## Give staging its own settings

1. Select the **Settings** tab. In the left sidebar, in the "Security" section, select **Secrets and variables**, then **Actions**.
2. Select the **Variables** tab, then **New repository variable**.
3. In **Name**, type `LOG_LEVEL`. In **Value**, type `Warning`. Select **Add variable**. Every job in the repository can read this variable.
4. In the left sidebar, select **Environments**, then `staging`.
5. Under **Environment variables**, select **Add Variable**. Enter `LOG_LEVEL` as the name and `Debug` as the value, then select **Add variable**.
6. Under **Environment secrets**, select **Add Secret**. Enter `DEPLOY_TOKEN` as the name and any made-up text as the value, such as `practice-only`, then select **Add secret**. Don't use a real token, because nothing in the workflow uses the value.
7. Run **Practice deployment** again from `main`.

When the run finishes, open **Show the settings this job can read** in each job. **Build** shows:

```text
LOG_LEVEL is Warning
DEPLOY_TOKEN is available: false
```

**Deploy to staging** shows:

```text
LOG_LEVEL is Debug
DEPLOY_TOKEN is available: true
```

Both jobs read `vars.LOG_LEVEL`, but only **Deploy to staging** references `staging`, so only it gets the environment's values. When the environment and the repository both have a variable with the same name, the environment's value wins, and the same goes for secrets. **Build** can't read the environment secret at all. A job that deploys to a `production` environment could get different values under the same names, with no change to its steps. See [configuration variable precedence](https://docs.github.com/en/actions/reference/workflows-and-actions/variables#configuration-variable-precedence) in the GitHub docs.

## Limit which branches can deploy

1. Select the **Settings** tab, then **Environments** in the left sidebar, then `staging`.
2. In the **Deployment branches** dropdown, select **Selected branches and tags**.
3. Select **Add deployment branch or tag rule**. In the "Ref type" dropdown, select **Branch**. Type `main` as the name pattern, then select **Add rule**.
4. Select the **Code** tab, then the branch menu above the list of files. In the **Find or create a branch...** box, type `try-staging`, then select **Create branch**.
5. Select the **Actions** tab, then **Practice deployment**, then **Run workflow**. In the menu that opens, select `try-staging` in the **Branch** dropdown, then select the green **Run workflow** button.

Open the new run. **Build** passes, but **Deploy to staging** fails within seconds, without running any of its steps. Under **Annotations**, the run's **Summary** says why:

```text
Branch "try-staging" is not allowed to deploy to staging due to environment protection rules.
The deployment was rejected or didn't satisfy other protection rules.
```

The rule is matched against the branch the run uses, which is the one you picked in the **Run workflow** menu. A run from `main` still deploys. The rule is stored in the environment's settings, not in the workflow file, so editing the workflow on `try-staging` can't get around it.

Name patterns can use wildcards, such as `release/*`, and a rule with **Tag** as its ref type matches tags instead of branches. The dropdown has two other choices. **No restriction**, which `staging` had until now, lets any branch or tag deploy. **Protected branches only** lets only branches with branch protection rules deploy, and if no branch has any, every branch can deploy. See [deployment branches and tags](https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments#deployment-branches-and-tags) in the GitHub docs.

## Require an approval

Required reviewers need a public repository, or a private one on GitHub Enterprise Cloud. If your copy is private on another plan, continue at [Add a manual gate without protection rules](#add-a-manual-gate-without-protection-rules) instead.

1. Select the **Settings** tab, then **Environments** in the left sidebar, then `staging`.
2. Select **Required reviewers**, then enter your own username as the reviewer.
3. Leave **Prevent self-review** cleared. When it's selected, nobody can approve a run they started, so you couldn't approve your own.
4. Select **Save protection rules**.
5. Run **Practice deployment** from `main`, and open the run. **Build** runs as before, but **Deploy to staging** doesn't start. Its status is **Waiting**.
6. On the notification for the review request, select **Review deployments**.
7. Select `staging`. You can leave a comment if you like. Then select **Approve and deploy**.

**Deploy to staging** starts, finishes as before, and adds a new deployment to the "Deployments" page.

If you select **Reject** instead, the job doesn't run, and the workflow fails. A job can wait up to 30 days for an approval. A job gets the environment's secrets only after every protection rule passes, so a deployment you reject never gets `DEPLOY_TOKEN`.

You can list up to six people or teams as reviewers, and one approval from any of them is enough. Each reviewer needs at least read access to the repository. In a team, select **Prevent self-review**, so every deployment needs someone other than the person who started it.

The same page has two more rules. **Wait timer** makes the job wait a set number of minutes, from 1 to 43,200 (30 days), before it can start, and that wait doesn't count toward your billable minutes. **Allow administrators to bypass configured protection rules** is selected by default, so a repository admin can start a waiting job without an approval: on the run, to the right of **Deployment protection rules**, they select **Start all waiting jobs**. Clear it if the rules should apply to admins too.

## Add a manual gate without protection rules

Use this section if you can't add required reviewers, because your copy is private on GitHub Free, Pro, or Team. Without reviewers, a run deploys as soon as someone starts it. The workflow can't ask for a real approval, but it can refuse to deploy unless the run uses `main` and the person who starts it types `staging` to confirm.

### Add a repository variable and secret

Skip these steps if you did [Give staging its own settings](#give-staging-its-own-settings).

1. Select the **Settings** tab. In the left sidebar, in the "Security" section, select **Secrets and variables**, then **Actions**.
2. Select the **Variables** tab, then **New repository variable**. In **Name**, type `LOG_LEVEL`. In **Value**, type `Warning`. Select **Add variable**.
3. Select the **Secrets** tab, then **New repository secret**. In **Name**, type `DEPLOY_TOKEN`. In **Secret**, type any made-up text, such as `practice-only`. Select **Add secret**.

### Add the confirmation

1. Select the **Code** tab, and make sure the branch menu shows `main`.
2. Select `.github`, then `workflows`, then `practice-deploy.yml`.
3. Select the pencil icon to edit the file.
4. Add these lines below `workflow_dispatch:`. The `inputs:` line starts four spaces from the left edge, two more than `workflow_dispatch:`.

```yaml
    inputs:
      confirm:
        description: Type staging to deploy
        required: true
```

5. In the `deploy` job, add this line below `needs: build`, lined up with it:

```yaml
    if: github.ref == 'refs/heads/main' && inputs.confirm == 'staging'
```

6. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `main` branch, then select **Commit changes**.

<details>
<summary>Show the two changed parts of the file</summary>

The top of the file:

```yaml
on:
  workflow_dispatch:
    inputs:
      confirm:
        description: Type staging to deploy
        required: true
```

The start of the `deploy` job:

```yaml
  deploy:
    name: Deploy to staging
    needs: build
    if: github.ref == 'refs/heads/main' && inputs.confirm == 'staging'
    runs-on: ubuntu-latest
```

</details>

`inputs` adds a required text box to the **Run workflow** menu. `github.ref` is the branch the run uses, written as `refs/heads/` followed by the branch name, and `inputs.confirm` is what you typed. When the `if` condition is false, GitHub skips the job.

### Try the gate

1. Select the **Actions** tab, then **Practice deployment**, then **Run workflow**. The menu now has a text box for the confirmation. Leave the branch set to `main`, type `nope` in the box, then select the green **Run workflow** button.

   The run gets a green check mark, but **Deploy to staging** was skipped, and no new deployment appears on the "Deployments" page.

2. Run the workflow again from `main`, and type `staging` this time.

   Both jobs run, and the "Deployments" page lists the new deployment. If you added the repository secret in this section, both jobs show `DEPLOY_TOKEN is available: true`.

A run from any branch other than `main` skips the deploy job too, whatever you type. This gate is a typed confirmation, though, not an approval:

- Nobody else signs off, nothing waits, and GitHub keeps no record of an approval. Anyone who can start the workflow can type `staging`.
- The check is in the workflow file. A manual run uses the workflow file from the branch you pick, so anyone who can push a branch can delete the `if` line there and deploy from that branch. An environment's rules are in the repository's settings, where a workflow file can't change them.
- A repository secret is available to every job, not only the one that deploys, which is why **Build** can read `DEPLOY_TOKEN` here.
- A skipped deploy job still leaves a run with a green check mark, so check the job, not only the run.

For real approvals, change your copy to public. Then remove the `inputs` lines and the `if` line you added, and delete `DEPLOY_TOKEN` on the **Secrets** tab, so the next sections behave as described. Continue at [Give staging its own settings](#give-staging-its-own-settings), and skip its steps 2 and 3, because the repository already has `LOG_LEVEL`. On GitHub Free, if you make the copy private again later, GitHub ignores the environment's protection rules and secrets until the copy is public again.

## Deploy your Azure app through staging

You need the **Deploy to Azure** workflow from [Lab 7](azure-oidc.md) for this section, and the `main` rule from [Limit which branches can deploy](#limit-which-branches-can-deploy). Once the `deploy` job references `staging`, only that rule keeps a run from another branch out of Azure. Skip this section if your copy is private on GitHub Free. There, `staging` can't have the rule, and Lab 7's setup, where only runs from `main` can sign in, is safer.

When a job references an environment, the subject of its OIDC token changes from `PREFIX:ref:refs/heads/main` to `PREFIX:environment:staging`, whatever branch the run uses. `PREFIX` is your repository's subject prefix, which starts with `repo:`. Your managed identity's `github-branch-main` federated credential no longer matches, so the identity needs a second credential for the environment.

### Add a federated credential for staging

If your instructor set up Azure for you, this credential already exists, so continue at [Add the environment to the deploy job](#add-the-environment-to-the-deploy-job).

If you ran `scripts/azure/learner-setup.sh` in Lab 7, run it again with the same arguments plus `--environment staging`. It keeps what already exists, adds a `github-env-staging` credential, and prints its subject.

Otherwise, look up your repository's subject prefix, because repositories created after July 15, 2026 have numeric IDs in it. In a terminal where `gh` is signed in, run this command with your own `OWNER/REPO`:

```bash
gh api repos/OWNER/REPO/actions/oidc/customization/sub --jq .sub_claim_prefix
```

If your repository is public, you can open `https://api.github.com/repos/OWNER/REPO/actions/oidc/customization/sub` in your browser instead, and copy the `sub_claim_prefix` value. You can also open your latest **Deploy to Azure** run, select the `deploy` job, and expand **Sign in to Azure**. The line that starts with `subject claim -` shows the subject, and the prefix is the part before `:ref:refs/heads/main`.

Then create the credential in Bash, either in Azure Cloud Shell or in a terminal where you've run `az login`. Replace `APPNAME` with the name you used in Lab 7, and `PREFIX` with your prefix:

```bash
az identity federated-credential create \
  --name github-env-staging \
  --identity-name id-APPNAME \
  --resource-group rg-APPNAME \
  --issuer https://token.actions.githubusercontent.com \
  --subject "PREFIX:environment:staging" \
  --audiences api://AzureADTokenExchange
```

Azure compares the subject exactly, including uppercase and lowercase letters, so paste the prefix instead of typing it.

### Add the environment to the deploy job

1. On `main`, open `.github/workflows/deploy-azure.yml`, and select the pencil icon to edit the file.
2. In the `deploy` job, add these lines below `timeout-minutes: 15`, lined up with it:

```yaml
    environment:
      name: staging
      url: ${{ steps.deploy-to-webapp.outputs.webapp-url }}
```

3. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `main` branch, then select **Commit changes**.
4. Run **Deploy to Azure** from `main`. If `staging` has a required reviewer, approve the deployment as you did for the practice workflow.

`deploy-to-webapp` is the `id` of the **Deploy to App Service** step, and `webapp-url` is the address that step reports for your web app. [The finished workflow](../../solutions/environments/deploy-azure-staging.yml) shows the whole file.

When the run finishes, the "Deployments" page lists a new `staging` deployment, and its external link icon opens your web app. The `staging` branch rule now decides which branches can deploy to Azure, because the token's subject no longer names a branch.

## What happened

When a run reached **Deploy to staging**, GitHub checked the environment's protection rules before the job could start. A run from a branch the rules didn't allow failed without running a step, and a run that needed an approval waited. Only after every rule passed did the job start and get the environment's variables and secrets, which take precedence over repository and organization values with the same names. **Build** never referenced `staging`, so it only saw the repository's values.

Each run of the deploy job also created a deployment: a record of which commit went to which environment, from which run, with statuses that changed as the job progressed. The workflow's `url` became the deployment's `environment_url`, the link on the "Deployments" page. Other tools can read the same records through the REST API, or receive them as webhook events.

An environment's rules are part of the repository's settings, not the workflow file. Anyone who can edit workflows can create an environment by naming it in a workflow, as your first run did, but only the repository's owner, or an admin in an organization, can configure it. The manual gate can't match that, because its check sits in a file that anyone who can push a branch can change. See [managing environments for deployment](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments) in the GitHub docs.

## Check your result

Select the **Code** tab, then **Deployments** in the right-hand sidebar, then `staging`. The list shows your deployments from `main`, and each one's external link icon opens your commit's files on GitHub. In **Settings**, under **Environments**, `staging` has the environment variable `LOG_LEVEL`, the environment secret `DEPLOY_TOKEN`, a deployment branch rule for `main`, and you as a required reviewer, as far as your plan allows.

If you used the manual gate, the **Practice deployment** runs include one where **Deploy to staging** was skipped and a later one where it ran. If you did the Azure section, the newest `staging` deployment links to your web app.

## If something fails

If **Practice deployment** isn't in the **Actions** sidebar, or it has no **Run workflow** button, the file isn't on `main` or its path is wrong. A workflow has to be on the default branch before you can run it by hand, and GitHub only reads workflow files in the `.github/workflows` folder. Check that `main` has the file at exactly `.github/workflows/practice-deploy.yml`.

If **Deploy to staging** fails within seconds with `Branch "main" is not allowed to deploy to staging due to environment protection rules.`, the deployment branch rule doesn't match `main`. Open `staging` under **Environments** in **Settings**, check that the rule's name pattern is `main` and its type is **Branch**, and run the workflow again.

If **Deploy to staging** still shows `LOG_LEVEL is Warning` or `DEPLOY_TOKEN is available: false`, the environment doesn't have those values. Variables and secrets you add on the **Secrets and variables** page belong to the repository. Open `staging` under **Environments**, and check that `LOG_LEVEL` is listed under **Environment variables** and `DEPLOY_TOKEN` under **Environment secrets**.

If the run didn't wait for an approval, the reviewer wasn't saved. Open `staging`, check that **Required reviewers** is selected and lists you, select **Save protection rules**, and run the workflow again.

If you can't approve the waiting job, check that you're listed under **Required reviewers** and that **Prevent self-review** is cleared. Then open the run, select **Cancel workflow** in its upper-right corner, and start a new run. As an admin, you can also start the waiting job without an approval. On the run, to the right of **Deployment protection rules**, select **Start all waiting jobs**. In the window that opens, select `staging`, enter a reason under **Leave a comment**, then select **I understand the consequences, start deploying**.

If **Deploy to staging** was skipped after you added the manual gate, the run didn't use `main`, or the text you typed wasn't `staging`. The comparison ignores uppercase and lowercase, so `Staging` works too. Check the branch and the text in the **Run workflow** menu, and run the workflow again.

If **Sign in to Azure** fails with `AADSTS700213: No matching federated identity record found for presented assertion subject`, no federated credential on the identity matches the token's subject. The error message includes the subject GitHub sent. Compare it with the subjects that `az identity federated-credential list --identity-name id-APPNAME --resource-group rg-APPNAME` prints, including uppercase and lowercase letters. If they match and you added the credential a few minutes ago, wait a few minutes and run the workflow again, because [a new federated credential takes time to propagate](https://learn.microsoft.com/entra/workload-id/workload-identity-federation-considerations#time-for-federated-credential-changes-to-propagate).

## Clean up

Nothing this lab adds costs money or keeps running, so cleanup is optional.

1. If you did the Azure section, first remove the three `environment` lines from `.github/workflows/deploy-azure.yml`. Otherwise the next **Deploy to Azure** run creates `staging` again without any rules, and a run from any branch could sign in to Azure. Without those lines, the job signs in with the `github-branch-main` credential again.
2. Select the **Settings** tab, then **Environments** in the left sidebar. Select the trash icon next to `staging`, then **I understand, delete this environment**. That also deletes its variable, secret, and rules. GitHub keeps the deployment records.
3. In the left sidebar, select **Secrets and variables**, then **Actions**. Delete `LOG_LEVEL` on the **Variables** tab, and `DEPLOY_TOKEN` on the **Secrets** tab if you created it there.
4. If you created `try-staging`, select the **Code** tab, then the branch menu, then **View all branches**. Select the trash icon next to `try-staging`.

`.github/workflows/practice-deploy.yml` can stay, because it only runs when you start it. The `github-env-staging` credential belongs to your managed identity, so it's deleted along with the identity when you clean up Lab 7's Azure resources.

## Next step

You can do the labs after Lab 2 in any order, so pick your next one from [the lesson list](../../README.md). If you skipped [Lab 7](azure-oidc.md), it deploys the dashboard to Azure, and afterward you can come back to [Deploy your Azure app through staging](#deploy-your-azure-app-through-staging).
