# For maintainers

This folder holds the files that only the people who maintain the workshop need. If you're working through the labs, you can ignore it. Learners' copies of the template include it, but no lab uses it, nothing in it runs on its own, and coding agents don't load it as instructions.

| File | What it's for |
| --- | --- |
| [AGENTS.md](AGENTS.md) | The rules for changing the repository, for people and coding agents. |
| [editorial-guide.md](editorial-guide.md) | The full writing rules and lesson format, and what the content tests check. |
| [editorial-checklist.md](editorial-checklist.md) | The review checklist for any change people read. |
| [architecture.md](architecture.md) | How the dashboard works: settings, rendering, security headers, and tests. |
| [deployment.md](deployment.md) | How to create the workshop site's Azure resources and deploy it by hand. |
| `scripts/site-setup.sh` | Creates the site's resource group, App Service plan, and web app. |
| `scripts/deploy-site.sh` | Tests, publishes, and deploys the site from your checkout. |

[CONTRIBUTING.md](../CONTRIBUTING.md) at the root of the repository covers building, testing, updating pinned actions, and opening a pull request.

## Load the agent instructions

Copilot CLI loads `AGENTS.md` automatically only from the repository root and the folder you start it in. To have it follow `maintainers/AGENTS.md` while you work anywhere in the repository, add the folder to its instruction folders when you start it from the repository root:

```bash
COPILOT_CUSTOM_INSTRUCTIONS_DIRS="$PWD/maintainers" copilot
```

## Deploy the site

```bash
bash maintainers/scripts/deploy-site.sh --app-name APPNAME --resource-group rg-APPNAME
```

[Deploy the workshop site](deployment.md) covers the first-time setup, the permissions you need, and what to do if the deploy fails.
