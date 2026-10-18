# Run the workshop for a group

Learners can work through this workshop on their own, so you don't need to present anything. This guide covers what to check before you run it for a group, such as a team or a class, and what learners usually get stuck on.

## Before the session

### Decide where learners' copies live

In Lab 0, each learner creates their own copy of the workshop with **Use this template**, and every workflow runs in that copy.

Personal accounts are the simplest choice. Learners don't need anything from you, and a public copy runs on GitHub-hosted runners for free. That's why Lab 0 suggests **Public** to learners who aren't sure.

If the copies have to be in your organization, check these organization settings first:

- Members can create repositories. In the organization's **Settings**, find the "Access" section of the sidebar, select **Member privileges**, and check "Repository creation".
- GitHub Actions can run the actions the labs use. In **Settings**, select **Actions**, then **General**, and check "Policies". If the policy limits actions, the labs need the actions created by GitHub and the ones listed in [Actions the labs use](#actions-the-labs-use).
- Your organization pays for the minutes. Private repositories use the organization's included minutes, so plan for them as described in [Plan for Actions minutes](#plan-for-actions-minutes).

Every action in the workshop is pinned to a full commit SHA, so **Require actions to be pinned to a full-length commit SHA** doesn't block anything.

### Plan for Actions minutes

Runs on standard GitHub-hosted runners are free in public repositories. In private repositories, they use the included minutes of the account that owns the repository, not the learner who started the run. GitHub Free and GitHub Free for organizations include 2,000 minutes a month, and GitHub rounds each job up to a whole minute. If the account has no payment method, GitHub blocks runs in private repositories once the minutes run out, until they reset at the start of the next billing cycle.

The required labs use about 4 minutes per learner in a private copy: one build in Lab 1 and three test runs in Lab 2. Each run takes under a minute.

The optional labs use more, and [Prerequisites](prerequisites.md#what-each-lab-needs) lists the minutes each one uses in a private copy. Most use a few. Lab 3 uses about 16 per run, because included minutes are used up at each runner's price, and a macOS minute costs about 10 times as much as a Linux minute. So if 24 learners run Lab 3 once in private copies that your organization owns, they use about 380 of its minutes. Lab 3 tells learners how to leave the macOS job out. [Actions runner pricing](https://docs.github.com/en/billing/reference/actions-runner-pricing) lists the price of each runner.

### Actions the labs use

The required labs use only actions created by GitHub: `actions/checkout`, `actions/setup-dotnet`, and `actions/upload-artifact`. If your organization allows only select actions, select **Allow actions created by GitHub**. The optional labs use more actions from GitHub's `actions` organization, such as `actions/cache` in Lab 5, and that setting allows them too.

Labs 7 and 8 also use `azure/login` and `azure/webapps-deploy`. To allow them, add this to **Allow or block specified actions and reusable workflows**:

```text
azure/login@*, azure/webapps-deploy@*
```

### Set up Azure for Labs 7 and 8

Labs 7 and 8 deploy each learner's copy to Azure App Service. Learners with their own Azure subscription set it up themselves in Lab 7. To let learners do these labs without Azure access, set up Azure for them with `scripts/azure/instructor-setup.sh`. Run it after learners have created their copies in Lab 0: each learner's identity trusts one repository by its ID, and a repository has no ID until it exists.

```bash
bash scripts/azure/instructor-setup.sh --learners learners.txt --prefix PREFIX
```

`learners.txt` lists the learners' repositories, one `OWNER/REPO` per line. If a learner's repository is private, add their subject prefix after the name. [Get values from your instructor](../lessons/explore/azure-oidc.md#get-values-from-your-instructor) in Lab 7 asks learners to send it. The script writes each learner's four repository variables and site address to `PREFIX-azure-values.csv`. Send each learner their row, and they skip the Azure setup in Lab 7. With the default B1 plans, a class of 24 costs about $1.22 a day in East US at October 2026 prices, until you delete the resource group. [scripts/azure/README.md](../scripts/azure/README.md) covers the permissions you need, costs, and cleanup.

Every learner gets your tenant ID and subscription ID. Neither grants access on its own, but Lab 7 stores them as repository variables, which appear in workflow logs that anyone can read in a public repository. If you'd rather keep them private, ask learners to store the three IDs as secrets instead, as [Add the repository variables](../lessons/explore/azure-oidc.md#add-the-repository-variables) in Lab 7 describes.

### Plan machines for Lab 9

Lab 9 needs more than a browser. Each learner creates a new private practice repository, registers a self-hosted runner with it on a Linux machine they can delete, and removes the runner and the machine at the end. Most learners use a codespace created from the practice repository, which runs in the browser. The jobs run on that machine, so the lab uses no Actions minutes. The codespace counts against the free monthly Codespaces quota of whoever pays for it: an hour on a 2-core codespace uses 2 of the 120 compute hours that GitHub Free includes for personal accounts, and the codespace uses storage quota for as long as it exists, even while it's stopped.

Personal accounts are the simplest place for the practice repositories, and that's what the lab describes. If they have to be in your organization, check these first:

- Repositories can have their own self-hosted runners. In the organization's **Settings**, select **Actions**, then **General**, and check that the "Runners" menu is set to **All repositories**, or to **Selected repositories** with the practice repositories selected.
- Learners can create codespaces from the practice repositories, and you know who pays. Organizations on GitHub Free can't pay for Codespaces, so the learner always does. Organization plans include no free quota, so if your organization pays, a 2-core codespace costs $0.18 for each hour it runs, plus $0.07 per GB of storage per month for as long as it exists. See [Choosing who owns and pays for codespaces in your organization](https://docs.github.com/en/codespaces/managing-codespaces-for-your-organization/choosing-who-owns-and-pays-for-codespaces-in-your-organization).

Tell learners not to use their everyday computer or a work laptop for this lab. Every job the runner picks up can run any command as the runner's user.

### Try the required labs yourself

Create a fresh copy and work through Labs 0 to 2 the way a learner would. It takes about 35 minutes, and it shows you the pages learners will see, including the test failure that Lab 2 causes on purpose.

## During the session

Send learners to the lesson list in the [README](../README.md). Each lab says what it needs at the top, and the required labs need only a browser and a GitHub account.

Plan about 35 minutes for Labs 0 to 2, plus time for questions. The optional labs can be done in any order, and each one lists its time at the top.

When someone falls behind, the [solutions](../solutions/README.md) folder has the finished workflow for each lab, with steps for copying one into a repository. For problems, start with the lab's "If something fails" section, then [troubleshooting](troubleshooting.md).
