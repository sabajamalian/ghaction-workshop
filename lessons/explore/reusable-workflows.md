---
title: "Lab 10: Reuse workflows and steps"
summary: "Turn the build job into a reusable workflow, call it twice with different inputs, and move its setup steps into a composite action."
duration: 35
difficulty: Intermediate
order: 10
---

# Lab 10: Reuse workflows and steps

`build.yml` from Lab 1 and `tests.yml` from Lab 2 start with the same two steps: check out the repository, then set up .NET. Every new workflow that builds the app would copy them again. In this lab, you'll put the setup steps in a composite action, which a job runs as a single step. Then you'll move the build into a reusable workflow, which other workflows call the way code calls a function: they pass inputs and secrets in and get outputs back. Finally, you'll call it twice in one run, for a Release build with tests and a Debug build without them.

## Before you start

You need the repository you created in [Lab 0](../foundation/00-create-repository.md). The reusable workflow builds the same app as [Lab 1](../foundation/01-build-application.md) and runs the same tests as [Lab 2](../foundation/02-test-pull-request.md). Everything happens in your browser.

If your repository is public, the runs are free. If it's private, they use your account's included Actions minutes, and GitHub rounds each job up to a whole minute. A run of this lab's workflow has three short jobs, so it counts as about 3 of the 2,000 minutes a month that GitHub Free includes. See [Actions runner pricing](https://docs.github.com/en/billing/reference/actions-runner-pricing).

## Add a practice secret

The reusable workflow takes a secret so you can see how secrets get passed, but it only checks whether the secret has a value. Use made-up text, not a real password or token. If you skip this section, the lab still works, and the build reports that the secret is empty.

1. Select the **Settings** tab. In the left sidebar, in the "Security" section, select **Secrets and variables**, then **Actions**.
2. Select the **Secrets** tab. If it already lists `PRACTICE_SECRET` from another lab, skip to the next section, because any value works. Otherwise, select **New repository secret**.
3. In **Name**, type `PRACTICE_SECRET`. In **Secret**, type any made-up text, such as `practice-value-123`.
4. Select **Add secret**.

## Create the composite action

A composite action is a list of steps saved as an action, so a job can run all of them with one `uses` step. It lives in a folder of its own, in a file named `action.yml`.

1. Select the **Code** tab, and make sure the branch menu shows `main`.
2. Select **Add file**, then **Create new file**.
3. In the file name box, type `.github/actions/setup-build/action.yml`. Each `/` you type starts a new folder.
4. Copy this into the editor. YAML uses indentation to show structure, so keep the spaces exactly as they are.

```yaml source=solutions/reusable-workflows/setup-build/action.yml
name: "Set up the build"
description: "Installs the .NET SDK that global.json asks for and restores the dashboard's dependencies."

runs:
  using: composite
  steps:
    - name: Set up .NET
      uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
      with:
        global-json-file: global.json

    - name: Restore dependencies
      shell: bash
      run: dotnet restore src/WorkshopDashboard/WorkshopDashboard.csproj
```

5. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `main` branch, then select **Commit changes**.

Every action's `action.yml` needs a `name` and a `description`. `runs.using: composite` makes this a composite action, and `steps` lists its steps in the same format as a job's steps, with one difference: a `run` step in a composite action has no default shell, so it has to set `shell`. The folder can be anywhere in the repository. This lab keeps it under `.github/actions`, next to the workflows.

Committing the file doesn't run anything. The action runs when a workflow uses it.

## Create the reusable workflow

1. Select the **Code** tab, then **Add file**, then **Create new file**.
2. In the file name box, type `.github/workflows/reusable-build.yml`.
3. Copy this workflow into the editor:

