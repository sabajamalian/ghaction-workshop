# Deploy the workshop site

The workshop's website is the dashboard in this repository, running on Azure App Service. You deploy it by hand with `maintainers/scripts/deploy-site.sh`, from your own machine or from Azure Cloud Shell. The script runs every test, publishes the dashboard, uploads it with `az webapp deploy`, and then checks that the site reports the commit it just built.

There's no deployment workflow in `.github/workflows`, because every workflow there shows up in every learner's copy of the template. The site doesn't change when `main` changes, so run the script again after you merge lesson or app changes. Learners who want to deploy their own copy follow [Lab 7](../lessons/explore/azure-oidc.md), which deploys from a workflow with OpenID Connect (OIDC).

The site runs at [ghaction-workshop.azurewebsites.net](https://ghaction-workshop.azurewebsites.net), on a B1 plan in Central US. Its resources are in the resource group `rg-github-workshop`, so `az group delete --name rg-github-workshop` deletes them.

## How the sign-in works

The deploy script uses your Azure CLI sign-in. Nothing in GitHub stores an Azure secret or can deploy the site, and the web app has no identity, publish profile, or FTP password: basic authentication is off, so `az webapp deploy` authenticates with your Microsoft Entra token.

## What the setup creates

| Resource | Default name | Purpose |
| --- | --- | --- |
| Resource group | `rg-APPNAME` | Holds everything below, so one command deletes it all. |
| App Service plan | `plan-APPNAME` | The Linux compute the site runs on. B1 by default. |
| Web app | `APPNAME` | The site itself. HTTPS only, with basic authentication off. |

`APPNAME` is the web app's name. It must be unique across Azure, because it becomes part of the site's address.

## Before you start

You need:

- An Azure subscription, and the Azure CLI 2.58.0 or later signed in to it. Run `az login`, then `az account set --subscription SUBSCRIPTION_ID`. Azure Cloud Shell works too, because it has Bash and a signed-in Azure CLI.
- **Contributor** on the subscription, or on an existing resource group, to create the resources. Pass an existing group's name with `--resource-group`. Deploying needs only **Website Contributor** on the web app.
- The `Microsoft.Web` resource provider registered on the subscription. Check with `az provider show --namespace Microsoft.Web --query registrationState`. If it isn't `Registered`, the setup script registers it, which needs Contributor on the subscription.
- To deploy: a clone of this repository, the .NET 10 SDK, `git`, `zip`, `curl`, and `jq`.

## Create the Azure resources

From the repository root, preview the commands first, then run them:

```bash
bash maintainers/scripts/site-setup.sh --app-name APPNAME --dry-run
bash maintainers/scripts/site-setup.sh --app-name APPNAME
```

`--dry-run` prints the Azure CLI commands without running them. By default, the script uses a B1 plan and the resource group `rg-APPNAME`. Change them with `--sku`, `--resource-group`, and `--location`. Run `bash maintainers/scripts/site-setup.sh --help` to see every option and the default location.

When it finishes, the script prints the site's address, the deploy command, and the cleanup command.

## Deploy

1. Check out the commit you want to deploy, usually the latest `main`, with no uncommitted changes.
2. From the repository root, run:

   ```bash
   bash maintainers/scripts/deploy-site.sh --app-name APPNAME --resource-group rg-APPNAME
   ```

The script stops if the checkout has uncommitted changes, because the site would then report a commit that doesn't match what it runs. It restores, runs all the tests, and publishes the dashboard with the commit, the build time, and the repository address stamped into it. Then it deploys the zip file and calls the site's `/health` page every 15 seconds, up to 10 times, until the page reports the commit you deployed.

If your network blocks the default NuGet feed, pass a NuGet.Config file that lists a feed you can reach with `--nuget-config FILE`.

## Check the site

- `/health` returns JSON. `status` is `healthy`, `commit` is the full SHA of the commit you deployed, `lessons` is the number of lessons, and `environment` is `Production`.
- The footer on every page shows the version, the short commit linked to it on GitHub, the build time, and the environment.
- If the site's address changes, update the link near the top of `README.md`.

## Costs

- **B1**, the default: a Linux Basic plan. In East US it's $0.017 an hour, about $12.41 for a 730-hour month (Azure retail price, checked in October 2026). Prices vary by region, so check [App Service pricing](https://azure.microsoft.com/pricing/details/app-service/linux/) for yours. The plan costs the same whether or not anyone visits the site.
- **F1**: free, with a daily CPU quota, and no Always On, so the app unloads when it's idle and the next visit is slow. It's fine for a trial, not for a workshop with many learners at once. Use `--sku F1`.

Deploying from your machine or Cloud Shell uses no GitHub Actions minutes.

## Delete the site

```bash
az group delete --name rg-APPNAME
```

That deletes the plan and the web app. Then remove the site link from `README.md`.

## Remove the old workflow deployment setup

Earlier versions of this repository deployed the site from a **Deploy workshop site** workflow that signed in with OIDC. If your subscription and repository still have that setup, nothing uses it now. You can remove it:

1. Delete the managed identity, which also deletes its federated credential. Its role assignment stays behind as an orphan, so delete that first:

   ```bash
   az role assignment delete --assignee CLIENT_ID --role "Website Contributor" --scope WEBAPP_ID
   az identity delete --name id-APPNAME --resource-group rg-APPNAME
   ```

   `az identity show --name id-APPNAME --resource-group rg-APPNAME --query clientId` prints `CLIENT_ID`, and `az webapp show --name APPNAME --resource-group rg-APPNAME --query id` prints `WEBAPP_ID`.
2. Remove the repository variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, and `AZURE_WEBAPP_NAME`, either on the **Variables** tab under **Settings**, **Secrets and variables**, **Actions**, or with `gh variable delete NAME --repo sabajamalian/ghaction-workshop`.
3. Delete the `production` environment under **Settings**, **Environments**.

## If something fails

If `site-setup.sh` says the name can't be used, web app names are unique across all of Azure. Choose another `--app-name`.

If `deploy-site.sh` can't find the web app, pass the resource group that holds it with `--resource-group`.

If the restore or the tests fail, fix them first. The script deploys only after every test passes.

If `az webapp deploy` fails with an authorization error, check that your account has Website Contributor or Contributor on the web app: `az role assignment list --assignee YOUR_UPN --all --output table`. A new role assignment can take a few minutes to take effect.

If the final check never sees the new commit, the app may still be restarting, or failing to start. Check its log stream in the Azure portal, or run `az webapp log tail --name APPNAME --resource-group rg-APPNAME`. If `/health` returns status 503 with `unhealthy`, the deployed app found no lessons.
