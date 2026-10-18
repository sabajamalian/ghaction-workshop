# GitHub Actions workshop

Learn GitHub Actions by building and testing the website that serves this workshop's lessons.

You'll start by running an existing workflow, then create one that runs tests on pull requests. After that, you can pick optional labs on runners, deployments, Copilot CLI, and other topics.

The required labs run entirely in your browser on GitHub. You don't need to install anything.

<!-- After the first deployment of the workshop site, add a link to it here. -->

## Start the workshop

Each lab is a Markdown file in the `lessons` folder, so you can read it here on GitHub. [Lab 0: Create your repository](lessons/foundation/00-create-repository.md) shows you how to create your own copy of this repository with **Use this template**. You'll run every workflow in that copy.

| Lab | What you'll do | Time |
| --- | --- | --- |
| [Lab 0: Create your repository](lessons/foundation/00-create-repository.md) | Create your own copy of this repository from the template. | 5 minutes |
| [Lab 1: Build the application](lessons/foundation/01-build-application.md) | Run the workflow that builds the website, and download the app it publishes. | 10 minutes |
| [Lab 2: Run tests on pull requests](lessons/foundation/02-test-pull-request.md) | Write a workflow that runs the unit tests on every pull request, then make it fail and pass. | 20 minutes |

## Optional labs

After Lab 2, do any of these in any order. Each one lists anything else it needs at the top. All of them work in your copy from Lab 0, except Lab 9, which uses a separate private practice repository.

| Lab | What you'll do | Time | You need |
| --- | --- | --- | --- |
| [Lab 3: Compare runners](lessons/explore/runners.md) | Run one job on Linux, Windows, and macOS runners at the same time, and compare the machines GitHub gives it. | 20 minutes | Nothing extra |
| [Lab 4: Trigger workflows and pass inputs](lessons/explore/triggers-and-inputs.md) | Add a manual workflow with inputs and conditions, pass the inputs to the shell safely, and see how the `push`, `pull_request`, and `schedule` triggers start runs. | 30 minutes | Nothing extra |
| [Lab 5: Pass files between jobs](lessons/explore/jobs-and-artifacts.md) | Split a build into parallel jobs, pass the published app to a dependent job as an artifact, and cache NuGet packages between runs. | 30 minutes | Nothing extra |
| [Lab 7: Deploy to Azure with OIDC](lessons/explore/azure-oidc.md) | Deploy the workshop website to Azure App Service from a workflow that signs in to Azure with OpenID Connect instead of a stored secret. | 45 minutes | An Azure subscription where you can create resources and assign roles, or Azure values from your instructor |
| [Lab 8: Add a staging environment](lessons/explore/environments.md) | Deploy a practice workflow through a staging environment with its own variables, secrets, branch rules, and approvals. | 40 minutes | A public copy or GitHub Enterprise Cloud for every step, and Lab 7 for the last section |
| [Lab 9: Run jobs on a self-hosted runner](lessons/explore/self-hosted-runners.md) | Register a runner on a disposable machine, run a workflow on it, and remove the runner and its files. | 60 minutes | A new private repository, a codespace or a Linux virtual machine you can delete, and a terminal on it |
| [Lab 10: Reuse workflows and steps](lessons/explore/reusable-workflows.md) | Turn the build job into a reusable workflow, call it twice with different inputs, and move its setup steps into a composite action. | 35 minutes | Nothing extra |

Runs in a public copy are free. In a private copy, they use your account's included Actions minutes. Most labs use a few, Lab 3 uses about 16 per run, because macOS minutes cost more, and Lab 9 uses none, because its jobs run on your own machine.

## Prerequisites

You need a GitHub account. The free plan is enough for the required labs. Optional labs that need more, such as an Azure subscription or a Copilot plan, say so at the top. [Prerequisites](docs/prerequisites.md) lists what every lab needs, in one place.

## Run the website locally

This is for maintainers and contributors. Learners don't need it.

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then run this from the repository root:

```bash
dotnet run --project src/WorkshopDashboard
```

The site runs at `http://localhost:5080`.

## Contributing

Read [the contributing guide](CONTRIBUTING.md) before you open a pull request. It covers building and testing, and links to the editorial guide that lessons follow.

## License

This workshop is available under [the MIT License](LICENSE).
