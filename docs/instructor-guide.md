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

Labs 0 to 2 use about 4 minutes per learner in a private copy: one build in Lab 1 and three test runs in Lab 2. Each run takes under a minute.

The other labs use more, and [Prerequisites](prerequisites.md#what-each-lab-needs) lists the minutes each one uses in a private copy. Most use a few. Lab 3 uses about 16 per run, because included minutes are used up at each runner's price, and a macOS minute costs about 10 times as much as a Linux minute. So if 24 learners run Lab 3 once in private copies that your organization owns, they use about 380 of its minutes. Lab 3 tells learners how to leave the macOS job out. [Actions runner pricing](https://docs.github.com/en/billing/reference/actions-runner-pricing) lists the price of each runner.

### Actions the labs use

Labs 0 to 2 use only actions created by GitHub: `actions/checkout`, `actions/setup-dotnet`, and `actions/upload-artifact`. If your organization allows only select actions, select **Allow actions created by GitHub**. The other labs use more actions from GitHub's `actions` and `github` organizations, such as `actions/cache` in Lab 5 and `github/gh-aw-actions/setup` in Lab 11, and that setting allows them too.

Labs 7 and 8 also use `azure/login` and `azure/webapps-deploy`, Lab 15 uses `zizmorcore/zizmor-action`, and the Lab 18 demo uses `azure/login`. To allow them, add this to **Allow or block specified actions and reusable workflows**:

```text
azure/login@*, azure/webapps-deploy@*, zizmorcore/zizmor-action@*
```

A few labs use more than actions. Lab 13 runs actionlint from its Docker Hub image with `uses: docker://rhysd/actionlint`, and Lab 17 starts a PostgreSQL container from Docker Hub as a service container, so runners need to reach Docker Hub. GitHub-hosted runners can. Lab 16 has learners write an action in their own copy and run it with `uses: $/.github/actions/count-lessons`, which GitHub Enterprise Server doesn't support.

### Check Copilot access for Labs 6 and 11

Labs 6 and 11 run GitHub Copilot CLI in a workflow. What they need depends on who owns the learner's copy:

- In a learner's personal account, the learner needs a Copilot plan, and the runs use that plan's AI credits.
- In your organization, Copilot CLI has to be turned on, along with the **Allow use of Copilot CLI billed to the organization** policy, which is on by default. The organization pays for the runs.

One test run of Lab 6 used about 4 AI credits, or about $0.04. A run of Lab 11 used about 30, or about $0.30. [Using Copilot CLI in GitHub Actions with GITHUB_TOKEN](https://docs.github.com/en/copilot/how-tos/copilot-cli/use-copilot-cli-in-actions) explains the policy.

Lab 11 uses GitHub Agentic Workflows, which is in public preview and releases a new version every week or two. Learners compile the workflow in a terminal with Git and GitHub CLI, or use the compiled solution, which stops working if the gh-aw version that compiled it is blocked or falls below the minimum supported version. Try Lab 11 yourself shortly before the session.

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

### Prepare for Lab 12

Lab 12 migrates an Azure Pipelines file that's in the repository, so neither you nor the learners need an Azure DevOps organization. Parts A, B, and D happen in the browser.

The optional Part C runs GitHub Actions Importer, which needs a terminal with Docker and GitHub CLI. It downloads a container image that takes about 630 MB of disk space, so on a slow or shared network, ask learners to install it before the session, as [Install the importer](../lessons/explore/azure-devops-migration.md#install-the-importer) describes. Learners without Docker can use a codespace created from their copy, which counts against the Codespaces quota in the same way as in Lab 9. On learners' own computers, Docker Desktop is free for businesses with fewer than 250 employees and less than $10 million in annual revenue. Larger organizations need a paid subscription.

The importer asks for an Azure DevOps organization, a project, and two tokens even when it reads a file. The lab gives placeholder values for all four, so tell learners not to enter real tokens.

### Prepare the Lab 18 demo

Lab 18 is a demo: you run it, and learners watch. It asks Microsoft-Decision-1 in Microsoft Foundry whether a pull request's changes break each of a set of coding standards, and fails the check when the model is at least 80% sure a rule marked `block` is broken. Learners don't need Azure or Foundry access.

Set it up in a repository you'll show on screen, such as your own copy of the workshop, at least a day before the session:

1. Deploy Microsoft-Decision-1 to a Microsoft Foundry resource, and note the deployment name and the resource's endpoint.
2. Create the `id-standards` identity, its federated credential for `pull_request`, and its **Cognitive Services User** role assignment, as [Create an identity for the check](../lessons/explore/foundry-standards.md#create-an-identity-for-the-check) in Lab 18 describes. Role assignments can take a few minutes to apply.
3. Add the repository variables from [Add the repository variables](../lessons/explore/foundry-standards.md#add-the-repository-variables).
4. Add `.github/workflows/coding-standards-check.yml` to `main`.
5. Run the demo once, from a branch you then delete. The first run shows whether sign-in and the model call work, and it gives you a finished run to fall back on if the network fails during the session.

Each run is one call to the decision API, with one question for each rule, billed by Microsoft Foundry. The input is mostly the diff, and the job summary shows how many input tokens the call used. The check skips pull requests from forks, because they can't get an OIDC token from your repository.

### Try Labs 0 to 2 yourself

Create a fresh copy and work through Labs 0 to 2 the way a learner would. It takes about 35 minutes, and it shows you the pages learners will see, including the test failure that Lab 2 causes on purpose.

## During the session

Send learners to the lesson list in the [README](../README.md). Labs 0 to 2 are the foundation labs: everyone does them first, in order, and they need only a browser and a GitHub account. Each of the other labs says what it needs at the top.

Plan about 35 minutes for Labs 0 to 2, plus time for questions. Make sure everyone finishes them before moving on: every other lab works in the repository from Lab 0 and assumes the workflow basics from Labs 1 and 2. After the foundation labs, learners can do the explore labs in any order, and each one lists its time at the top.

If you have time for one more lab as a group, Lab 13, which debugs a broken workflow, and Lab 15, which secures workflows, apply to any team that uses GitHub Actions. Run the Lab 18 demo after Lab 15, because it builds on its points about tokens, secrets, and pull request triggers.

When someone falls behind, the [solutions](../solutions/README.md) folder has the finished workflow for each lab, with steps for copying one into a repository. For problems, start with the lab's "If something fails" section, then [troubleshooting](troubleshooting.md).
