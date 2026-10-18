# Prerequisites

The required labs, 0 to 2, need a GitHub account and a web browser. GitHub Free is enough, and you don't install anything. This page lists what each optional lab needs on top of that, so you can check before you start. Each lab's **Before you start** section has the details.

## What each lab needs

Every lab except Lab 9 runs in the copy of the repository you create in [Lab 0](../lessons/foundation/00-create-repository.md), and you do it in your browser unless the table says otherwise. The last column is for a private copy. In a public copy, the runs are free.

| Lab | You need | Included Actions minutes |
| --- | --- | --- |
| [Labs 0 to 2](../README.md#start-the-workshop) | A GitHub account | About 4 in total |
| [Lab 3: Compare runners](../lessons/explore/runners.md) | Nothing extra | About 16 per run, 10 of them for the macOS job |
| [Lab 4: Trigger workflows and pass inputs](../lessons/explore/triggers-and-inputs.md) | Nothing extra | About 4, or 5 with the optional part |
| [Lab 5: Pass files between jobs](../lessons/explore/jobs-and-artifacts.md) | Nothing extra | About 8 in total |
| [Lab 7: Deploy to Azure with OIDC](../lessons/explore/azure-oidc.md) | An Azure subscription where you have the Owner role, or Contributor plus User Access Administrator or Role Based Access Control Administrator. For the setup, Azure Cloud Shell, or a terminal with Bash, Git, and Azure CLI 2.58.0 or later. If your instructor set up Azure for your class, only the four values they send you. | About 2 per run |
| [Lab 8: Add a staging environment](../lessons/explore/environments.md) | Owner or admin access to your copy, and a public copy or GitHub Enterprise Cloud for every step. The last section also needs Lab 7. | Up to 2 per run of the practice workflow |
| [Lab 9: Run jobs on a self-hosted runner](../lessons/explore/self-hosted-runners.md) | A new private repository, separate from your copy. A codespace or a Linux virtual machine you can delete afterward, and a terminal on it. | None, because the jobs run on your machine |
| [Lab 10: Reuse workflows and steps](../lessons/explore/reusable-workflows.md) | Nothing extra | About 3 per run |

## Public or private copy

Runs on standard GitHub-hosted runners are free in public repositories. In a private repository, they use the included minutes of the account that owns it: 2,000 a month on GitHub Free and 3,000 on GitHub Pro. GitHub rounds each job up to a whole minute, and a minute on Windows or macOS uses more included minutes than one on Linux. See [Actions runner pricing](https://docs.github.com/en/billing/reference/actions-runner-pricing).

Visibility also decides what Lab 8 can configure. A private copy on GitHub Free can't configure environments, and one on GitHub Pro or Team can't require reviewers or use a wait timer. Lab 8 lists each feature by plan and has a workaround for private copies.

## Costs outside GitHub Actions

Labs 7 and 8 use Azure. Lab 7's setup uses App Service's Free (F1) tier, and managed identities cost nothing, so the Azure side is free. If your instructor set up Azure for your class, they pay for it.

Lab 9 runs its jobs on a machine you provide. A codespace uses your free monthly Codespaces quota: an hour on a 2-core codespace uses 2 of the 120 compute hours GitHub Free includes, or 2 of the 180 on GitHub Pro. A codespace also uses storage quota for as long as it exists, even while it's stopped. A cloud virtual machine is billed until you delete it. See [GitHub Codespaces billing](https://docs.github.com/en/billing/concepts/product-billing/github-codespaces).
