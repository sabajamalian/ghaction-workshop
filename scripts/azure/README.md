# Azure setup scripts

These scripts create the Azure side of a GitHub Actions deployment that signs in with OpenID Connect (OIDC): an App Service web app, and a user-assigned managed identity that the workflow signs in as. The identity trusts tokens that GitHub issues to one repository's jobs, and it can deploy to one web app. The scripts create no secret, password, or publish profile, and the workflow needs only four repository variables.

| Script | Who runs it | What it sets up |
| --- | --- | --- |
| `learner-setup.sh` | A learner with their own Azure subscription | A web app for the learner's copy of the workshop, for [Lab 7](../../lessons/explore/azure-oidc.md), and with `--environment staging`, for [Lab 8](../../lessons/explore/environments.md) |
| `instructor-setup.sh` | An instructor, for a class | A web app for each learner in the instructor's subscription, ready for Labs 7 and 8 |

`common.sh` holds the code the two scripts share. You don't run it yourself.

Every script has `--help`, which lists its options and defaults, and `--dry-run`, which shows what it would do without changing anything.

## Before you run a script

You need:

- Bash 3.2 or later, on macOS, Linux, or Windows Subsystem for Linux, or [Azure Cloud Shell](#use-azure-cloud-shell).
- The [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) 2.58.0 or later, signed in with `az login`. The scripts use the subscription that `az account show` reports. To use another one, run `az account set --subscription NAME-OR-ID` first.
- `curl`, or the GitHub CLI signed in with `gh auth login`. The scripts read each repository's OIDC subject prefix from GitHub, as [The OIDC subject](#the-oidc-subject) explains. For a private repository, `gh` has to be signed in as someone who can read it.
- For `instructor-setup.sh`, also `sha256sum` or `shasum`. Linux has the first and macOS the second.
- The Azure roles that [Permissions](#permissions) lists.

Run the scripts from the root of your clone of the repository. Put `bash` in front, so they run even if the files lost their executable permission:

```bash
bash scripts/azure/learner-setup.sh --help
```

### Use Azure Cloud Shell

Azure Cloud Shell runs in your browser. Its Bash has the Azure CLI already signed in as you, plus Git and the GitHub CLI.

1. Open [shell.azure.com](https://shell.azure.com), or sign in to the [Azure portal](https://portal.azure.com) and select **Cloud Shell** in the top navigation.
2. The first time, choose **Bash**. In the **Getting started** pane, select **No storage account required**, choose your subscription, and select **Apply**.
3. Clone your repository and change to its folder:

   ```bash
   git clone https://github.com/OWNER/REPO.git
   cd REPO
   ```

   For a private repository, run `gh auth login` and follow its prompts, then clone with `gh repo clone OWNER/REPO` instead. Signing in also lets the scripts read your repository's subject prefix.

Cloud Shell needs the `Microsoft.CloudShell` resource provider registered in your subscription. If it doesn't start, follow [Cloud Shell's getting started steps](https://learn.microsoft.com/azure/cloud-shell/get-started/ephemeral).

## What the scripts create

| Resource | Name | Settings |
| --- | --- | --- |
| Resource group | `rg-APPNAME` | Tagged `created-by=ghaction-workshop`, like everything the scripts create |
| App Service plan | `plan-APPNAME` | Linux, F1 or B1 |
| Web app | `APPNAME` | .NET 10 runtime (`DOTNETCORE:10.0`), HTTPS only, basic authentication off |
| User-assigned managed identity | `id-APPNAME` | Not attached to the web app |
| Federated credentials | `github-branch-main`, and `github-env-NAME` for each environment | On the identity. Each trusts tokens for one repository's branch or environment. |
| Role assignment | Website Contributor | For the identity, on the web app only |

`APPNAME` is the `--app-name` you choose. The site's address is `https://APPNAME.azurewebsites.net`, so the name has to be unique across Azure. The scripts check that with Azure's name availability API before they create anything. `instructor-setup.sh` names its resources after `--prefix` instead, as [instructor-setup.sh](#instructor-setupsh) describes.

The web app needs no startup command or app settings. App Service finds the published app's one `.runtimeconfig.json` file and starts `WorkshopDashboard.dll` with the .NET runtime.

With basic authentication off, the app has no FTP password or publish profile that could leak. Deployments have to sign in to Azure, the way the workflow does as the identity.

The four values the workflow needs are the identity's client ID, the tenant ID, the subscription ID, and the web app's name. They identify things but aren't credentials, so they go in repository variables, not secrets. Knowing them doesn't let anyone sign in, because Azure only accepts a GitHub token whose subject matches one of the identity's federated credentials.

## Permissions

Your Azure account needs these roles on the subscription, or on the resource group if it already exists:

- Contributor or Owner, to create the resource group, the plan, the web app, the identity, and its federated credentials.
- Owner, User Access Administrator, or Role Based Access Control Administrator, to give the identity the Website Contributor role. Contributor can't assign roles.

So Owner alone is enough, and so is Contributor plus one of the other two. You don't need a Microsoft Entra role, because the scripts don't create an app registration. Microsoft's guide to [configuring a federated credential on a user-assigned managed identity](https://learn.microsoft.com/entra/workload-id/workload-identity-federation-create-trust-user-assigned-managed-identity) also says that creating the identity and its federated credential needs Contributor or Owner.

To see your role assignments in the current subscription, including the ones you have through groups:

```bash
az role assignment list --assignee "$(az ad signed-in-user show --query id --output tsv)" --all --include-groups --query "[].[roleDefinitionName, scope]" --output tsv
```

If a subscription hasn't used the `Microsoft.Web` or `Microsoft.ManagedIdentity` resource provider before, the scripts register it, which needs Contributor or Owner on the subscription. If you only have roles on a resource group, ask a subscription owner to run `az provider register --namespace NAMESPACE`.

If Azure refuses the role assignment, the script stops after creating everything else and says so. Ask someone with one of those roles to run the `az role assignment create` command the script printed, or to give you the role, then run the script again. It reuses what it already created.

## The OIDC subject

A federated credential names the tokens the identity accepts: the issuer `https://token.actions.githubusercontent.com`, the audience `api://AzureADTokenExchange`, and a subject. Azure only accepts a token when all three match exactly. The subject GitHub puts in a job's token depends on the job:

| Job | Subject | Credential name |
| --- | --- | --- |
| No `environment`, run from `main` | `PREFIX:ref:refs/heads/main` | `github-branch-main` |
| `environment: staging` | `PREFIX:environment:staging` | `github-env-staging` |

`PREFIX` identifies the repository. For repositories created after July 15, 2026, it includes the numeric IDs of the owner and the repository, such as `repo:octo-org@123456/octo-repo@456789`, so a repository that later takes the same name can't get a matching token. Older repositories use `repo:OWNER/REPO` unless someone opted them in, or they were renamed or transferred after that date. [Immutable subject claims](https://docs.github.com/en/actions/reference/security/oidc#immutable-subject-claims) in the GitHub docs has the details. Every copy of the workshop is a new repository with its own IDs, so each copy needs its own credentials.

The scripts get the prefix from the `sub_claim_prefix` field of GitHub's [OIDC subject customization endpoint](https://docs.github.com/en/rest/actions/oidc#get-the-customization-template-for-an-oidc-subject-claim-for-a-repository). They use `gh` when it's signed in, and `curl` without signing in otherwise, which only works for public repositories. You can find the prefix yourself in three ways:

- In a terminal where `gh` is signed in, run `gh api repos/OWNER/REPO/actions/oidc/customization/sub --jq .sub_claim_prefix`.
- For a public repository, open `https://api.github.com/repos/OWNER/REPO/actions/oidc/customization/sub` in your browser and copy the `sub_claim_prefix` value.
- Run the **Deploy to Azure** workflow. In the `deploy` job, expand **Sign in to Azure**. The line that starts with `subject claim -` shows the job's subject, and the prefix is the part before `:ref:refs/heads/main`.

When a script can't look the prefix up, such as for a private repository without `gh`, pass it with `--subject-prefix`. For `instructor-setup.sh`, add it after the repository's name in the learners file. Some guides, including the troubleshooting section of the `azure/login` README, show subjects without IDs. Those are only right for repositories created before July 15, 2026.

### Custom subject claims

If the endpoint returns `"use_default": false`, someone has customized the subject template for the repository, so its subjects don't follow the formats above. The scripts stop instead of guessing. To set up Azure anyway:

1. Run the script with `--subject-prefix repo:OWNER/REPO`, using the repository's owner and name. It creates everything, but its federated credentials won't match the repository's tokens.
2. Run the workflow. **Sign in to Azure** fails, and its log has a line that starts with `subject claim -`. Copy the subject that follows it.
3. Add a federated credential for that exact subject, replacing `APPNAME` and `SUBJECT`:

   ```bash
   az identity federated-credential create \
     --name github-custom \
     --identity-name id-APPNAME \
     --resource-group rg-APPNAME \
     --issuer https://token.actions.githubusercontent.com \
     --subject "SUBJECT" \
     --audiences api://AzureADTokenExchange
   ```

4. Wait a minute or two, then run the workflow again.

Running the script again later leaves `github-custom` alone. [Customizing the token claims](https://docs.github.com/en/actions/reference/security/oidc#customizing-the-token-claims) explains how subject templates work.

## Costs

The App Service plan is the only resource the scripts create that has a price. [Managed identities have no extra cost](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/overview).

- F1 (Free) costs nothing, but it has quotas, such as 1 GB of memory and 60 CPU minutes a day. When an app goes over its CPU or bandwidth quota, App Service [stops it until the quota resets](https://learn.microsoft.com/azure/app-service/web-sites-monitor), and it returns HTTP 403 in the meantime. Free apps can't use Always On, so App Service unloads an app after 20 minutes without requests, and the next request waits while it starts again. A Free plan holds at most 10 apps, and Azure allows 10 Free plans per region. See [App Service limits](https://learn.microsoft.com/azure/azure-resource-manager/management/azure-subscription-service-limits#azure-app-service-limits).
- B1 (Basic) is billed by the hour for each plan, whether or not anyone visits the apps. At the time of writing, a Linux B1 plan in East US costs $0.017 an hour, about $0.41 a day or $12.41 for a 730-hour month. Prices vary by region, so check [App Service pricing](https://azure.microsoft.com/pricing/details/app-service/linux/) for yours.

Each **Deploy to Azure** run has two jobs. Standard GitHub-hosted runners are free in public repositories. In a private repository, each job uses the owner's included Actions minutes, rounded up to a whole minute.

## learner-setup.sh

Choose a web app name, such as `gha-lab7-` followed by your GitHub username in lowercase. Preview the commands, then run them, with your own `OWNER/REPO`:

```bash
bash scripts/azure/learner-setup.sh --app-name APPNAME --repo OWNER/REPO --dry-run
bash scripts/azure/learner-setup.sh --app-name APPNAME --repo OWNER/REPO
```

The script uses an F1 plan unless you pass `--sku B1`. It trusts jobs from `main` without an environment, which is what [Lab 7](../../lessons/explore/azure-oidc.md) needs. For [Lab 8](../../lessons/explore/environments.md), run it again with the same arguments plus `--environment staging`. It keeps what exists and adds a `github-env-staging` credential.

At the end, the script prints the four repository variables and their values, the site's address, `gh variable set` commands that add the variables, the steps to add them in the browser, the subjects the identity trusts, and the cleanup command.

## instructor-setup.sh

This script sets up Azure for a whole class, so learners can do the Azure parts of Labs 7 and 8 without an Azure subscription. List the learners' repositories in a text file, one `OWNER/REPO` per line. Blank lines and anything after `#` are ignored:

```text
# Learner repositories for the October workshop
octocat/ghaction-workshop
mona/ghaction-workshop repo:mona@123456/ghaction-workshop@456789
```

The second word on a line is optional. It's that learner's subject prefix, for a private repository the script can't read. To read private repositories instead, sign in with `gh auth login` as someone who can read them, such as an owner of the organization the copies are in. A learner can also get their own prefix with the `gh api` command in [The OIDC subject](#the-oidc-subject) and send it to you.

Preview, then run:

```bash
bash scripts/azure/instructor-setup.sh --learners learners.txt --prefix gha-oct26 --dry-run
bash scripts/azure/instructor-setup.sh --learners learners.txt --prefix gha-oct26
```

The script checks every line, repository, and web app name before it creates anything, and stops with a list of problems if any line has one.

For each learner, it creates a web app named `PREFIX-` followed by the first 8 hexadecimal characters of the SHA-256 hash of the repository's name in lowercase. The name stays the same when you run the script again, and the address doesn't show the learner's GitHub name. Each learner also gets an identity named `id-` followed by the app's name, with two federated credentials: `github-branch-main` for Lab 7 and `github-env-staging` for Lab 8. The web apps share Linux plans named `plan-PREFIX-1`, `plan-PREFIX-2`, and so on, all in the resource group `rg-PREFIX`.

### Why the apps share plans

Each learner needs their own web app and identity, but not their own plan or resource group. The script could give each learner a resource group with its own plan, the way `learner-setup.sh` does. It puts the whole class in one resource group with shared plans instead:

- The plan is what costs money, and each B1 plan is billed by the hour. For 24 learners, 3 shared plans cost about $1.22 a day at the price above, and 24 separate plans about $9.79 a day.
- Basic plans have no fixed limit on apps, but Microsoft's [App Service plan guidance](https://learn.microsoft.com/azure/app-service/overview-hosting-plans) suggests at most 8 apps on a B1 plan, so `--apps-per-plan` defaults to 8.
- F1 costs nothing, but a plan for each learner would run into the limit of 10 Free plans per region, and a shared Free plan holds at most 10 apps. Free apps also stop when they go over a quota, which you'd rather not find out about during a session. So the script uses B1 unless you pass `--sku F1`.
- With one resource group, one command deletes everything after the workshop.

Sharing a plan doesn't share access. Each identity has a role on its own web app only, and its federated credentials trust only its learner's repository. If a learner sets another learner's client ID, Azure still refuses the sign-in, because their token's subject names their own repository. Apps in a plan do share its CPU and memory, so one busy app can slow the others down.

### Send learners their values

The script prints each learner's site and `gh variable set` commands, and writes the same values to `PREFIX-azure-values.csv`, or the file you name with `--output`. The CSV has one row per learner, with the columns `repository`, `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_WEBAPP_NAME`, and `site_url`. Every learner sees your tenant ID and subscription ID. Neither grants access on its own. The file maps learners to web apps, so keep it out of the repository.

Send each learner their row. They add the four values as repository variables, as Lab 7 describes, and skip the Azure setup. Their identity already trusts their `staging` environment, so Lab 8 needs no changes in Azure.

To add learners later, add their repositories to the file and run the script again with the same `--prefix`. It reuses the existing apps and fills the existing plans before it creates another one.

If the learners' copies are in an organization that only allows selected actions, also allow the Azure actions, as [Actions the labs use](../../docs/instructor-guide.md#actions-the-labs-use) describes.

## Dry runs and running a script again

With `--dry-run`, a script runs only read-only checks: your Azure sign-in, the name availability check, the GitHub lookup, and queries for existing resources. It prints every command that would change something, with the values filled in. Values that only exist once Azure creates the identity show as placeholders, such as `<client-id-of-id-APPNAME>`. `instructor-setup.sh` doesn't write its CSV file in a dry run.

You can run a script again with the same arguments at any time. It reuses what exists, creates what's missing, and updates a federated credential whose subject changed. `--sku` and `--location` only apply to resources the script creates.

## Clean up

Each script ends with the command that deletes what it created. When a script created the resource group, that's one command:

```bash
az group delete --name rg-APPNAME
```

It asks for confirmation. Add `--yes` to skip the question, or `--no-wait` to return before Azure finishes. Deleting the group deletes the web app, the plan, the identity, and its federated credentials, and billing for a B1 plan stops once the plan is gone.

If you passed `--resource-group` with a group the scripts didn't create, the script prints commands that delete only the role assignment, the identity, and the web app, so nothing else in the group is touched. Deleting the last web app on a plan also deletes the plan.

To list what's left from any of the scripts in the current subscription:

```bash
az group list --tag created-by=ghaction-workshop --output table
az resource list --tag created-by=ghaction-workshop --output table
```

Then delete the four repository variables in GitHub, on the **Variables** tab or with `gh variable delete NAME --repo OWNER/REPO`.