```yaml source=solutions/reusable-workflows/reusable-build.yml
name: Reusable build

on:
  workflow_call:
    inputs:
      configuration:
        description: "The build configuration: Release or Debug."
        type: string
        default: Release
      run-tests:
        description: "Whether to run the unit tests before publishing."
        type: boolean
        default: true
      artifact-name:
        description: "The name of the artifact to upload."
        type: string
        required: true
    secrets:
      practice-secret:
        description: "A practice secret. The job only checks whether it has a value."
        required: false
    outputs:
      artifact-url:
        description: "The download URL of the uploaded artifact."
        value: ${{ jobs.build.outputs.artifact-url }}

permissions:
  contents: read

jobs:
  build:
    name: Build
    runs-on: ubuntu-latest
    timeout-minutes: 10
    outputs:
      artifact-url: ${{ steps.upload.outputs.artifact-url }}
    steps:
      - name: Check out the repository
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1

      # A local action is read from the checked-out files, so this step has to come after the checkout.
      - name: Set up .NET and restore
        uses: ./.github/actions/setup-build

      - name: Check for the practice secret
        env:
          PRACTICE_SECRET: ${{ secrets.practice-secret }}
        run: |
          if [ -n "$PRACTICE_SECRET" ]; then
            echo "practice-secret has a value."
          else
            echo "practice-secret is empty or wasn't passed."
          fi

      - name: Run the tests
        if: ${{ inputs.run-tests }}
        env:
          CONFIGURATION: ${{ inputs.configuration }}
        run: dotnet test tests/WorkshopCore.Tests --configuration "$CONFIGURATION"

      - name: Publish the dashboard
        env:
          CONFIGURATION: ${{ inputs.configuration }}
        run: dotnet publish src/WorkshopDashboard/WorkshopDashboard.csproj --configuration "$CONFIGURATION" --no-restore --output publish

      - name: Upload the published app
        id: upload
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: ${{ inputs.artifact-name }}
          path: publish
          if-no-files-found: error
          retention-days: 1
```

4. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `main` branch, then select **Commit changes**.

This commit doesn't start a run either. A workflow whose only trigger is `workflow_call` runs only when another workflow calls it.

### What's in the reusable workflow

- `on: workflow_call` is what makes a workflow reusable. A reusable workflow has to be directly in `.github/workflows`, not in a subfolder.
- `inputs` lists what a caller can pass in. Each input has a `type`: `string`, `boolean`, or `number`. Every caller has to pass `artifact-name`, because it's `required: true`. `configuration` and `run-tests` have a `default`, so callers can leave them out.
- `secrets` lists the secrets a caller can pass in. A reusable workflow can't read the caller's secrets unless the caller passes them, except `GITHUB_TOKEN`, which it always gets. `practice-secret` is `required: false`, so a call without it still works.
- `outputs` is what the workflow hands back. The value moves up in three steps: `actions/upload-artifact` sets a step output named `artifact-url`, the `build` job's `outputs` passes it on as a job output, and the workflow's `outputs` reads it from `jobs.build.outputs.artifact-url`.
- `uses: ./.github/actions/setup-build` runs your composite action. A `./` path points to a folder in the files the job has checked out, so this step has to come after **Check out the repository**, as the comment above it says.
- **Check for the practice secret** reads the secret through `env` and prints only whether it's empty, never the value.
- `if: ${{ inputs.run-tests }}` runs **Run the tests** only when `run-tests` is `true`. The commands get `inputs.configuration` through `env` as well, rather than through a `${{ }}` expression in the command, so an unusual value from a caller can't change the command.
- `timeout-minutes: 10` is on the `build` job here because a job that calls a reusable workflow can't set `timeout-minutes` itself.

## Create the caller

1. Select the **Code** tab, then **Add file**, then **Create new file**.
2. In the file name box, type `.github/workflows/build-both.yml`.
3. Copy this workflow into the editor:

