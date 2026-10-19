---
title: "Lab 13: Debug a broken workflow"
summary: "Fix a workflow with five planted problems, using error annotations, step logs, debug logging, and actionlint."
duration: 40
difficulty: Intermediate
order: 13
---

# Lab 13: Debug a broken workflow

Workflows break in a few common ways: the file doesn't load, a command fails, a step works on one operating system but not another, an expression comes out empty, or the job's token isn't allowed to do something. In this lab, you'll add a workflow that has one of each, then find and fix them one at a time. Along the way, you'll read error annotations, re-run a job with debug logging, and run actionlint, a linter that catches some mistakes before a workflow runs.

## Before you start

Finish the foundation labs, [Lab 0](../foundation/00-create-repository.md) to [Lab 2](../foundation/02-test-pull-request.md), first. They cover the workflow basics this lab builds on.

This lab works in the repository you created in Lab 0. It uses a matrix to run a job on Linux and Windows, which [Lab 3](runners.md) explains, but you don't need to do Lab 3 first. Everything happens in your browser.

If your repository is public, the runs are free. If it's private, they use your account's included Actions minutes. You'll push about six fixes, and most runs have a Linux job and a Windows job, so the lab uses about 25 of the 2,000 minutes a month that GitHub Free includes. A minute on Windows counts as about 1.7 Linux minutes. See [Actions runner pricing](https://docs.github.com/en/billing/reference/actions-runner-pricing).

## Create a practice branch

The broken workflow runs on every push to a branch named `debug-practice`, so each fix you commit starts a new run. Keeping it on its own branch keeps the failures off `main`.

1. Select the **Code** tab of your repository, then the branch menu above the list of files.
2. In the **Find or create a branch...** box, type `debug-practice`, then select **Create branch**.

## Add the broken workflow

1. With the branch menu showing `debug-practice`, select **Add file**, then **Create new file**.
2. In the file name box, type `.github/workflows/broken-ci.yml`.
3. Copy this workflow into the editor exactly as it is, mistakes included. It's also in your repository at `exercises/debug-workflows/broken-ci.yml`.

```yaml source=exercises/debug-workflows/broken-ci.yml
name: Broken CI

on:
  push:
    branches: [debug-practice]

permissions:
  contents: read

jobs:
  test:
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest]
    runs-on: ${{ matrix.os }}
    timeout-minutes: 10
    steps:
      - name: Check out the repository
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          persist-credentials: false

      - name: Set up .NET
        uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
      with:
          global-json-file: global.json

      - name: Show the tools
        run: |
          echo "::group::.NET SDKs"
          dotnet --list-sdks
          echo "::endgroup::"

      - name: Run the tests
        run: dotnet test tests/WorkshopCore.Test

      - name: List the test output
        run: ls -la tests/WorkshopCore.Tests/bin/Debug/net10.0

  report:
    needs: test
    runs-on: ubuntu-latest
    timeout-minutes: 5
    steps:
      - name: Shorten the commit SHA
        id: short-sha
        run: echo "sha=${GITHUB_SHA::7}" >> "$GITHUB_OUTPUT"

      - name: Post a commit status
        env:
          GH_TOKEN: ${{ github.token }}
          SHORT_SHA: ${{ steps.short_sha.outputs.sha }}
        run: |
          if [ -z "$SHORT_SHA" ]; then
            echo "::error::SHORT_SHA is empty, so there's nothing to report."
            exit 1
          fi
          gh api "repos/$GITHUB_REPOSITORY/statuses/$GITHUB_SHA" \
            -f state=success \
            -f context=debug-practice \
            -f description="Tests passed on Linux and Windows for $SHORT_SHA"
```

4. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `debug-practice` branch, then select **Commit changes**.

When it works, the workflow runs the unit tests on Linux and Windows, then a `report` job posts a commit status, the small check that GitHub shows next to a commit. `report` reads the shortened commit SHA from an earlier step's output, and it uses the GitHub CLI, `gh`, to call the REST API with the job's token.

The editor may already underline a problem. Leave it for now: the point is to see how GitHub reports it.

