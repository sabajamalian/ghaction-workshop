# Troubleshooting

Each lab ends with an "If something fails" section for the problems that lab can cause. This page covers problems that can happen in any lab, and problems running the workshop site on your own computer.

## Problems in any lab

If there's no **Run workflow** button, check two things. The workflow file has to be on your repository's default branch, usually `main`, and it has to have `workflow_dispatch` in its `on:` section. You also need write access to the repository, so the button doesn't appear on `sabajamalian/ghaction-workshop` itself. Open your own copy instead.

If a failed run named after the workflow's file, such as `.github/workflows/tests.yml`, appears right after you commit the file, GitHub couldn't read the workflow file. Open that run. Under **Annotations**, an **Invalid workflow file** error names the line with the problem. The usual cause is indentation that changed when you pasted the YAML. Compare your file with the lab's solution in [the solutions folder](../solutions/README.md).

If a workflow you added never runs, check its path. GitHub only runs workflow files in the `.github/workflows` folder of the repository, with names that end in `.yml` or `.yaml`.

If a run shows the notice "The ubuntu-latest label will migrate to Ubuntu 26 beginning October 19, 2026", you don't need to change anything. It's information, not an error. The workshop's build and the Lab 2 tests also pass on Ubuntu 26.04.

If a run stays queued for several minutes, check [GitHub Status](https://www.githubstatus.com) for an incident affecting GitHub Actions.

If GitHub stops running jobs in a private copy, your account may have used the Actions minutes its plan includes for the month. GitHub Free includes 2,000 minutes a month for private repositories. Without a payment method, GitHub blocks Actions once they're used up, and with one, a budget you've set can block it too. Check your usage on the [billing overview page](https://github.com/settings/billing). Jobs on standard GitHub-hosted runners are free in public repositories, so changing your copy to public in its **Settings** also works.

If a run fails because an action isn't allowed, your copy belongs to an organization whose policy limits which actions can run. Ask an organization owner to allow the actions the lab uses, or create a copy under your personal account. [Run the workshop for a group](instructor-guide.md#actions-the-labs-use) lists the actions.

## Problems in a specific lab

| Lab | Troubleshooting |
| --- | --- |
| [Lab 0: Create your repository](../lessons/foundation/00-create-repository.md) | [If something fails](../lessons/foundation/00-create-repository.md#if-something-fails) |
| [Lab 1: Build the application](../lessons/foundation/01-build-application.md) | [If something fails](../lessons/foundation/01-build-application.md#if-something-fails) |
| [Lab 2: Run tests on pull requests](../lessons/foundation/02-test-pull-request.md) | [If something fails](../lessons/foundation/02-test-pull-request.md#if-something-fails) |
| [Lab 3: Compare runners](../lessons/explore/runners.md) | [If something fails](../lessons/explore/runners.md#if-something-fails) |
| [Lab 4: Trigger workflows and pass inputs](../lessons/explore/triggers-and-inputs.md) | [If something fails](../lessons/explore/triggers-and-inputs.md#if-something-fails) |
| [Lab 5: Pass files between jobs](../lessons/explore/jobs-and-artifacts.md) | [If something fails](../lessons/explore/jobs-and-artifacts.md#if-something-fails) |
| [Lab 7: Deploy to Azure with OIDC](../lessons/explore/azure-oidc.md) | [If something fails](../lessons/explore/azure-oidc.md#if-something-fails) |
| [Lab 8: Add a staging environment](../lessons/explore/environments.md) | [If something fails](../lessons/explore/environments.md#if-something-fails) |
| [Lab 9: Run jobs on a self-hosted runner](../lessons/explore/self-hosted-runners.md) | [If something fails](../lessons/explore/self-hosted-runners.md#if-something-fails) |
| [Lab 10: Reuse workflows and steps](../lessons/explore/reusable-workflows.md) | [If something fails](../lessons/explore/reusable-workflows.md#if-something-fails) |

## Problems running the site on your computer

If `dotnet run` fails with "A compatible .NET SDK was not found", you don't have a .NET 10 SDK. The repository's `global.json` asks for SDK 10.0.100 or a later .NET 10 SDK. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then run the command again.

If the site starts but lists no lessons, the log says "No lessons were loaded from" and names the folder it read. Check the `Workshop:LessonsPath` setting, described in [How the workshop site works](architecture.md#settings). When you run the site with `dotnet run`, it reads the `lessons` folder in the repository.

If a lesson is missing from the list, the log has a "Skipped lesson" warning that names the file and the problem, usually invalid front matter. The [editorial guide](editorial-guide.md#lesson-files) shows the format.

If a lesson change doesn't show up, check which environment the site runs in. `dotnet run` uses Development, which reads the lesson files again on every request, so refreshing the page is enough. Any other environment reads them once at startup, so restart the site.
