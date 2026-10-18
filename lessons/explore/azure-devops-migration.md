---
title: "Lab 12: Migrate an Azure DevOps pipeline"
summary: "Convert an Azure Pipelines CI pipeline to a GitHub Actions workflow, repair what the conversion missed, and compare where each platform shows test results and coverage."
duration: 75
difficulty: Advanced
order: 12
requires: "A terminal with Docker and GitHub CLI for the optional Part C."
---

# Lab 12: Migrate an Azure DevOps pipeline

Your repository has an Azure Pipelines file, `migration/azure-devops/azure-pipelines.yml`, that builds the workshop app, runs the tests with Coverlet code coverage, and publishes the test results, a coverage report, and the app. In this lab, you'll read that pipeline, repair a hand-converted GitHub Actions version of it, and compare where each platform shows test results and coverage. In the optional Part C, you'll convert the pipeline with GitHub Actions Importer and fix what it couldn't convert.

## Before you start

You need the repository you created in [Lab 0](../foundation/00-create-repository.md). This lab uses the run, job, and step pages you saw in [Lab 1](../foundation/01-build-application.md) and the tests from [Lab 2](../foundation/02-test-pull-request.md). You don't need an Azure DevOps organization, and nothing is created in Azure DevOps.

Parts A, B, and D happen in your browser and take about 45 minutes. Part C is optional, takes about 30 minutes more, and needs a terminal with Docker and GitHub CLI. [Get a terminal with Docker](#get-a-terminal-with-docker) explains the options.

The workflow you add in Part B runs on every push to `main` and every pull request into `main` until you delete it in [Clean up](#clean-up), including pushes you make for other labs. If your repository is public, the runs are free. If it's private, they use your account's included Actions minutes, and GitHub rounds each job up to a whole minute. Part B starts two runs of about a minute each, or more if you commit the fixes one at a time, and Part C starts one more, so the lab uses about 3 of the 2,000 minutes a month that GitHub Free includes. Each successful run also keeps about 0.5 MB of artifacts for 7 days, which counts toward your account's storage in a private repository (GitHub Free includes 500 MB).

## Part A: Understand the existing pipeline

Select the **Code** tab of your repository, then `migration`, `azure-devops`, and `azure-pipelines.yml`. The pipeline looks like this:

```yaml source=migration/azure-devops/azure-pipelines.yml
# Continuous integration for the workshop app, written for Azure Pipelines.
# Lab 12 converts this pipeline to a GitHub Actions workflow. Nothing in this
# repository runs it, and the lab doesn't need an Azure DevOps organization.

trigger:
  branches:
    include:
      - main

# Pull request validation depends on where the code is hosted:
# - GitHub repository: this pr trigger runs the pipeline for pull requests into main.
# - Azure Repos Git: YAML pr triggers aren't supported. Open the branch policies for
#   main and configure the Build validation policy instead.
pr:
  branches:
    include:
      - main

pool:
  vmImage: ubuntu-latest

variables:
  buildConfiguration: Release

steps:
  - task: UseDotNet@2
    displayName: Install the .NET SDK from global.json
    inputs:
      packageType: sdk
      useGlobalJson: true

  - task: DotNetCoreCLI@2
    displayName: Restore dependencies
    inputs:
      command: restore
      projects: '**/*.csproj'

  - task: DotNetCoreCLI@2
    displayName: Build
    inputs:
      command: build
      projects: '**/*.csproj'
      arguments: --configuration $(buildConfiguration) --no-restore

  # publishTestResults defaults to true. It's off here because the next two tasks
  # publish the test results and the Coverlet coverage report.
  - task: DotNetCoreCLI@2
    displayName: Run the tests with Coverlet
    inputs:
      command: test
      projects: tests/WorkshopCore.Tests/WorkshopCore.Tests.csproj
      arguments: --configuration $(buildConfiguration) --no-build --logger trx --results-directory $(Agent.TempDirectory)/TestResults --collect "XPlat Code Coverage"
      publishTestResults: false

  - task: PublishTestResults@2
    displayName: Publish the test results
    condition: succeededOrFailed()
    inputs:
      testResultsFormat: VSTest
      testResultsFiles: '**/*.trx'
      searchFolder: $(Agent.TempDirectory)/TestResults
      failTaskOnMissingResultsFile: true

  - task: PublishCodeCoverageResults@2
    displayName: Publish the coverage report
    condition: succeededOrFailed()
    inputs:
      summaryFileLocation: $(Agent.TempDirectory)/TestResults/**/coverage.cobertura.xml
      failIfCoverageEmpty: true

  - task: DotNetCoreCLI@2
    displayName: Publish the app
    inputs:
      command: publish
      publishWebProjects: false
      projects: src/WorkshopDashboard/WorkshopDashboard.csproj
      arguments: --configuration $(buildConfiguration) --no-build --output $(Build.ArtifactStagingDirectory)/app
      zipAfterPublish: false
      modifyOutputPath: false

  - task: PublishPipelineArtifact@1
    displayName: Upload the app as a pipeline artifact
    inputs:
      targetPath: $(Build.ArtifactStagingDirectory)/app
      artifact: workshop-dashboard
```

Nothing runs this file. GitHub only runs workflow files in `.github/workflows`, and Azure Pipelines only runs a pipeline that someone has created in an Azure DevOps project. [The README in that folder](../../migration/azure-devops/README.md) explains how the pipeline was checked without one.

### What's in the pipeline

- `trigger` runs the pipeline for each push to `main`.
- `pr` runs it for each pull request into `main`, but only when the code is in GitHub or Bitbucket Cloud. Azure Repos Git doesn't support YAML `pr` triggers. There, a **Build validation** branch policy on `main` runs the pipeline for each pull request, and when the policy is required, the pull request can't be completed until the build succeeds. See [the pr definition](https://learn.microsoft.com/azure/devops/pipelines/yaml-schema/pr?view=azure-pipelines) and [build validation policies](https://learn.microsoft.com/azure/devops/repos/git/branch-policies?view=azure-devops#set-build-validation).
- `pool` picks the agent that runs the job. `vmImage: ubuntu-latest` asks for a Microsoft-hosted Ubuntu agent, a fresh virtual machine that's discarded after the job.
- `variables` sets `buildConfiguration`. Azure Pipelines replaces `$(buildConfiguration)` with its value before a task runs. It also has predefined variables, such as `Agent.TempDirectory`, a folder that's cleaned after each job, and `Build.ArtifactStagingDirectory`, where files are copied before they're published as artifacts. See [the predefined variables](https://learn.microsoft.com/azure/devops/pipelines/build/variables?view=azure-devops).
- `steps` lists the work. Each `task` names a task and its major version after the `@`, `inputs` configures it, and `displayName` is the name the run shows. There's no `checkout` step, because Azure Pipelines checks out the repository when a job has none. See [the task reference](https://learn.microsoft.com/azure/devops/pipelines/tasks/reference/?view=azure-pipelines) and [the checkout step](https://learn.microsoft.com/azure/devops/pipelines/yaml-schema/steps-checkout?view=azure-pipelines).
- `UseDotNet@2` with `useGlobalJson: true` installs the .NET SDK version that `global.json` asks for.
- The first two `DotNetCoreCLI@2` tasks restore and build every project that matches `**/*.csproj`.
- The third runs `dotnet test` on `WorkshopCore.Tests`. `--logger trx` writes the results as a TRX file, the Visual Studio test results format. `--collect "XPlat Code Coverage"` turns on Coverlet, through the `coverlet.collector` package that the test project references, and Coverlet writes `coverage.cobertura.xml` into a folder named with a new GUID. The task's `publishTestResults` input defaults to `true`, which adds its own TRX logger and results folder and publishes the results. It's `false` here because the next two tasks do the publishing.
- `PublishTestResults@2` reads the TRX file and shows the results on the run's **Tests** tab. `VSTest` is the format name for TRX files. `failTaskOnMissingResultsFile: true` fails the task when no file matches. By default, the task doesn't fail.
- `PublishCodeCoverageResults@2` reads the Cobertura file and shows it on the run's **Code Coverage** tab. `failIfCoverageEmpty: true` fails the task when there's no coverage data. Version 1 of the task also needed a `codeCoverageTool` input, and Microsoft says version 1 will be deprecated.
- `condition: succeededOrFailed()` runs both publishing tasks even when the tests fail, so failed results still get published. They're skipped only when the run is canceled.
- The last `DotNetCoreCLI@2` task publishes the app into `Build.ArtifactStagingDirectory`. It sets three task inputs that `dotnet publish` itself doesn't have: `publishWebProjects: false`, because the default `true` ignores `projects` and looks for web projects on its own; `zipAfterPublish: false`, because the default zips the output and deletes the folder; and `modifyOutputPath: false`, because the default adds the project's folder name to the output path.
- `PublishPipelineArtifact@1` uploads that folder as an artifact named `workshop-dashboard`.

### How the concepts map to GitHub Actions

| Azure Pipelines | GitHub Actions |
| --- | --- |
| `trigger` | `on: push` |
| `pr`, or a **Build validation** policy in Azure Repos | `on: pull_request`, plus a required status check in a ruleset to block merging, as [Lab 2](../foundation/02-test-pull-request.md#what-happened) describes |
| `pool` with `vmImage` | `runs-on` with a runner label |
| `variables` and `$(name)` | `env`, read as `$NAME` in a `run` script or `${{ env.NAME }}` in an expression |
| Predefined variables, such as `$(Agent.TempDirectory)` | Contexts and environment variables, such as `${{ runner.temp }}` or `$RUNNER_TEMP` |
| A task, such as `UseDotNet@2` | An action, such as `actions/setup-dotnet` |
| `DotNetCoreCLI@2` | A `run` step with the `dotnet` command |
| `condition: succeededOrFailed()` | `if: ${{ !cancelled() }}` |
| **Tests** and **Code Coverage** tabs | No built-in equivalent. This lab's workflow uploads the files as artifacts and writes a job summary. |
| `PublishPipelineArtifact@1` | `actions/upload-artifact` |

For more of these mappings, see [GitHub's guide to migrating from Azure Pipelines](https://docs.github.com/en/actions/tutorials/migrate-to-github-actions/manual-migrations/migrate-from-azure-pipelines).

## Part B: Review a converted workflow

Someone converted the pipeline to a workflow by hand and left three problems in it. You'll add the workflow to your repository, find what fails, and fix it, all in your browser.

### Add the workflow

1. Select the **Code** tab of your repository, and make sure the branch menu shows `main`.
2. Select **Add file**, then **Create new file**.
3. In the file name box, type `.github/workflows/migrated-ci.yml`.
4. Copy this workflow into the editor. Keep the indentation exactly as it is. It's the file `migration/azure-devops/workflow-to-repair.yml` in your repository.

```yaml source=migration/azure-devops/workflow-to-repair.yml
name: Migrated CI

# A hand-converted version of migration/azure-devops/azure-pipelines.yml with three
# problems left in it. Lab 12 asks you to copy it to .github/workflows/migrated-ci.yml,
# run it, and fix it.

on:
  push:
    branches:
      - main
  pull_request:
    branches:
      - main
  workflow_dispatch:

permissions:
  contents: read

env:
  BUILD_CONFIGURATION: Release

jobs:
  build:
    name: Build, test, and publish
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
        run: dotnet restore GitHubActionsWorkshop.slnx

      - name: Build
        run: dotnet build GitHubActionsWorkshop.slnx --configuration "$BUILD_CONFIGURATION" --no-restore

      - name: Run the tests with Coverlet
        run: dotnet test tests/WorkshopCore.Tests/WorkshopCore.Tests.csproj --configuration "$BUILD_CONFIGURATION" --no-build --logger "trx;LogFileName=test-results.trx" --results-directory TestResults

      - name: Upload the test results
        if: ${{ !cancelled() }}
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: test-results
          path: $(Agent.TempDirectory)/TestResults/*.trx
          if-no-files-found: error
          retention-days: 7

      - name: Upload the coverage report
        if: ${{ !cancelled() }}
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: coverage
          path: TestResults/*/coverage.cobertura.xml
          if-no-files-found: error
          retention-days: 7

      - name: Write the job summary
        if: ${{ !cancelled() }}
        run: |
          coverage_file=$(find TestResults -mindepth 2 -maxdepth 2 -name coverage.cobertura.xml | head -n 1)
          if [ -z "$coverage_file" ]; then
            echo "::error::No coverage.cobertura.xml file in TestResults. Check that the test step collects coverage."
            exit 1
          fi
          attribute() { echo "$1" | grep -o " $2=\"[^\"]*\"" | cut -d '"' -f 2; }
          coverage=$(grep -o -m 1 '<coverage [^>]*>' "$coverage_file")
          counters=$(grep -o -m 1 '<Counters [^>]*>' TestResults/test-results.trx)
          line_rate=$(attribute "$coverage" line-rate)
          {
            echo "## Tests and coverage"
            echo ""
            echo "| Measure | Result |"
            echo "| --- | --- |"
            echo "| Tests passed | $(attribute "$counters" passed) of $(attribute "$counters" total) |"
            echo "| Line coverage | $(awk -v rate="$line_rate" 'BEGIN { printf "%.1f%%", rate * 100 }') |"
            echo "| Lines covered | $(attribute "$coverage" lines-covered) of $(attribute "$coverage" lines-valid) |"
            echo ""
            echo "Download the coverage artifact for the full Cobertura report."
          } >> "$GITHUB_STEP_SUMMARY"

      - name: Publish the app
        run: dotnet publish src/WorkshopDashboard/WorkshopDashboard.csproj --configuration "$BUILD_CONFIGURATION" --no-build --output publish
```

5. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `main` branch, then select **Commit changes**.

The commit is a push to `main`, so it starts a run.

### Find the problems

1. Select the **Actions** tab, then **Migrated CI** in the left sidebar, and select the newest run. It fails in under a minute.
2. Under **Annotations** on the run's summary page, find these errors. Their order may differ.
   - `No files were found with the provided path: $(Agent.TempDirectory)/TestResults/*.trx. No artifacts will be uploaded.` comes from **Upload the test results**.
   - `No files were found with the provided path: TestResults/*/coverage.cobertura.xml. No artifacts will be uploaded.` comes from **Upload the coverage report**.
   - `No coverage.cobertura.xml file in TestResults. Check that the test step collects coverage.` and `Process completed with exit code 1.` come from **Write the job summary**.

   You may also see a notice about the `ubuntu-latest` label. It's for information only and doesn't affect this lab.
3. Select the **Build, test, and publish** job. **Upload the test results**, **Upload the coverage report**, and **Write the job summary** failed, and **Publish the app** was skipped because a step before it failed. The run has no artifacts.
4. Select **Run the tests with Coverlet**. The step passed, and its log has lines like these, where `REPO` is your repository's name:

   ```text
   Results File: /home/runner/work/REPO/REPO/TestResults/test-results.trx

   Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4, Duration: 65 ms - WorkshopCore.Tests.dll (net10.0)
   ```

The tests ran and wrote their TRX file, so the problems are in the other steps. Two of the three problems cause the errors you found. The third causes no error at all, so compare the workflow's steps with the pipeline's tasks in Part A to find it. Try each one before you open its hint.

<details>
<summary>Hint: the coverage report</summary>

The pipeline's test task passes `--collect "XPlat Code Coverage"`, and the workflow's test step doesn't, so Coverlet never ran. Add it to the end of the `run` line, on the same line:

```yaml excerpt=migration/azure-devops/expected-workflow.yml
      - name: Run the tests with Coverlet
        run: dotnet test tests/WorkshopCore.Tests/WorkshopCore.Tests.csproj --configuration "$BUILD_CONFIGURATION" --no-build --logger "trx;LogFileName=test-results.trx" --results-directory TestResults --collect "XPlat Code Coverage"
```

</details>

<details>
<summary>Hint: the test results</summary>

`$(Agent.TempDirectory)` is an Azure Pipelines variable. GitHub Actions passes it to the action as plain text, so the upload looked for a folder named `$(Agent.TempDirectory)`. The test step writes its results to `TestResults` in the repository folder, so that's where the upload has to look:

```yaml excerpt=migration/azure-devops/expected-workflow.yml
      - name: Upload the test results
        if: ${{ !cancelled() }}
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: test-results
          path: TestResults/*.trx
          if-no-files-found: error
          retention-days: 7
```

</details>

<details>
<summary>Hint: the missing step</summary>

The pipeline ends with `PublishPipelineArtifact@1`, which uploads the app. The workflow publishes the app but never uploads it. Add this step at the end of the file, after **Publish the app**, with its `-` lined up with the `-` of the other steps:

```yaml excerpt=migration/azure-devops/expected-workflow.yml
      - name: Upload the published app
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: workshop-dashboard
          path: publish
          if-no-files-found: error
          retention-days: 7
```

</details>

### Fix the workflow

1. Select the **Code** tab, then `.github`, `workflows`, and `migrated-ci.yml`, and select the pencil icon to edit the file.
2. Fix the three problems.
3. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `main` branch, then select **Commit changes**.
4. Select the **Actions** tab, then **Migrated CI**, and open the new run. It succeeds in about a minute.

The log of **Run the tests with Coverlet** now has two more lines after the `Passed!` line. `GUID` stands for an ID that's different in every run:

```text
Attachments:
  /home/runner/work/REPO/REPO/TestResults/GUID/coverage.cobertura.xml
```

The run's summary page has a **Tests and coverage** section with this table:

| Measure | Result |
| --- | --- |
| Tests passed | 4 of 4 |
| Line coverage | 100.0% |
| Lines covered | 2 of 2 |

Under **Artifacts**, it lists `test-results`, `coverage`, and `workshop-dashboard`.

<details>
<summary>Show the finished workflow</summary>

```yaml source=solutions/migration/migrated-ci.yml
name: Migrated CI

# The GitHub Actions version of migration/azure-devops/azure-pipelines.yml (Lab 12).

on:
  push:
    branches:
      - main
  pull_request:
    branches:
      - main
  workflow_dispatch:

permissions:
  contents: read

env:
  BUILD_CONFIGURATION: Release

jobs:
  build:
    name: Build, test, and publish
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
        run: dotnet restore GitHubActionsWorkshop.slnx

      - name: Build
        run: dotnet build GitHubActionsWorkshop.slnx --configuration "$BUILD_CONFIGURATION" --no-restore

      - name: Run the tests with Coverlet
        run: dotnet test tests/WorkshopCore.Tests/WorkshopCore.Tests.csproj --configuration "$BUILD_CONFIGURATION" --no-build --logger "trx;LogFileName=test-results.trx" --results-directory TestResults --collect "XPlat Code Coverage"

      - name: Upload the test results
        if: ${{ !cancelled() }}
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: test-results
          path: TestResults/*.trx
          if-no-files-found: error
          retention-days: 7

      - name: Upload the coverage report
        if: ${{ !cancelled() }}
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: coverage
          path: TestResults/*/coverage.cobertura.xml
          if-no-files-found: error
          retention-days: 7

      - name: Write the job summary
        if: ${{ !cancelled() }}
        run: |
          coverage_file=$(find TestResults -mindepth 2 -maxdepth 2 -name coverage.cobertura.xml | head -n 1)
          if [ -z "$coverage_file" ]; then
            echo "::error::No coverage.cobertura.xml file in TestResults. Check that the test step collects coverage."
            exit 1
          fi
          attribute() { echo "$1" | grep -o " $2=\"[^\"]*\"" | cut -d '"' -f 2; }
          coverage=$(grep -o -m 1 '<coverage [^>]*>' "$coverage_file")
          counters=$(grep -o -m 1 '<Counters [^>]*>' TestResults/test-results.trx)
          line_rate=$(attribute "$coverage" line-rate)
          {
            echo "## Tests and coverage"
            echo ""
            echo "| Measure | Result |"
            echo "| --- | --- |"
            echo "| Tests passed | $(attribute "$counters" passed) of $(attribute "$counters" total) |"
            echo "| Line coverage | $(awk -v rate="$line_rate" 'BEGIN { printf "%.1f%%", rate * 100 }') |"
            echo "| Lines covered | $(attribute "$coverage" lines-covered) of $(attribute "$coverage" lines-valid) |"
            echo ""
            echo "Download the coverage artifact for the full Cobertura report."
          } >> "$GITHUB_STEP_SUMMARY"

      - name: Publish the app
        run: dotnet publish src/WorkshopDashboard/WorkshopDashboard.csproj --configuration "$BUILD_CONFIGURATION" --no-build --output publish

      - name: Upload the published app
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: workshop-dashboard
          path: publish
          if-no-files-found: error
          retention-days: 7
```

</details>

### What's in the finished workflow

- `on` has `push` and `pull_request` for `main`, like `trigger` and `pr`, plus `workflow_dispatch` so you can start a run from the **Actions** tab. To block merging when the check fails, make it a required status check in a ruleset.
- `permissions` gives the job's `GITHUB_TOKEN` read access to the repository's contents and nothing more.
- `env` sets `BUILD_CONFIGURATION`, and the `run` scripts read it as `"$BUILD_CONFIGURATION"`. In a `run` script, `$(...)` is Bash command substitution, and in a `with` value it's plain text, so the Azure Pipelines syntax doesn't carry over.
- `timeout-minutes: 10` stops a stuck job after 10 minutes instead of the default 360.
- `actions/checkout` is a step of its own, because a GitHub Actions job doesn't check out the repository unless a step does.
- `actions/setup-dotnet` with `global-json-file` installs the SDK version from `global.json`, like `useGlobalJson: true`.
- Restore and build use the solution file, `GitHubActionsWorkshop.slnx`, because `dotnet` takes one project or solution, not a wildcard.
- The test step names the TRX file with `LogFileName` and writes its results to `TestResults` in the repository folder. Coverlet puts its file in a GUID folder under that, which is why the coverage upload looks in `TestResults/*/`.
- `if: ${{ !cancelled() }}` runs the two uploads and the job summary even when the tests fail, like `succeededOrFailed()`.
- `if-no-files-found: error` fails an upload that finds no files. The default only warns, so a missing report wouldn't fail the run.
- **Write the job summary** reads the line rate from the Cobertura file and the test counts from the TRX file, and appends a Markdown table to the file that `$GITHUB_STEP_SUMMARY` names. When the coverage file is missing, `echo "::error::..."` creates the error annotation you saw, and `exit 1` fails the step. See [Adding a job summary](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-commands#adding-a-job-summary).
- `dotnet publish` writes the app to `publish` in the repository folder. A `run` step needs none of the three extra publish inputs, because it publishes only the project you name and doesn't zip or rename anything. `--no-build` reuses the build from earlier in the job.
- **Upload the published app** keeps the artifact name `workshop-dashboard`. `retention-days: 7` deletes each artifact after 7 days instead of the default 90.

## Part C: Use GitHub Actions Importer

This part is optional. It takes about 30 minutes and needs a terminal with Docker. To skip it, go to [Part D](#part-d-compare-test-coverage).

GitHub Actions Importer is a GitHub CLI extension that runs its converter in a Docker container. Its `dry-run` command converts a pipeline and writes the workflow to a folder on your computer, without changing anything in GitHub or Azure DevOps. Its `migrate` command converts a pipeline and opens a pull request with the workflow, so this lab doesn't use it.

[GitHub's docs for migrating from Azure DevOps with GitHub Actions Importer](https://docs.github.com/en/actions/tutorials/migrate-to-github-actions/automated-migrations/azure-devops-migration) list these prerequisites:

- An Azure DevOps account or organization with projects and pipelines to convert.
- Access to create an Azure DevOps personal access token.
- An environment that can run Linux-based containers, with Docker installed and running, and GitHub CLI installed.

For a real migration, `gh actions-importer configure` asks for a GitHub personal access token (classic) with the `workflow` scope, an Azure DevOps personal access token with read access to agent pools, builds, code, releases, service connections, task groups, and variable groups, and your organization and project names.

This lab reads the pipeline from your repository with `--source-file-path` instead of fetching it from Azure DevOps. The importer still requires an organization, a project, and both tokens, but with `--source-file-path`, version 1.3.22671 accepted placeholder values for all four. Don't use real tokens for this lab. Reading pipelines from Azure DevOps, and the `audit` and `forecast` commands, need a real Azure DevOps token, and `migrate` needs a real GitHub token to open its pull request.

These steps were tested in October 2026 with GitHub CLI 2.102.0 and Docker Desktop on macOS, and on a GitHub-hosted Ubuntu runner.

### Get a terminal with Docker

You need a Bash or Zsh terminal on Linux, macOS, or Windows with WSL, with Docker and GitHub CLI.

On your own computer, install [GitHub CLI](https://cli.github.com/) and [Docker](https://docs.docker.com/get-started/get-docker/). Docker Desktop is free for personal use, education, non-commercial open source projects, and businesses with fewer than 250 employees and less than $10 million in annual revenue. Larger organizations need a paid subscription. See [the Docker Desktop license agreement](https://docs.docker.com/subscription-billing/desktop-license/).

You can also use a codespace, a development environment in the cloud that you use from your browser. A codespace created from your repository uses GitHub's default image, which includes Docker and GitHub CLI, and GitHub CLI is already signed in with a token that GitHub creates for the codespace. To create one:

1. In your repository, select the **Code** tab, then the **Code** button, then the **Codespaces** tab. A message at the bottom of the dialog says who pays for the codespace.
2. Select the plus button, **Create a codespace on main**. The codespace opens in the browser version of VS Code.
3. If there's no terminal at the bottom of the window, press Ctrl+Shift+P (Shift+Command+P on a Mac), type `View: Toggle Terminal`, and press Enter. The terminal starts in your repository's folder under `/workspaces`.

Personal accounts get a free monthly quota of codespace compute time and storage. Compute time counts per core: GitHub Free includes 120 hours, so an hour on a 2-core codespace uses 2 of them, and GitHub Pro includes 180. Storage counts until you delete the codespace, even while it's stopped. Usage over the quota is billed, or blocked if your account has no payment method. Organization plans don't include a free quota, so if your repository belongs to an organization, check with an owner first. See [GitHub Codespaces billing](https://docs.github.com/en/billing/concepts/product-billing/github-codespaces). When you finish, delete the codespace as described in [Clean up](#clean-up).

Whichever you use, check that Docker is running:

```bash
docker info --format '{{.ServerVersion}}'
```

It prints the Docker server version, such as `28.5.2`. If it prints an error instead, start Docker Desktop or the Docker service, and try again.

### Install the importer

1. Check that GitHub CLI is signed in:

   ```bash
   gh auth status
   ```

   If it says you aren't logged in, run `gh auth login` and follow its prompts.
2. Install the extension, then download the importer's container image, which takes about 630 MB of disk space:

   ```bash
   gh extension install github/gh-actions-importer
   gh actions-importer update
   ```

   If the extension is already installed, the first command says so, and `gh extension upgrade actions-importer` updates it.
3. Check the versions:

   ```bash
   gh actions-importer version
   ```

   The output looks like this. A newer version may convert some parts differently from what this lab shows.

   ```text
   gh version 2.102.0 (2026-09-30)
   gh actions-importer     github/gh-actions-importer      v1.3.6
   actions-importer/cli:latest     v1.3.22671
   ```

### Run the dry run

1. Clone your repository and change to its folder, with your own `OWNER/REPO`:

   ```bash
   gh repo clone OWNER/REPO
   cd REPO
   ```

   If you already have a clone, change to its folder and run `git switch main`, then `git pull`. In a codespace, you're already in the repository's folder.
2. Run the dry run:

   ```bash
   GITHUB_ACCESS_TOKEN=placeholder-token \
   AZURE_DEVOPS_ACCESS_TOKEN=placeholder-token \
   gh actions-importer dry-run azure-devops pipeline \
     --azure-devops-organization placeholder-org \
     --azure-devops-project placeholder-project \
     --source-file-path migration/azure-devops/azure-pipelines.yml \
     --output-dir tmp/dry-run \
     --no-telemetry
   ```

   - The first two lines set placeholder tokens for this command only.
   - `--azure-devops-organization` and `--azure-devops-project` are required. The importer uses them in the output path and the workflow's name.
   - `--source-file-path` reads the pipeline from your clone instead of from Azure DevOps.
   - `--output-dir tmp/dry-run` is where the importer writes its files. The repository ignores `tmp`, so Git won't pick them up.
   - `--no-telemetry` turns off the importer's telemetry.

   The command shows its progress, then prints lines like these, with your own date and time:

   ```text
   [2026-10-18 10:39:24] Logs: 'tmp/dry-run/log/valet-20261018-103924.log'
   [2026-10-18 10:39:24] Output file(s):
   [2026-10-18 10:39:24]   tmp/dry-run/pipelines/placeholder-org/placeholder-project/.github/workflows/placeholder-project.yml
   ```

### Compare the converted workflow with the pipeline

Show the converted workflow:

```bash
cat tmp/dry-run/pipelines/placeholder-org/placeholder-project/.github/workflows/placeholder-project.yml
```

```text
name: placeholder-org/placeholder-project
on:
  pull_request:
    branches:
    - main
env:
  buildConfiguration: Release
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - name: checkout
      uses: actions/checkout@v4.1.0
    - name: Install the .NET SDK from global.json
      uses: actions/setup-dotnet@v4.0.0
    # The dotnet CLI does not accept glob patterns. Consider using a solution file to act on multiple projects at once.
    - name: Restore dependencies
      run: dotnet restore **/*.csproj
    # The dotnet CLI does not accept glob patterns. Consider using a solution file to act on multiple projects at once.
    - name: Build
      run: dotnet build **/*.csproj --configuration ${{ env.buildConfiguration }} --no-restore
    - name: Run the tests with Coverlet
      run: dotnet test tests/WorkshopCore.Tests/WorkshopCore.Tests.csproj --configuration ${{ env.buildConfiguration }} --no-build --logger trx --results-directory ${{ runner.temp }}/TestResults --collect "XPlat Code Coverage"
#     # (VSTest) is an unsupported test runner.
#     # This item has no matching transformer
#     - task: PublishCodeCoverageResults@2
#       displayName: Publish the coverage report
#       condition: succeededOrFailed()
#       inputs:
#         summaryFileLocation: "${{ runner.temp }}/TestResults/**/coverage.cobertura.xml"
#         failIfCoverageEmpty: true
    - name: Publish the app
      run: dotnet publish src/WorkshopDashboard/WorkshopDashboard.csproj --configuration ${{ env.buildConfiguration }} --no-build --output ${{ runner.temp }}/app
    - name: Upload the app as a pipeline artifact
      uses: actions/upload-artifact@v4.1.0
      with:
        name: workshop-dashboard
        path: "${{ runner.temp }}/app"
```

Compare it with the pipeline in Part A. The importer converted these parts automatically:

- `pr` became `on: pull_request` for `main`.
- `pool` became `runs-on: ubuntu-latest`, in a job named `build`.
- `variables` became `env`, and `$(buildConfiguration)` became `${{ env.buildConfiguration }}`.
- It added a `checkout` step, because a GitHub Actions job doesn't check out the repository on its own.
- `UseDotNet@2` became `actions/setup-dotnet`.
- Each `displayName` became a step's `name`.
- Each `DotNetCoreCLI@2` task became a `run` step with the same `dotnet` command and arguments.
- `$(Agent.TempDirectory)` and `$(Build.ArtifactStagingDirectory)` both became `${{ runner.temp }}`.
- `PublishPipelineArtifact@1` became `actions/upload-artifact`, with the same artifact name.

### Review the importer's warnings

The importer left comments in the workflow where it needed help:

- Above restore and build: `The dotnet CLI does not accept glob patterns.` It kept `**/*.csproj` anyway.
- In place of `PublishTestResults@2`: `(VSTest) is an unsupported test runner.` The task itself is gone.
- In place of `PublishCodeCoverageResults@2`: `This item has no matching transformer`, followed by the whole task, commented out.

The log has more detail. Show it:

```bash
cat tmp/dry-run/log/valet-*.log
```

Every task gets a `WARN` line that says `There was an error tranforming step, and an array is being surfaced`, including the tasks that converted, so those lines don't tell you much. The `DEBUG` lines are more useful. They say whether the importer has a transformer, the code that converts one kind of task:

```text
D, [2026-10-18T10:39:24.065074 #1] DEBUG -- : [Transformers::AzureDevops::Pipeline::Steps]: Located transformer for 'PublishTestResults@2'
W, [2026-10-18T10:39:24.065121 #1]  WARN -- : There was an error tranforming step, and an array is being surfaced: PublishTestResults@2
D, [2026-10-18T10:39:24.065223 #1] DEBUG -- : [Transformers::AzureDevops::Pipeline::Steps]: Failed to locate transformer for 'PublishCodeCoverageResults@2'
W, [2026-10-18T10:39:24.065256 #1]  WARN -- : There was an error tranforming step, and an array is being surfaced: PublishCodeCoverageResults@2
```

The importer has a transformer for `PublishTestResults@2`, but it only converts results in the JUnit, XUnit, and NUnit formats, which it turns into steps that use third-party test report actions. It doesn't support `VSTest`, the format of TRX files. It has no transformer for version 2 of `PublishCodeCoverageResults`; its documented transformer is for version 1. See the transformer docs for [PublishTestResults](https://github.com/github/gh-actions-importer/blob/main/docs/azure_devops/PublishTestResults.md) and [PublishCodeCoverageResults](https://github.com/github/gh-actions-importer/blob/main/docs/azure_devops/PublishCodeCoverageResults.md).

The log also has a line for `pr`, `Located transformer for 'pullRequest'`, but nothing for `trigger`. When the importer read the pipeline from a file, it dropped the push trigger without a warning.

### Find what didn't convert

Compare the converted workflow with the pipeline and with the workflow you fixed in Part B. These are the gaps:

- There's no `push` trigger, so the workflow wouldn't run when you push to `main`.
- The workflow is named `placeholder-org/placeholder-project`, after the organization and project options.
- There's no `permissions` and no `timeout-minutes`.
- The actions are pinned to old version tags, such as `actions/checkout@v4.1.0`, instead of full commit SHAs. [Lab 1](../foundation/01-build-application.md#what-happened) explains why a SHA is safer.
- `actions/setup-dotnet` has no inputs. It found `global.json` on its own and installed the right SDK, but `global-json-file` makes that explicit.
- Restore and build still pass `**/*.csproj`, which `dotnet` doesn't accept.
- Nothing publishes the test results or the coverage report. Both files would stay in `${{ runner.temp }}`, which GitHub empties after the job.
- The upload has no `if-no-files-found` or `retention-days`.

If you ran the converted workflow without changes, it would fail at **Restore dependencies** with `MSBUILD : error MSB1009: Project file does not exist.`, and GitHub would also warn that the old action versions target Node.js 20:

```text
Node.js 20 is deprecated. The following actions target Node.js 20 but are being forced to run on Node.js 24: actions/checkout@v4.1.0, actions/setup-dotnet@v4.0.0. For more information see: https://github.blog/changelog/2025-09-19-deprecation-of-node-20-on-github-actions-runners/
```

### Repair the converted workflow

Copy the converted workflow to a file you can edit, then open `tmp/migrated-ci.yml` in a text editor:

```bash
cp tmp/dry-run/pipelines/placeholder-org/placeholder-project/.github/workflows/placeholder-project.yml tmp/migrated-ci.yml
```

Keep the importer's indentation, with each step's `-` 4 spaces in. Make these seven changes. Each block shows that part of the file after the change.

1. Rename the workflow, and replace the trigger. Add `push` for `main`, so it runs like `trigger`, keep `pull_request`, and add `workflow_dispatch` and `permissions`:

```yaml excerpt=solutions/migration/imported-ci.yml
name: Migrated CI
on:
  push:
    branches:
    - main
  pull_request:
    branches:
    - main
  workflow_dispatch:
permissions:
  contents: read
```

2. Add `timeout-minutes` below `runs-on`:

```yaml excerpt=solutions/migration/imported-ci.yml
    runs-on: ubuntu-latest
    timeout-minutes: 10
```

3. Rename the checkout step, pin both actions to full commit SHAs, and give `actions/setup-dotnet` the `global.json` file:

```yaml excerpt=solutions/migration/imported-ci.yml
    - name: Check out the repository
      uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
    - name: Install the .NET SDK from global.json
      uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
      with:
        global-json-file: global.json
```

4. Delete the two glob comments, and restore and build the solution file instead of `**/*.csproj`:

```yaml excerpt=solutions/migration/imported-ci.yml
    - name: Restore dependencies
      run: dotnet restore GitHubActionsWorkshop.slnx
    - name: Build
      run: dotnet build GitHubActionsWorkshop.slnx --configuration ${{ env.buildConfiguration }} --no-restore
```

5. In the test step, name the TRX file and write the results to `TestResults` in the repository folder, where the next steps look for them:

```yaml excerpt=solutions/migration/imported-ci.yml
    - name: Run the tests with Coverlet
      run: dotnet test tests/WorkshopCore.Tests/WorkshopCore.Tests.csproj --configuration ${{ env.buildConfiguration }} --no-build --logger "trx;LogFileName=test-results.trx" --results-directory TestResults --collect "XPlat Code Coverage"
```

6. Replace the commented-out lines with the two uploads and the job summary from Part B. They're the same steps as in the finished workflow, indented 2 spaces less and without the blank lines between them:

```yaml excerpt=solutions/migration/imported-ci.yml
    - name: Upload the test results
      if: ${{ !cancelled() }}
      uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
      with:
        name: test-results
        path: TestResults/*.trx
        if-no-files-found: error
        retention-days: 7
    - name: Upload the coverage report
      if: ${{ !cancelled() }}
      uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
      with:
        name: coverage
        path: TestResults/*/coverage.cobertura.xml
        if-no-files-found: error
        retention-days: 7
    - name: Write the job summary
      if: ${{ !cancelled() }}
      run: |
        coverage_file=$(find TestResults -mindepth 2 -maxdepth 2 -name coverage.cobertura.xml | head -n 1)
        if [ -z "$coverage_file" ]; then
          echo "::error::No coverage.cobertura.xml file in TestResults. Check that the test step collects coverage."
          exit 1
        fi
        attribute() { echo "$1" | grep -o " $2=\"[^\"]*\"" | cut -d '"' -f 2; }
        coverage=$(grep -o -m 1 '<coverage [^>]*>' "$coverage_file")
        counters=$(grep -o -m 1 '<Counters [^>]*>' TestResults/test-results.trx)
        line_rate=$(attribute "$coverage" line-rate)
        {
          echo "## Tests and coverage"
          echo ""
          echo "| Measure | Result |"
          echo "| --- | --- |"
          echo "| Tests passed | $(attribute "$counters" passed) of $(attribute "$counters" total) |"
          echo "| Line coverage | $(awk -v rate="$line_rate" 'BEGIN { printf "%.1f%%", rate * 100 }') |"
          echo "| Lines covered | $(attribute "$coverage" lines-covered) of $(attribute "$coverage" lines-valid) |"
          echo ""
          echo "Download the coverage artifact for the full Cobertura report."
        } >> "$GITHUB_STEP_SUMMARY"
```

7. Pin `actions/upload-artifact` to a full commit SHA, and add `if-no-files-found` and `retention-days` to the last step:

```yaml excerpt=solutions/migration/imported-ci.yml
    - name: Upload the app as a pipeline artifact
      uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
      with:
        name: workshop-dashboard
        path: "${{ runner.temp }}/app"
        if-no-files-found: error
        retention-days: 7
```

Leave **Publish the app** as it is. `${{ env.buildConfiguration }}` works in a `run` script, because GitHub replaces the expression before the shell starts, and `${{ runner.temp }}/app` is a fine place for a folder that's uploaded in the same job.

To check your changes, compare your file with the solution:

```bash
diff tmp/migrated-ci.yml solutions/migration/imported-ci.yml
```

If your changes match, the only difference is the solution's two comment lines at the top:

```text
0a1,2
> # GitHub Actions Importer's conversion of migration/azure-devops/azure-pipelines.yml,
> # after the repairs in Lab 12, Part C. It keeps the importer's formatting.
```

### Add it to your repository and run it

Pushing a workflow file from a terminal fails if your sign-in doesn't have the `workflow` scope, so use your browser:

1. In your text editor, select everything in `tmp/migrated-ci.yml` and copy it.
2. In your browser, select the **Code** tab, then `.github`, `workflows`, and `migrated-ci.yml`, and select the pencil icon to edit the file.
3. Select all the text in the editor, delete it, and paste the file you copied.
4. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `main` branch, then select **Commit changes**.
5. Select the **Actions** tab, then **Migrated CI**, and open the new run.
6. When the run finishes, in about a minute, check its summary page. The job summary shows `4 of 4` tests passed and `100.0%` line coverage, and **Artifacts** lists `test-results`, `coverage`, and `workshop-dashboard`, as in Part B. The job is named `build` this time, because the converted workflow doesn't give it a `name`.

## Part D: Compare test coverage

Use the newest successful run of **Migrated CI**. It saved the test results and the coverage report in two standard file formats, and you can open both.

1. On the run's summary page, under **Artifacts**, select `test-results`, then `coverage`. Your browser downloads `test-results.zip` and `coverage.zip`.
2. Unzip both. `test-results.zip` holds `test-results.trx`, and `coverage.zip` holds a folder named with a GUID, with `coverage.cobertura.xml` in it.

If you have a terminal with GitHub CLI, you can download both from your clone instead. The run ID is the number after `/runs/` in the run's URL:

```bash
gh run download RUN_ID -n test-results -n coverage -D tmp/results
```

That creates `tmp/results/test-results/test-results.trx` and `tmp/results/coverage/GUID/coverage.cobertura.xml`.

### Read the test results

Open `test-results.trx` in a text editor. Near the end, inside `ResultSummary`, the `Counters` element has the totals:

```xml
<Counters total="4" executed="4" passed="4" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" inProgress="0" pending="0" />
```

Earlier in the file, a `UnitTestResult` element for each of the four tests has the test's name, its duration, and `outcome="Passed"`. In Azure DevOps, `PublishTestResults@2` reads this file to fill the **Tests** tab. In your workflow, **Write the job summary** reads `passed` and `total` from `Counters`.

### Read the coverage report

Open `coverage.cobertura.xml`. `REPO` stands for your repository's name:

```xml
<?xml version="1.0" encoding="utf-8"?>
<coverage line-rate="1" branch-rate="1" version="1.9" timestamp="1792313501" lines-covered="2" lines-valid="2" branches-covered="0" branches-valid="0">
  <sources>
    <source>/</source>
  </sources>
  <packages>
    <package name="WorkshopCore" line-rate="1" branch-rate="1" complexity="2">
      <classes>
        <class name="WorkshopCore.Calculator" filename="home/runner/work/REPO/REPO/src/WorkshopCore/Calculator.cs" line-rate="1" branch-rate="1" complexity="2">
          <methods>
            <method name="Add" signature="(System.Int32,System.Int32)" line-rate="1" branch-rate="1" complexity="1">
              <lines>
                <line number="9" hits="3" branch="False" />
              </lines>
            </method>
            <method name="Subtract" signature="(System.Int32,System.Int32)" line-rate="1" branch-rate="1" complexity="1">
              <lines>
                <line number="14" hits="1" branch="False" />
              </lines>
            </method>
          </methods>
          <lines>
            <line number="9" hits="3" branch="False" />
            <line number="14" hits="1" branch="False" />
          </lines>
        </class>
      </classes>
    </package>
  </packages>
</coverage>
```

- `line-rate` is the share of coverable lines that ran during the tests, from 0 to 1, so `1` is 100%. `lines-covered="2"` and `lines-valid="2"` give the same result as counts. These are the numbers in the job summary.
- The file tracks two lines of `Calculator.cs`. Line 9, `return a + b;` in `Add`, ran 3 times, once for each of the three `Add` tests. Line 14, `return a - b;` in `Subtract`, ran once.
- `branch-rate="1"` with `branches-valid="0"` means there are no branches to cover, not that every branch was tested.
- The `timestamp` and the GUID folder name change in every run.

### Where Azure DevOps shows them

This lab doesn't use Azure DevOps, so you won't see these pages. Microsoft's documentation describes them this way:

- The run's **Tests** tab shows the total number of tests, how many failed, and the pass percentage. By default, it lists only failed and aborted tests. Each result has its error message, stack trace, attachments, and history. See [reviewing test results](https://learn.microsoft.com/azure/devops/pipelines/test/review-continuous-test-results-after-build?view=azure-devops).
- The run's **Code Coverage** tab shows coverage by folder and file, opens each file's source, and can switch to an HTML report. The coverage artifacts also appear on the run's **Summary** tab. See [reviewing code coverage results](https://learn.microsoft.com/azure/devops/pipelines/test/review-code-coverage-results?view=azure-devops).
- Azure DevOps can also show coverage of the changed lines in a pull request, but only for code in Azure Repos.

### Where GitHub Actions shows them

GitHub Actions doesn't have a built-in page for test results or coverage. In this lab, you see them in three places:

- The test step's log, where a failed test shows its name, error message, and stack trace, as in Lab 2.
- The job summary, which shows whatever the workflow writes to it. Here, that's the test counts and the line coverage.
- The artifacts, which keep the full files until they expire.

To browse coverage file by file, you can turn the Cobertura file into an HTML report with a tool such as [ReportGenerator](https://github.com/danielpalme/ReportGenerator), installed as a `dotnet tool`, and upload the report as another artifact. This lab doesn't do that.

GitHub Code Quality, a separate paid product for GitHub Team and GitHub Enterprise Cloud, can also show coverage on pull requests. When it's turned on for a repository, a workflow uploads the Cobertura file with the `actions/upload-code-coverage` action, and a bot comments on pull requests with their line coverage compared with the default branch, and the files that gained or lost coverage. This lab doesn't use it. See [Setting up code coverage for your repository](https://docs.github.com/en/code-security/how-tos/maintain-quality-code/set-up-code-coverage).

| Result | Azure DevOps | GitHub Actions in this lab |
| --- | --- | --- |
| Test counts | **Tests** tab | Job summary, and the TRX file in the `test-results` artifact |
| Failed tests | **Tests** tab, with error messages and stack traces | The test step's log, and the TRX file |
| Line coverage | **Code Coverage** tab | Job summary, and the Cobertura file in the `coverage` artifact |
| Coverage by file | **Code Coverage** tab | The Cobertura file, or an HTML report you generate |
| Coverage in pull requests | Coverage of the changed lines, with Azure Repos | The job summary of the pull request's run |

## What happened

Both conversions kept most of the pipeline's structure: the agent, the variables, and the `dotnet` commands. The reporting is what had to change. Azure Pipelines has tasks that publish test results and coverage to tabs on the run. GitHub Actions has no equivalent, so the workflow keeps the files as artifacts and writes its own job summary.

Here's what GitHub Actions Importer did with each part of the pipeline in Part C, and what you changed afterward:

| Pipeline part | GitHub Actions Importer | What you changed |
| --- | --- | --- |
| `trigger` | Dropped, without a warning | Added `push` for `main` |
| `pr` | Converted to `pull_request` | Nothing |
| `pool` | Converted to `runs-on` | Added `timeout-minutes` |
| `variables` | Converted to `env` | Nothing |
| Checkout | Added an `actions/checkout` step, pinned to a tag | Renamed it, and pinned it to a commit SHA |
| `UseDotNet@2` | Converted to `actions/setup-dotnet`, pinned to a tag, without inputs | Pinned it to a commit SHA, and added `global-json-file` |
| Restore and build | Converted, with a wildcard that `dotnet` doesn't accept | Used the solution file |
| Test | Converted, with `$(Agent.TempDirectory)` changed to `${{ runner.temp }}` | Named the TRX file, and moved the results to `TestResults` |
| `PublishTestResults@2` | Removed, because `VSTest` isn't supported | Uploaded the TRX file as an artifact |
| `PublishCodeCoverageResults@2` | Commented out, with no transformer | Uploaded the Cobertura file, and wrote the job summary |
| Publish | Converted, without the three publish inputs, which `dotnet publish` doesn't need | Nothing |
| `PublishPipelineArtifact@1` | Converted to `actions/upload-artifact`, pinned to a tag | Pinned it to a commit SHA, and added `if-no-files-found` and `retention-days` |

You also renamed the workflow and added `permissions` and `workflow_dispatch`.

What the importer converts depends on its version, and this lab used 1.3.22671. If it can't convert a task your pipelines use often, you can write a custom transformer for it in Ruby. See [Custom transformers for GitHub Actions Importer](https://docs.github.com/en/actions/reference/github-actions-importer/custom-transformers). Pipeline templates convert too: a template used for stages, jobs, or deployments becomes a reusable workflow, and one used for steps becomes a composite action. For a real migration, `audit` analyzes an organization's pipelines and reports which ones it can convert completely, partly, or not at all, and `forecast` estimates GitHub Actions usage from past pipeline runs. Both read from Azure DevOps, so they need real access.

## Check your result

Select the **Actions** tab, then **Migrated CI**. The newest run on `main` has a green check mark. Its summary page shows a job summary with `4 of 4` tests passed and `100.0%` line coverage, and lists the `test-results`, `coverage`, and `workshop-dashboard` artifacts. Your `.github/workflows/migrated-ci.yml` matches [the finished workflow](../../solutions/migration/migrated-ci.yml) from Part B, apart from the comment at the top, or, if you did Part C, [the repaired importer output](../../solutions/migration/imported-ci.yml) without its two comment lines.

## If something fails

If a failed run named `.github/workflows/migrated-ci.yml` appears right after you commit the file, GitHub couldn't read the workflow file. Open that run. Under **Annotations**, an **Invalid workflow file** error names the line with the problem. The usual cause is indentation that changed when you pasted the YAML. Compare your file with [the Part B solution](../../solutions/migration/migrated-ci.yml) or [the Part C solution](../../solutions/migration/imported-ci.yml), fix it on `main`, and commit.

If **Migrated CI** isn't in the sidebar, check that the file is on `main` and that its path is exactly `.github/workflows/migrated-ci.yml`.

If **Write the job summary** still says `No coverage.cobertura.xml file in TestResults`, check that `--collect "XPlat Code Coverage"` is on the same line as the rest of the test command, with straight double quotes.

If **Upload the test results** still says `No files were found with the provided path:`, check that its `path` is `TestResults/*.trx`, with nothing in front.

If **Restore dependencies** fails with `MSBUILD : error MSB1009: Project file does not exist.`, the step still passes `**/*.csproj`. Make change 4 in [Repair the converted workflow](#repair-the-converted-workflow).

If a run warns that `Node.js 20 is deprecated`, an action is still pinned to an old version tag. Make changes 3 and 7 in [Repair the converted workflow](#repair-the-converted-workflow).

If the dry run says `No value provided for the required options '--azure-devops-organization, --azure-devops-project'`, add both options, as in [Run the dry run](#run-the-dry-run).

If the dry run says `Unable to locate credentials for 'https://github.com'` or `Unable to locate credentials for 'https://dev.azure.com'`, the token variables didn't reach the command. Copy the whole command, including the two variable lines and the `\` at the end of each line.

If `docker info` prints an error, Docker isn't running. Start Docker Desktop, or the Docker service on Linux, and wait until `docker info` prints the version.

If `gh actions-importer update` can't download the image, check that Docker is running and that your network allows connections to `ghcr.io`, where the image is published.

If your repository has no `migration` folder, it was created from an older copy of the template. Create `migration/azure-devops/azure-pipelines.yml` with **Add file** and the pipeline from [Part A](#part-a-understand-the-existing-pipeline). In Part C, compare your file with the blocks in [Repair the converted workflow](#repair-the-converted-workflow) instead of running `diff`.

If `diff` shows other lines, the lines that start with `<` come from your file, and the lines that start with `>` show what the solution has there. Spaces count, so check the indentation too.

## Clean up

Delete the workflow so it stops running on every push and pull request:

1. Select the **Code** tab, then `.github`, `workflows`, and `migrated-ci.yml`.
2. In the top-right corner of the file, select the three dots, then **Delete file**.
3. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `main` branch, then select **Commit changes**.

Deleting the file doesn't start a run, and the artifacts expire after 7 days.

If you created a codespace, delete it: on [your codespaces](https://github.com/codespaces), select the three dots to the right of the codespace, then **Delete**.

If you did Part C on your own computer, delete the files the importer wrote. If you don't want to keep the importer, also remove its container image and the extension. Run these in your clone:

```bash
rm -rf tmp/dry-run tmp/migrated-ci.yml tmp/results
docker image rm ghcr.io/actions-importer/cli:latest
gh extension remove actions-importer
```

## Next step

Azure Pipelines templates convert to reusable workflows and composite actions. To learn how those work, go to [Lab 10: Reuse workflows and steps](reusable-workflows.md), or pick another lab from [the lesson list](../../README.md).