## Problem 1: the file doesn't load

1. Select the **Actions** tab. A new run named `.github/workflows/broken-ci.yml` has failed. GitHub uses the file name because it couldn't read the workflow's `name`.
2. Open the run. Under **Annotations**, an **Invalid workflow file** error says the YAML has a syntax error and gives a line number.

A run like this has no jobs and costs nothing: GitHub stopped before it started any runners.

In YAML, indentation is structure. The `with:` under **Set up .NET** starts at the same column as `- name:`, so the parser reads it as a new item in the list of steps, and an item there has to start with `-`. That's why the line the error names can be the line where the parser gave up, near the mistake rather than on it.

To fix it:

1. Select the **Code** tab, make sure the branch menu shows `debug-practice`, and open `.github/workflows/broken-ci.yml`. Select the pencil icon to edit it.
2. Add two spaces before `with:` under **Set up .NET**, so it lines up with `uses:` above it.
3. Select **Commit changes**, and commit directly to `debug-practice`.

## Problem 2: a step fails

Open the new run from the **Actions** tab. This time GitHub could read the file: the run is named **Broken CI**, and it has jobs. Both `test (ubuntu-latest)` and `test (windows-latest)` fail, and `report` is skipped because the job it needs failed.

1. Select `test (ubuntu-latest)`. The step with the red X is **Run the tests**, and GitHub has expanded it.
2. Read the last lines of the step:

   ```text
   MSBUILD : error MSB1009: Project file does not exist.
   Switch: tests/WorkshopCore.Test
   ```

3. The project folder is `tests/WorkshopCore.Tests`, with an `s`. Select the **Code** tab and open `tests` to check.

Fix the path in **Run the tests** to `tests/WorkshopCore.Tests`, and commit to `debug-practice`.

When a step fails, start with its last few lines, then scroll up to the first error. The summary page's **Annotations** list also shows each job's failure in one place.

Before you move on, expand **Show the tools** in either job. It prints one collapsed line, **.NET SDKs**. Select it to see the SDK list. `echo "::group::..."` and `echo "::endgroup::"` are workflow commands: lines that a step prints to tell the runner to do something, here to fold the lines between them. Groups keep long output out of the way until you need it.

## Problem 3: it fails on one runner only

In the next run, `test (ubuntu-latest)` passes and `test (windows-latest)` fails. Select the Windows job. **List the test output** failed with an error that ends:

```text
A parameter cannot be found that matches parameter name 'la'.
```

On Linux and macOS, `run` steps use Bash. On Windows, they use PowerShell, where `ls` is a short name for `Get-ChildItem`, which has no `-la` option. Fix it by telling the step which shell to use:

1. Edit `.github/workflows/broken-ci.yml` on `debug-practice`.
2. Under `- name: List the test output`, add a line `shell: bash`, indented to line up with `run:`.
3. Commit to `debug-practice`.

Windows runners have the Bash that comes with Git for Windows, so `ls -la` works there too. The other choice would be a separate step for each operating system, each with an `if` on `runner.os`, as Lab 3's workflow does.

The Linux job finished even though the Windows job failed because of `fail-fast: false`. Without it, a matrix cancels its other jobs when one fails, and you'd only see one problem.

## Problem 4: a value is empty

In the next run, both `test` jobs pass and `report` runs. It fails at **Post a commit status** with the message the script prints:

```text
Error: SHORT_SHA is empty, so there's nothing to report.
```

Expand the first line of the step, which starts with `Run if [ -z`. Under `env`, `SHORT_SHA:` has nothing after it. So the expression `${{ steps.short_sha.outputs.sha }}` came out empty, but nothing in the log says why. GitHub doesn't treat a missing value as an error: it uses an empty string.

### Re-run with debug logging

1. At the top of the run, select **Re-run jobs**, then **Re-run failed jobs**.
2. In the dialog, select **Enable debug logging**, then **Re-run jobs**.
3. Open the `report` job, and expand **Post a commit status** again.

