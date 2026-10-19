# Prerequisites

Labs 0 to 2 are the foundation labs. Everyone does them first, in order, and they need only a GitHub account and a web browser. GitHub Free is enough, and you don't install anything. This page lists what each of the other labs needs on top of that, so you can check before you start. Each lab's **Before you start** section has the details.

## What each lab needs

Every lab except Lab 9 runs in the copy of the repository you create in [Lab 0](../lessons/foundation/00-create-repository.md), and you do it in your browser unless the table says otherwise. The last column is for a private copy. In a public copy, the runs are free.

| Lab | You need | Included Actions minutes |
| --- | --- | --- |
| [Labs 0 to 2, the foundation labs](../README.md#foundation-labs-required) | A GitHub account | About 4 in total |
| [Lab 3: Compare runners](../lessons/explore/runners.md) | Nothing extra | About 16 per run, 10 of them for the macOS job |
| [Lab 4: Trigger workflows and pass inputs](../lessons/explore/triggers-and-inputs.md) | Nothing extra | About 4, or 5 with the optional part |
| [Lab 5: Pass files between jobs](../lessons/explore/jobs-and-artifacts.md) | Nothing extra | About 8 in total |
| [Lab 6: Generate a report with Copilot CLI](../lessons/explore/copilot-cli.md) | A GitHub Copilot plan. If an organization owns your copy, its **Allow use of Copilot CLI billed to the organization** policy instead. | About 1 per run |
| [Lab 7: Deploy to Azure with OIDC](../lessons/explore/azure-oidc.md) | An Azure subscription where you have the Owner role, or Contributor plus User Access Administrator or Role Based Access Control Administrator. For the setup, Azure Cloud Shell, or a terminal with Bash, Git, and Azure CLI 2.58.0 or later. If your instructor set up Azure for your class, only the four values they send you. | About 2 per run |
| [Lab 8: Add a staging environment](../lessons/explore/environments.md) | Owner or admin access to your copy, and a public copy or GitHub Enterprise Cloud for every step. The last section also needs Lab 7. | Up to 2 per run of the practice workflow |
| [Lab 9: Run jobs on a self-hosted runner](../lessons/explore/self-hosted-runners.md) | A new private repository, separate from your copy. A codespace or a Linux virtual machine you can delete afterward, and a terminal on it. | None, because the jobs run on your machine |
| [Lab 10: Reuse workflows and steps](../lessons/explore/reusable-workflows.md) | Nothing extra | About 3 per run |
| [Lab 11: Review CI failures with an agentic workflow](../lessons/explore/agentic-workflows.md) | A GitHub Copilot plan. If an organization owns your copy, its **Allow use of Copilot CLI billed to the organization** policy instead. To compile the workflow, a terminal with Git and GitHub CLI 2.0.0 or later, on Linux, macOS, or Windows with WSL. Without one, you can use the compiled solution. | About 5 per run |
| [Lab 12: Migrate an Azure DevOps pipeline](../lessons/explore/azure-devops-migration.md) | Nothing extra for Parts A, B, and D. For the optional Part C, a terminal with Docker and GitHub CLI, on Linux, macOS, or Windows with WSL, or a codespace. You don't need an Azure DevOps organization or token. | About 3 in total |
| [Lab 13: Debug a broken workflow](../lessons/explore/debug-workflows.md) | Nothing extra | About 25 in total, because most runs have a Windows job |
| [Lab 14: Automate issues and pull requests](../lessons/explore/automate-issues-prs.md) | Issues turned on in your copy, which they are by default | About 10 in total |
| [Lab 15: Secure your workflows](../lessons/explore/secure-workflows.md) | Nothing extra | About 5 in total |
| [Lab 16: Build your own action](../lessons/explore/custom-action.md) | Nothing extra | About 3 in total |
| [Lab 17: Run integration tests with a service container](../lessons/explore/service-containers.md) | Nothing extra | About 6 in total |
| [Lab 18: Check coding standards with a Microsoft Foundry model](../lessons/explore/foundry-standards.md) | Nothing, because it's an instructor demo. To run it yourself, a Microsoft Foundry resource with the Microsoft decision model deployed, and an Azure subscription where you can create a managed identity and assign it a role on that resource. | About 1 per pull request |

## Public or private copy

Runs on standard GitHub-hosted runners are free in public repositories. In a private repository, they use the included minutes of the account that owns it: 2,000 a month on GitHub Free and 3,000 on GitHub Pro. GitHub rounds each job up to a whole minute, and a minute on Windows or macOS uses more included minutes than one on Linux. See [Actions runner pricing](https://docs.github.com/en/billing/reference/actions-runner-pricing).

Visibility also decides what Lab 8 can configure. A private copy on GitHub Free can't configure environments, and one on GitHub Pro or Team can't require reviewers or use a wait timer. Lab 8 lists each feature by plan and has a workaround for private copies.

## Costs outside GitHub Actions

Labs 6 and 11 use Copilot, which measures its work in AI credits worth $0.01 each. One test run of Lab 6 used about 4. A run of Lab 11 used about 30, because its agent reads the logs of several failed runs, and a second Copilot CLI session checks the agent's output. The credits come out of your Copilot plan's monthly allowance, or the organization pays if it owns your copy.

Labs 7 and 8 use Azure. Lab 7's setup uses App Service's Free (F1) tier, and managed identities cost nothing, so the Azure side is free. If your instructor set up Azure for your class, they pay for it.

Lab 18 is an instructor demo, so it costs you nothing. If you run it yourself, Microsoft Foundry bills each model call by the tokens it sends and receives, which depends mostly on the size of the pull request's diff. The managed identity is free. See [Microsoft Foundry pricing](https://azure.microsoft.com/pricing/details/microsoft-foundry/).

Lab 9 runs its jobs on a machine you provide. A codespace uses your free monthly Codespaces quota: an hour on a 2-core codespace uses 2 of the 120 compute hours GitHub Free includes, or 2 of the 180 on GitHub Pro. A codespace also uses storage quota for as long as it exists, even while it's stopped. A cloud virtual machine is billed until you delete it. See [GitHub Codespaces billing](https://docs.github.com/en/billing/concepts/product-billing/github-codespaces).

Lab 12's optional Part C runs Docker, either in a codespace, which uses the same quota as Lab 9, or on your own computer. Docker Desktop is free for personal use, education, non-commercial open source projects, and businesses with fewer than 250 employees and less than $10 million in annual revenue. Larger organizations need a paid subscription. See [the Docker Desktop license agreement](https://docs.docker.com/subscription-billing/desktop-license/).