```yaml source=solutions/reusable-workflows/build-both.yml
name: Build Release and Debug

on:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  release:
    name: Release build
    uses: ./.github/workflows/reusable-build.yml
    with:
      configuration: Release
      artifact-name: dashboard-release
    secrets:
      practice-secret: ${{ secrets.PRACTICE_SECRET }}

  debug:
    name: Debug build
    uses: ./.github/workflows/reusable-build.yml
    with:
      configuration: Debug
      run-tests: false
      artifact-name: dashboard-debug

  summary:
    name: Summarize the builds
    needs: [release, debug]
    runs-on: ubuntu-latest
    timeout-minutes: 5
    steps:
      - name: Write the artifact links to the summary
        env:
          RELEASE_URL: ${{ needs.release.outputs.artifact-url }}
          DEBUG_URL: ${{ needs.debug.outputs.artifact-url }}
        run: |
          {
            echo "## Published builds"
            echo ""
            echo "- Release: [dashboard-release]($RELEASE_URL)"
            echo "- Debug: [dashboard-debug]($DEBUG_URL)"
          } >> "$GITHUB_STEP_SUMMARY"
```

4. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `main` branch, then select **Commit changes**.

### What's in the caller

- `release` and `debug` have `uses` where a normal job has `runs-on` and `steps`, which makes each of them a call to the reusable workflow. `./.github/workflows/reusable-build.yml` means the file in this repository, at the same commit as the caller.
- `with` passes the inputs. `release` passes `configuration` and `artifact-name`, and gets the default, `true`, for `run-tests`. `debug` sets `run-tests: false` without quotes, because the input is a boolean. In quotes, `"false"` is a string, and GitHub rejects the call.
- `secrets` passes your `PRACTICE_SECRET` under the name the reusable workflow declares, `practice-secret`. `debug` passes no secrets.
- A job that calls a reusable workflow can only use these keywords: `name`, `uses`, `with`, `secrets`, `strategy`, `needs`, `if`, `concurrency`, `permissions`, and `cache-mode`. With `strategy`, one calling job could make both calls from a matrix, but the calling job would then have a single output, set by whichever call finished last. Two calling jobs keep the two links apart.
- `summary` is an ordinary job. `needs: [release, debug]` makes it wait for both calls, and `needs.release.outputs.artifact-url` reads the output that the `release` call returned. Its step writes both links to the run's summary page through `$GITHUB_STEP_SUMMARY`.
- `permissions` limits the `GITHUB_TOKEN` for the whole run, called jobs included. The reusable workflow can keep that access or lower it, but it can't raise it.

In a real project, you'd also change `build.yml` and `tests.yml` to call the reusable workflow. This lab leaves them alone, so they still match Labs 1 and 2.

## Run the workflow

1. Select the **Actions** tab.
2. In the left sidebar, select **Build Release and Debug**.
3. Select **Run workflow**. In the menu that opens, leave the branch set to `main`, then select the green **Run workflow** button.

After a few seconds, a new run appears at the top of the list. If it doesn't, refresh the page.

## Follow the run

1. Select the new run. Its graph shows three jobs: **Release build / Build**, **Debug build / Build**, and **Summarize the builds**.

   A called job's name has two parts: the name of the calling job in `build-both.yml`, then the name of the job in `reusable-build.yml`. The two builds run at the same time, and **Summarize the builds** waits for both. The whole run takes about a minute.

2. Select **Release build / Build** in the graph, or under **Jobs** in the left sidebar, and expand these steps:

   - **Set up job** has a `Uses:` line with your repository, the path `.github/workflows/reusable-build.yml`, the branch, and the commit it ran from. Below it, **Inputs** lists what the job received: `configuration: Release`, `run-tests: true`, and `artifact-name: dashboard-release`. `run-tests` is there even though `build-both.yml` doesn't pass it, because it has a default.
   - **Set up .NET and restore** is the composite action. It's one step in the list, and its log holds the output of both of its steps. **Post Set up .NET and restore**, near the end, is the cleanup that `actions/setup-dotnet` runs inside it.
   - **Check for the practice secret** prints `practice-secret has a value.` If you skipped the practice secret, it prints `practice-secret is empty or wasn't passed.`
   - **Run the tests** ends by saying that all four tests passed, as in Lab 2.
   - **Complete job** includes `Set output 'artifact-url'`. That's the job output, which the reusable workflow hands back to the caller.

