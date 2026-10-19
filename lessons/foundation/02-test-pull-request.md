---
title: "Lab 2: Run tests on pull requests"
summary: "Add a workflow that runs the unit tests whenever someone opens or updates a pull request."
duration: 20
difficulty: Beginner
order: 2
---

# Lab 2: Run tests on pull requests

Your repository has a small `Calculator` class and a few unit tests for it. You'll add a workflow that runs those tests on every pull request, then break the class on purpose to see what a failing check looks like.

## Before you start

You need the repository you created in [Lab 0](00-create-repository.md). This lab uses the run, job, and step pages you saw in [Lab 1](01-build-application.md).

## The code you'll test

`src/WorkshopCore/Calculator.cs` adds and subtracts two numbers:

```csharp source=src/WorkshopCore/Calculator.cs
namespace WorkshopCore;

// The dashboard uses Add to total lesson durations.
// In Lab 2, you break Subtract on purpose and watch a test fail.
public static class Calculator
{
    public static int Add(int a, int b)
    {
        return a + b;
    }

    public static int Subtract(int a, int b)
    {
        return a - b;
    }
}
```

The tests are in `tests/WorkshopCore.Tests/CalculatorTests.cs`. Each one calls a method and checks the result. Here's the test for `Subtract`:

```csharp excerpt=tests/WorkshopCore.Tests/CalculatorTests.cs
[Fact]
public void Subtract_ReturnsTheDifference()
{
    int result = Calculator.Subtract(10, 4);

    Assert.Equal(6, result);
}
```

`[Fact]` marks the method as a test for xUnit, the test framework this project uses. `Assert.Equal(6, result)` fails the test if `Subtract(10, 4)` returns anything other than 6.

## Create a branch

You'll make your changes on a branch, so `main` doesn't change until you merge them.

1. Select the **Code** tab of your repository.
2. Select the branch menu above the list of files. It shows `main`.
3. In the **Find or create a branch...** box, type `add-tests`, then select **Create branch**.

The branch menu now shows `add-tests`.

## Add the workflow

1. Make sure the branch menu shows `add-tests`.
2. Select **Add file**, then **Create new file**.
3. In the file name box, type `.github/workflows/tests.yml`. Each `/` you type starts a new folder.
4. Copy this workflow into the editor. YAML uses indentation to show structure, so keep the spaces exactly as they are.

```yaml source=solutions/tests/tests.yml
name: Tests

on: pull_request

permissions:
  contents: read

jobs:
  test:
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - name: Check out the repository
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1

      - name: Set up .NET
        uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json

      - name: Run the tests
        run: dotnet test tests/WorkshopCore.Tests
```

5. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `add-tests` branch, then select **Commit changes**.

### What's in the workflow

- `name: Tests` names the workflow. You'll see it in the **Actions** sidebar and on the pull request's **Checks** tab.
- `on: pull_request` is the trigger. GitHub runs the workflow when a pull request is opened or reopened, and each time someone pushes a commit to the pull request's branch.
- `permissions` sets what the run's `GITHUB_TOKEN` can do. The job only reads the code, so it gets `contents: read` and nothing else.
- `jobs` lists the workflow's jobs. There's one, with the ID `test`. It has no `name`, so the check on the pull request is called `test` too.
- `runs-on: ubuntu-latest` runs the job on a GitHub-hosted Ubuntu runner, and `timeout-minutes: 10` cancels the job if it's still running after 10 minutes, like the build in Lab 1.
- `steps` lists what the job does, in order, on that runner.
- `uses` runs an action, pinned to a commit. `actions/checkout` copies the code onto the runner, and `actions/setup-dotnet` installs the .NET SDK.
- `with` passes inputs to the action in the same step. `global-json-file: global.json` tells `setup-dotnet` which SDK version the repository needs.
- `run` runs a shell command. `dotnet test tests/WorkshopCore.Tests` builds the test project and the `Calculator` class, then runs the tests. If a test fails, the command exits with an error code, which fails the step, the job, and the check.

Committing the file doesn't start the workflow. It only runs for pull requests, and you haven't opened one yet.

## Open a pull request

1. Select the **Pull requests** tab.
2. GitHub shows a banner for the branch you just changed. Select **Compare & pull request**.

   If there's no banner, select **New pull request**, choose `add-tests` in the **compare** menu, and select **Create pull request**.

3. Check that the base branch is `main`. You can keep the title GitHub suggests.
4. Select **Create pull request**.

## Watch the check

