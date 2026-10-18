---
title: "Lab 1: Build the application"
summary: "Run the workflow that builds the workshop website, follow its steps, and download the app it publishes."
duration: 10
difficulty: Beginner
order: 1
---

# Lab 1: Build the application

Your repository already has a workflow that builds the workshop website, which displays these lessons. You'll start it from the **Actions** tab, follow it while it runs, and download the published app it saves.

## Before you start

You need the repository you created in [Lab 0](00-create-repository.md). Everything in this lab happens in your browser.

## Run the workflow

1. In your repository, select the **Actions** tab.
2. In the left sidebar, select **Build application**.

   **Build application** is a workflow: an automated process that GitHub runs from a YAML file in the `.github/workflows` folder. This one is defined in `.github/workflows/build.yml`.

3. Select **Run workflow**. In the menu that opens, leave the branch set to `main`, then select the green **Run workflow** button.

After a few seconds, a new run appears at the top of the list. If it doesn't, refresh the page. A run is one execution of a workflow, so you get a new run each time you start it.

## Follow the run

1. Select the new run. GitHub opens its **Summary**, which says the run was **Manually triggered**.
2. Under **Jobs** in the left sidebar, select **Build and publish**.

   A job is a list of steps that run on the same machine, called a runner. This job runs on a GitHub-hosted runner: a new Ubuntu virtual machine that GitHub creates when the job starts and removes when the job ends.

3. Watch the steps run. Select a step to expand its log.

   Each step either runs a command or uses an action. An action is a published piece of code that a step can run. For example, **Check out the repository** uses the `actions/checkout` action to copy your repository's files onto the runner.

The run usually takes less than a minute. It can wait in a queue for a few seconds before a runner picks it up.

## Check the result

When the run finishes, **Build and publish** and each of its steps have a green check mark, and the run's **Summary** shows the status **Success**.

The log also has steps that aren't in the workflow file:

- **Set up job** is where GitHub prepares the runner. Expand it to see which runner image the job used, and under **GITHUB_TOKEN Permissions**, what the run was allowed to do in your repository.
- **Post Set up .NET** and **Post Check out the repository** are cleanup steps that those two actions run after the other steps.
- **Complete job** is where GitHub cleans up after the job.

The **Summary** can also show annotations. A notice, such as one about an upcoming change to a runner image, is information only. It doesn't mean the run failed.

## Download the artifact

1. Select **Summary** in the left sidebar.
2. Under **Artifacts**, select `workshop-dashboard`. Your browser downloads `workshop-dashboard.zip`.

An artifact is a file or folder that a workflow saves before its runner is removed. This one holds the published website: `WorkshopDashboard.dll`, the libraries it needs, the styles and scripts in `wwwroot`, and the lessons you're reading. GitHub deletes it after 7 days, because the workflow sets `retention-days: 7`.

The published app is a build artifact, not a standalone program, so it only runs on a machine that has [the ASP.NET Core Runtime for .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0). If you want to try it, unzip the file, open a terminal in that folder, run `dotnet WorkshopDashboard.dll --urls http://localhost:5080`, and open `http://localhost:5080` in your browser. You don't need to run it to finish this lab.

## What happened

Here's `.github/workflows/build.yml`, the file you just ran. The workflow, job, and step names on the run page come from it.

```yaml source=.github/workflows/build.yml
name: Build application

on:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  build:
    name: Build and publish
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - name: Check out the repository
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1

      - name: Set up .NET
        uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json

      - name: Restore dependencies
        run: dotnet restore src/WorkshopDashboard/WorkshopDashboard.csproj

      - name: Publish the dashboard
        run: dotnet publish src/WorkshopDashboard/WorkshopDashboard.csproj --configuration Release --no-restore --output publish

      - name: Upload the published app
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: workshop-dashboard
          path: publish
          if-no-files-found: error
          retention-days: 7
```

- `on: workflow_dispatch` is the trigger. It adds the **Run workflow** button, and because it's the only trigger, the workflow runs only when someone starts it. That's why nothing ran when you created your repository.
- `permissions` limits the `GITHUB_TOKEN`, a token GitHub creates for each job, to reading the repository's contents. That's what you saw under **GITHUB_TOKEN Permissions**.
- `jobs` holds one job, with the ID `build` and the name **Build and publish**.
- `runs-on: ubuntu-latest` asks for a GitHub-hosted Ubuntu runner. `timeout-minutes: 10` cancels the job if it's still running after 10 minutes, instead of the default 6 hours.
- Each item under `steps` is one step, and its `name` is the label you saw in the log.
- `uses` runs an action. The long string after `@` pins the action to one exact commit, and the comment says which release that commit is. A tag such as `v7.0.1` can be moved to other code later, but a commit SHA can't.
- `with` passes inputs to an action. `setup-dotnet` installs a .NET SDK that matches `global.json`, and `upload-artifact` saves the `publish` folder as `workshop-dashboard`.
- `run` runs a shell command on the runner. `dotnet restore` downloads the NuGet packages the app depends on, and `dotnet publish` compiles the app and copies everything it needs into the `publish` folder.

This workflow doesn't run the unit tests. You'll add a workflow for that in the next lab.

## If something fails

If there's no **Run workflow** button, select **Build application** in the left sidebar first. If the button still doesn't appear, check the repository name at the top of the page. The button only appears for people who can change the repository, so you won't see it on `sabajamalian/ghaction-workshop` itself.

If the run stays queued for several minutes, check [GitHub Status](https://www.githubstatus.com) for an incident affecting GitHub Actions.

If the run fails with an error saying an action isn't allowed, your copy belongs to an organization that limits which actions can run. Create a copy under your personal account, as described in [Lab 0](00-create-repository.md).

If **Set up .NET** fails while downloading the SDK, a temporary network problem probably interrupted the download. On the run page, select **Re-run jobs**, then **Re-run failed jobs**.

## Next step

In [Lab 2: Run tests on pull requests](02-test-pull-request.md), you'll write your own workflow that runs the unit tests on every pull request.