3. Select **Debug build / Build**. **Run the tests** was skipped, because the `debug` call passed `run-tests: false`, and **Check for the practice secret** prints `practice-secret is empty or wasn't passed.`, because `debug` passes no secrets.
4. Select **Summary** in the left sidebar. Below the graph, the summary that **Summarize the builds** wrote has a **Published builds** heading with a link to each artifact. Under **Artifacts**, the run lists `dashboard-release` and `dashboard-debug`.

The left sidebar of the **Actions** tab now lists **Reusable build** too. If you select it, the page says "This workflow has no runs yet." The reusable workflow's jobs ran as part of the **Build Release and Debug** run, so that's where you'll find them.

## Pass secrets explicitly or with inherit

The `release` call passes one secret, under a name the reusable workflow chose. A caller can also pass all of its secrets at once:

```yaml
jobs:
  release:
    name: Release build
    uses: ./.github/workflows/reusable-build.yml
    with:
      configuration: Release
      artifact-name: dashboard-release
    secrets: inherit
```

With `secrets: inherit`, the reusable workflow gets every secret the caller can read, under its original name. That doesn't fit this reusable workflow: **Check for the practice secret** reads `secrets.practice-secret`, which would be empty, because the inherited secret is still named `PRACTICE_SECRET`. The reusable workflow would have to read `secrets.PRACTICE_SECRET` instead, which ties it to the secret names in each caller's repository.

An explicit mapping is narrower. The reusable workflow gets only the secrets it declares, under names it chooses, and the caller's file shows which secrets it hands over. Some rules apply to both:

- `inherit` passes secrets within a repository, across repositories in the same organization, and across organizations in the same enterprise.
- Secrets only go one level down. If a reusable workflow calls another one, it has to pass the secrets on, by name or with `inherit`.
- GitHub doesn't check that a secret you map exists. A misspelled `${{ secrets.PRACTICE_SECRT }}` passes an empty value, without an error.
- Passing a secret that the reusable workflow doesn't declare is an error, and the run fails before any job starts.

## Call it from another repository

From another repository, a caller names the owner, the repository, the path, and a ref:

```text
uses: OWNER/REPO/.github/workflows/reusable-build.yml@COMMIT_SHA
```

The ref can be a commit SHA, a tag, or a branch, but not an expression. Use a full commit SHA, for the same reason the workshop pins actions to one: the code you call can't change without you noticing.

Who can call a reusable workflow depends on where it's stored:

- Workflows in the same repository can always call it.
- If it's in a public repository, other repositories can call it, unless their Actions settings, or their organization's, block it.
- If it's in a private repository, only private repositories can call it, and only after its repository allows access. For a repository in your personal account, open the private repository's **Settings**, select **Actions**, then **General**, and in the **Access** section, select **Accessible from repositories owned by 'USERNAME' user**, where `USERNAME` is your account name. Then select **Save**.