1. Select the **Checks** tab of the pull request. It lists the **Tests** workflow, with the `test` job under it. If the list is empty, wait a few seconds and refresh the page.
2. Select `test` to follow its log. The job takes about half a minute.
3. When `test` has a green check mark, expand **Run the tests**. The last line of its output says that all four tests passed:

```text
Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4, Duration: 60 ms - WorkshopCore.Tests.dll (net10.0)
```

The pull request's **Conversation** tab shows the result too, above the **Merge pull request** button.

## Make a test fail

Next, break `Subtract` on the same branch.

1. Select the **Code** tab, and switch the branch menu to `add-tests`.
2. Select `src`, then `WorkshopCore`, then `Calculator.cs`.
3. Select the pencil icon to edit the file.
4. In `Subtract`, change `return a - b;` to `return a + b;`.
5. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `add-tests` branch, then select **Commit changes**.
6. Select the **Pull requests** tab, then your pull request. The `test` check runs again.

## Find the failure in the log

1. When `test` finishes with a red X, select the **Checks** tab, then `test`.
2. The **Run the tests** step failed. Type `Failed` in **Search logs** to find the failure, or expand the step and scroll down.

The log names the test that failed and says why:

```text
  Failed WorkshopCore.Tests.CalculatorTests.Subtract_ReturnsTheDifference [5 ms]
  Error Message:
   Assert.Equal() Failure: Values differ
Expected: 6
Actual:   14
```

`Subtract_ReturnsTheDifference` expects `Subtract(10, 4)` to return 6. After your change, it returns 10 + 4, which is 14.

The step ends with `Process completed with exit code 1.` When a test fails, `dotnet test` exits with a nonzero code, and a nonzero exit code is what makes a `run` step fail.

## Fix the code

1. On the `add-tests` branch, edit `src/WorkshopCore/Calculator.cs` again and change `return a + b;` in `Subtract` back to `return a - b;`.
2. Commit the change directly to `add-tests`.
3. Go back to your pull request and wait for `test` to pass.

## Merge the pull request

On the pull request's **Conversation** tab, select **Merge pull request**, then **Confirm merge**.

`tests.yml` is now on `main`, so the tests run for every pull request you open from now on. Merging doesn't start a run, because the workflow only runs for pull requests.

## What happened

Opening the pull request started the first run, and each commit you made on `add-tests` after that started another one. By default, `on: pull_request` starts the workflow for three kinds of pull request activity: `opened`, `reopened`, and `synchronize`. A new commit on the pull request's branch is a `synchronize`. The pull request shows the result of the latest run.

The check didn't test your branch on its own. GitHub ran the workflow on a merge commit that combines your branch with the current `main`, so the result tells you whether the tests would pass after the merge.

A failing check warns reviewers, but it doesn't block merging. Anyone who can merge the pull request still can. A repository administrator can make the `test` check required with a branch ruleset, and then a pull request can't be merged until the check passes. Rulesets are available in public repositories on GitHub Free, and in private repositories on GitHub Pro, Team, and Enterprise Cloud. See [About rulesets](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/about-rulesets).

## Check your result

Select the **Actions** tab, then **Tests** in the left sidebar. The list shows the runs for your pull request, newest first: a run that passed after you fixed `Subtract`, and below it, a run that failed after you broke it. Your `main` branch now has the file `.github/workflows/tests.yml`.

## If something fails

If no check appears on the pull request and the **Actions** tab shows a failed run named `.github/workflows/tests.yml`, GitHub couldn't read the workflow file. Open that run. Under **Annotations**, an **Invalid workflow file** error names the line with the problem. The usual cause is indentation that changed when you pasted the YAML. Compare your file with [solutions/tests/tests.yml](../../solutions/tests/tests.yml), fix it on the `add-tests` branch, and commit. The check runs for the new commit.

If no run appears at all, check the file's path on the pull request's **Files changed** tab. GitHub only runs workflow files that are in the `.github/workflows` folder and end in `.yml` or `.yaml`, so the path must be exactly `.github/workflows/tests.yml`, with a dot before `github` and an `s` at the end of `workflows`. To fix it, edit the file on `add-tests`, correct the path in the file name box, and commit.

If the check didn't run again after your commit, the commit probably went to another branch. Open the pull request's **Commits** tab. If your commit isn't listed, switch the branch menu on the **Code** tab to `add-tests` and make the change again. If the commit went to `main`, also edit `Calculator.cs` on `main` and change `Subtract` back to `return a - b;`, so `main` doesn't keep the broken code.

## Next step

You've finished Labs 0 to 2. The other labs cover runners, triggers, deployments, and more. You can do them in any order, and each one lists what you need before you start. Pick one from [the lesson list](../../README.md).