The log now has extra lines that start with `##[debug]`. Find `##[debug]Evaluating: steps.short_sha.outputs.sha`. The lines under it show each part of the expression being looked up, and the last one, `##[debug]Result: null`, says the whole expression found nothing.

Compare it with the step above. **Shorten the commit SHA** has `id: short-sha`, with a hyphen, and the expression asks for `short_sha`, with an underscore. They're different IDs.

Debug logging adds two kinds of output. Step debug logs, the `##[debug]` lines, show how the runner evaluated expressions and conditions. Runner diagnostic logs add details about the runner itself, which you get by downloading the run's logs. The checkbox turns on both for one re-run. To turn them on for every run, add repository variables named `ACTIONS_STEP_DEBUG` and `ACTIONS_RUNNER_DEBUG` with the value `true`. See [Enabling debug logging](https://docs.github.com/en/actions/how-tos/monitor-workflows/enable-debug-logging).

### Lint the workflow

You found the typo by reading debug logs after a failed run. A linter can find it before the workflow runs. actionlint checks workflow files for syntax errors, unknown keys, and expressions that refer to things that don't exist, and it runs ShellCheck on `run` scripts.

1. With the branch menu showing `debug-practice`, select **Add file**, then **Create new file**.
2. In the file name box, type `.github/workflows/lint-workflows.yml`.
3. Copy this workflow into the editor.

```yaml source=solutions/debug-workflows/lint-workflows.yml
name: Lint workflows

on:
  push:
    branches: [debug-practice]

permissions:
  contents: read

jobs:
  actionlint:
    runs-on: ubuntu-latest
    timeout-minutes: 5
    steps:
      - name: Check out the repository
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          persist-credentials: false

      - name: Run actionlint
        uses: docker://rhysd/actionlint@sha256:b1934ee5f1c509618f2508e6eb47ee0d3520686341fec936f3b79331f9315667 # 1.7.12
        with:
          args: -color .github/workflows/broken-ci.yml
```

4. Commit directly to `debug-practice`.

`uses: docker://` runs a container image as a step, here actionlint's official image. The `@sha256:` digest pins the exact image, the same way a commit SHA pins an action. `args` is the command line passed to the image: `-color` keeps actionlint's colors in the log, and the file name limits it to `broken-ci.yml`.

The commit starts two runs, one for each workflow. Open the **Lint workflows** run and its `actionlint` job. **Run actionlint** fails with the problem and the line it's on:

```text
.github/workflows/broken-ci.yml:54:26: property "short_sha" is not defined in object type {short-sha: {conclusion: string; outcome: string; outputs: {string => string}}} [expression]
```

The **Broken CI** run fails the same way as before, because you haven't fixed anything yet. Fix the expression in `broken-ci.yml` to `${{ steps.short-sha.outputs.sha }}`, and commit to `debug-practice`.

actionlint can't catch the other problems in this lab. A path that doesn't exist, a command that behaves differently on Windows, and a missing permission are only found when the job runs.

## Problem 5: permission denied

In the next run, **Lint workflows** passes, and **Broken CI** fails in `report` at **Post a commit status**, this time in the `gh api` call:

```text
gh: Resource not accessible by integration (HTTP 403)
```

`Resource not accessible by integration` means the job's token was refused. The workflow's `permissions` block only grants `contents: read`, and creating a commit status needs `statuses: write`. Give that permission to the one job that needs it:

1. Edit `.github/workflows/broken-ci.yml` on `debug-practice`.
2. Under `report:`, after `timeout-minutes: 5`, add these two lines, indented the same way as `timeout-minutes`:

   ```yaml
   permissions:
     statuses: write
   ```

3. Commit to `debug-practice`.

A job's `permissions` replace the workflow's, rather than adding to them, so `report` now has `statuses: write` and nothing else. It doesn't need `contents: read`, because it doesn't check out the repository. Granting the permission only where it's needed means the `test` jobs, which run the repository's code, still can't write anything.

## See the result

Open the new **Broken CI** run. All three jobs pass. Then look at the status `report` posted:

1. Select the **Code** tab, and switch the branch menu to `debug-practice`.
2. Above the list of files, next to the latest commit's message, select the green check mark.
3. The list includes `debug-practice`, with the description `Tests passed on Linux and Windows for` and the first seven characters of the commit's SHA.

The finished workflow is in [solutions/debug-workflows/fixed-ci.yml](../../solutions/debug-workflows/fixed-ci.yml).

## What happened

You saw the places GitHub reports a problem, in the order you usually check them:

- In the run list, a run named after the file path instead of the workflow's `name` means GitHub couldn't load the file, and **Annotations** on the run's page says why.
- **Annotations** on the run's summary page collects errors and warnings from every job. Steps add their own with `::error::` and `::warning::`, as **Post a commit status** does.
- In the step log, the failed step is expanded for you. The first error is usually more useful than the last.
- The first line of each step expands to show the `env` values and inputs it received, which shows an empty value without any extra logging.
- Debug logging adds `##[debug]` lines that show how expressions and `if` conditions were evaluated.

### Conditions for diagnostics

Every step has an implicit `if: ${{ success() }}`, so it only runs when the steps before it passed. The status functions change that. `failure()` runs a step only after an earlier step failed, which suits steps that collect logs:

```yaml
- name: Show the test logs
  if: ${{ failure() }}
  run: find tests -name '*.log' -print -exec cat {} \;
```

`always()` runs a step whatever happened, even if the run was canceled, and `cancelled()` runs it only when the run was canceled. See [Status check functions](https://docs.github.com/en/actions/reference/workflows-and-actions/expressions#status-check-functions).

`continue-on-error: true` on a step lets the job carry on and pass even when that step fails. It's useful for a step whose result you want to see but not depend on, such as a new linter you're trying out. It also hides real failures, so don't use it to make a red job green. The step's `outcome` is still `failure`, so later steps can check it with `steps.<id>.outcome`.

### Read logs from a terminal

In a codespace or anywhere the GitHub CLI is signed in, `gh run list --workflow broken-ci.yml` lists the runs, and `gh run view <run-id> --log-failed` prints only the logs of the failed steps. That's quicker than the browser when a log is long. See [Using workflow run logs](https://docs.github.com/en/actions/how-tos/monitor-workflows/use-workflow-run-logs).

### Run actionlint before you push

actionlint is a single program, so you can also run it in a codespace or on your own computer before you commit. A workflow like `lint-workflows.yml` that runs on pull requests catches the mistakes in review, before they reach `main`. See [the actionlint documentation](https://github.com/rhysd/actionlint).

## Check your result

On the **Actions** tab, the latest **Broken CI** run on `debug-practice` has a green check mark and three passed jobs: `test (ubuntu-latest)`, `test (windows-latest)`, and `report`. The latest **Lint workflows** run passed too, and the branch's latest commit lists a `debug-practice` status.

## If something fails

If a run fails at a step this lab doesn't mention, compare your file with [the fixed workflow](../../solutions/debug-workflows/fixed-ci.yml). The only differences between the starter and the fixed file are the five fixes, so a difference anywhere else is a copying mistake.

If no run starts after you commit, check that the commit went to `debug-practice`, not `main`. The workflow only runs on pushes to `debug-practice`.

If **Re-run jobs** doesn't offer **Enable debug logging**, wait for the run to finish. You can only re-run a run that has completed.

If **Lint workflows** fails with `no such file or directory`, the path in `args` doesn't match your workflow's file name. Both must be `.github/workflows/broken-ci.yml`.

If **Post a commit status** still fails with `HTTP 403` after you add `permissions`, check that `permissions:` lines up with `runs-on:` under `report:`, and that `statuses: write` is indented two more spaces.

## Clean up

Delete the `debug-practice` branch: on the **Code** tab, select the branch menu, then **View all branches**, and select the trash icon next to `debug-practice`. Both workflows exist only on that branch, so they won't run again.

## Next step

To make workflows react to issues and pull requests, try [the automation lab](automate-issues-prs.md). To tighten the security of the workflows you've written, try [the security lab](secure-workflows.md). Or pick another lab from [the lesson list](../../README.md).