Before you allow that access, know that outside collaborators on the calling repositories can see the logs of runs that use your private workflows. See [Sharing actions and workflows from your private repository](https://docs.github.com/en/actions/how-tos/reuse-automations/share-across-private-repositories).

This lab's reusable workflow has a catch when it's called from another repository. In that run, `actions/checkout` checks out the caller's repository, and `./.github/actions/setup-build` is a path in those files, so the caller's repository would need its own copy of the composite action. The `$/` prefix avoids that: `$/.github/actions/setup-build` always means the repository that contains the file it's written in, at the commit being run, and it doesn't need a checkout. GitHub's documentation recommends `$/` for actions and reusable workflows in the same repository. This lab uses `./` because `$/` isn't available on GitHub Enterprise Server, and `./` works everywhere. See [Workflow syntax for GitHub Actions](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#jobsjob_idstepsuses).

## Reusable workflow or composite action

Both let you write something once and use it in many workflows. They differ in what they contain and where they run:

| | Reusable workflow | Composite action |
| --- | --- | --- |
| Contains | Jobs, each with its own runner, permissions, and steps | Steps, which run on the runner of the job that uses it |
| Lives in | One file, directly in `.github/workflows` | A folder with an `action.yml`, anywhere in a repository, or a repository of its own |
| Used by | A job, with `uses` in place of `runs-on` and `steps` | A step, with `uses` |
| Appears in the log as | Separate jobs and steps, named `caller / called` | One step, with the output of its inner steps |
| Secrets | Declared under `workflow_call` and passed by the caller | Can't read `secrets`, so a caller passes values in as inputs |
| Nesting | Up to ten levels of workflows | Up to ten composite actions in one workflow |
| GitHub Marketplace | Can't be published | Can be published |

Use a reusable workflow to share whole jobs, including the runner they need and the permissions and secrets they get. Use a composite action to share a few steps that each job surrounds with its own, like the setup steps in this lab. A job that calls a reusable workflow can't add steps before or after the call. [Reusing workflow configurations](https://docs.github.com/en/actions/concepts/workflows-and-actions/reusing-workflow-configurations) compares the two in more detail.

## When to reuse instead of copying

Copying a few lines of YAML is fine for a one-off, or when the copies are meant to drift apart. Reuse starts to pay off when the same job appears in several workflows or repositories. Then an action pin, a build flag, or an SDK version gets fixed in one place, and teams share one reviewed job instead of keeping their own copies.

Reuse has costs too. Readers have to open a second file to see what a job does. The inputs, secrets, and outputs become an interface, so renaming one breaks every caller that still uses the old name: a call that passes an input the reusable workflow doesn't declare fails before any job starts. And callers that pin a commit SHA keep running the old version until someone updates the pin. That's the point of pinning, but it means a fix doesn't reach them on its own.

## Limits

- A run can connect up to ten levels of workflows: the caller and up to nine levels of reusable workflows below it. Loops aren't allowed.
- One workflow file can call up to 50 unique reusable workflows, counting the ones those workflows call in turn.
- `env` doesn't cross a call in either direction. The caller's `env` doesn't reach the called jobs, and the `env` context isn't available in `with`. Pass values as inputs and outputs, or store shared values as repository or organization variables and read them with `vars`.
- The caller's `GITHUB_TOKEN` permissions are a ceiling. If the reusable workflow asks for more than the caller grants, the run fails before any job starts.
- Inside a called workflow, `github.workflow` is the caller's name. If both files use `${{ github.workflow }}` as a `concurrency` group with `cancel-in-progress: true`, the called workflow cancels its own caller.
- GitHub-hosted runner minutes are billed to the caller, even when the reusable workflow is in another repository.

The [reusable workflows reference](https://docs.github.com/en/actions/reference/workflows-and-actions/reusing-workflow-configurations) lists these limits and the access rules.

## What happened

When you started **Build Release and Debug**, GitHub read `build-both.yml`, found the two jobs with `uses`, and loaded `reusable-build.yml` from the same commit. Before starting any job, it checked each call: that every input and secret it passes is declared, that inputs have the right types, that required inputs are there, and that the reusable workflow doesn't ask for more permissions than the caller grants. Then it added the reusable workflow's `build` job to the run once for each call, with that call's inputs and secrets. That's why the run has two jobs named **Build**, each under the name of the job that called it.

The composite action was loaded later. A runner can only read a `./` action once the files are on the runner, so when **Set up .NET and restore** started, the runner read `action.yml` from the checked-out files, downloaded `actions/setup-dotnet`, and ran both steps as part of that one step.

## Check your result

Select the **Actions** tab, then **Build Release and Debug** in the left sidebar. Your run is at the top of the list with a green check mark. Open it. The graph on its **Summary** shows **Release build / Build**, **Debug build / Build**, and **Summarize the builds**. Below the graph are the **Published builds** links, and under **Artifacts**, the run lists `dashboard-release` and `dashboard-debug`. In **Release build / Build**, **Check for the practice secret** prints `practice-secret has a value.`, or `practice-secret is empty or wasn't passed.` if you skipped the practice secret.

## If something fails

If **Build Release and Debug** isn't in the left sidebar of the **Actions** tab, check the path of the file you created on the **Code** tab. It must be exactly `.github/workflows/build-both.yml`. GitHub only runs workflow files that are in the `.github/workflows` folder and end in `.yml` or `.yaml`.

If a failed run named `.github/workflows/build-both.yml` appears on the **Actions** tab right after you commit the file, GitHub couldn't read the workflow file. Open that run. Under **Annotations**, an **Invalid workflow file** error names the line with the problem. `Unexpected value 'timeout-minutes'` means a job that calls the reusable workflow has a keyword it can't use. Compare your file with [the finished caller](../../solutions/reusable-workflows/build-both.yml), fix it on `main`, and commit.

If the green **Run workflow** button shows an error instead of starting a run, look for one of these messages in it. `failed to fetch workflow: workflow was not found` means the `uses:` path in `build-both.yml` doesn't match the reusable workflow's file, which must be `./.github/workflows/reusable-build.yml`. `workflow is not reusable` means the path points to a workflow without a `workflow_call` trigger, such as `build.yml`.

If the run fails a second or two after it starts and has no jobs, GitHub rejected one of the calls before any job started. Compare `with` and `secrets` in `build-both.yml` with the `workflow_call` section of your `reusable-build.yml` or of [the finished reusable workflow](../../solutions/reusable-workflows/reusable-build.yml). The causes are an input or secret name the reusable workflow doesn't declare, such as a misspelled `artifact_name`; a missing `artifact-name`; a boolean in quotes, such as `run-tests: "false"`; or `permissions` in `build-both.yml` that grant less than the reusable workflow asks for.

If **Set up .NET and restore** fails with `Can't find 'action.yml', 'action.yaml' or 'Dockerfile'` and asks `Did you forget to run actions/checkout before running your local action?`, the runner didn't find the composite action in the checked-out files. Check that the file is at `.github/actions/setup-build/action.yml`, and that **Check out the repository** comes before **Set up .NET and restore** in `reusable-build.yml`.

If **Set up .NET and restore** fails with `Required property is missing: shell`, a `run` step in `action.yml` has no `shell`. Add `shell: bash` above its `run` line, as in [the finished composite action](../../solutions/reusable-workflows/setup-build/action.yml).

If **Check for the practice secret** prints `practice-secret is empty or wasn't passed.` in **Release build / Build** although you added the secret, check the secret's name on the **Secrets** tab and in `build-both.yml`. Both must be `PRACTICE_SECRET`. A secret name that doesn't exist gives an empty value, not an error. If you added the secret after the run, start the workflow again. The steps are in [Add a practice secret](#add-a-practice-secret).

## Clean up

Nothing in this lab keeps running, so the three files can stay. The workflows only run when you start **Build Release and Debug**, and the artifacts expire after a day, because the reusable workflow sets `retention-days: 1`. When you no longer need the practice secret, select the **Settings** tab, then **Secrets and variables** in the left sidebar, then **Actions**, and delete `PRACTICE_SECRET` on the **Secrets** tab.

## Next step

The optional labs can be done in any order, so pick your next one from [the lesson list](../../README.md). If you haven't done it yet, [Lab 5: Pass files between jobs](jobs-and-artifacts.md) goes further into `needs`, job outputs, and artifacts, which the `summary` job used here.
