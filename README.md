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

## Prerequisites

You need a GitHub account. The free plan is enough for the required labs. Optional labs that need more, such as an Azure subscription or a Copilot plan, say so at the top.

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
