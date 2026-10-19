---
title: "Lab 5: Pass files between jobs"
summary: "Split a build into parallel jobs, pass the published app to a dependent job as an artifact, and cache NuGet packages between runs."
duration: 30
difficulty: Intermediate
order: 5
---

# Lab 5: Pass files between jobs

The workflow you ran in Lab 1 does all its work in one job. Here you'll add a workflow with three: `build` publishes the app and uploads it as an artifact, `test` runs the unit tests at the same time, and `verify` waits for both, downloads the artifact, starts the app, and checks that it answers. Along the way, you'll pass a value from one job to another and cache NuGet packages between runs.

## Before you start

Finish the foundation labs, [Lab 0](../foundation/00-create-repository.md) to [Lab 2](../foundation/02-test-pull-request.md), first. They cover the workflow basics this lab builds on.

This lab works in the repository you created in Lab 0. It uses the run, job, and step pages you saw in [Lab 1](../foundation/01-build-application.md), and its optional part repeats the branch and editing steps from [Lab 2](../foundation/02-test-pull-request.md). Everything happens in your browser.

If your repository is public, the runs are free. If it's private, they use your account's included Actions minutes, and GitHub rounds each job up to a whole minute. A run of this workflow has three short jobs, so it counts as about 3 minutes, and the whole lab uses about 8 of the 2,000 minutes a month that GitHub Free includes.

## Add the workflow

1. Select the **Code** tab of your repository, and make sure the branch menu shows `main`.
2. Select **Add file**, then **Create new file**.
3. In the file name box, type `.github/workflows/build-and-verify.yml`.
4. Copy this workflow into the editor. Keep the indentation exactly as it is.

```yaml source=solutions/jobs-and-artifacts/build-and-verify.yml
name: Build and verify

on:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  build:
    runs-on: ubuntu-latest
    timeout-minutes: 10
    outputs:
      version: ${{ steps.version.outputs.version }}
    steps:
      - name: Check out the repository
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1

      - name: Set up .NET
        uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json

      - name: Cache NuGet packages
        uses: actions/cache@55cc8345863c7cc4c66a329aec7e433d2d1c52a9 # v6.1.0
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj', 'global.json') }}

      - name: Restore dependencies
        run: dotnet restore src/WorkshopDashboard/WorkshopDashboard.csproj

      - name: Publish the dashboard
        run: dotnet publish src/WorkshopDashboard/WorkshopDashboard.csproj --configuration Release --no-restore --output publish

      - name: Read the version
        id: version
        run: |
          version=$(dotnet msbuild src/WorkshopDashboard/WorkshopDashboard.csproj -getProperty:Version)
          echo "Version: $version"
          echo "version=$version" >> "$GITHUB_OUTPUT"

      - name: Upload the published app
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: workshop-dashboard
          path: publish
          if-no-files-found: error
          retention-days: 1

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

  verify:
    needs: [build, test]
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - name: Set up .NET
        uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          dotnet-version: '10.0.x'

      - name: Download the published app
        uses: actions/download-artifact@9000827ccba6bdab643e8b6fd33ac0654aef8333 # v8.0.2
        with:
          name: workshop-dashboard
          path: app

      - name: Start the app
        working-directory: app
        run: dotnet WorkshopDashboard.dll --urls http://localhost:5080 > app.log 2>&1 &

      - name: Check the health endpoint
        working-directory: app
        env:
          EXPECTED_VERSION: ${{ needs.build.outputs.version }}
        run: |
          curl --silent --show-error --retry 10 --retry-delay 1 --retry-connrefused --output health.json http://localhost:5080/health
          jq . health.json
          jq --exit-status --arg version "$EXPECTED_VERSION" '.status == "healthy" and .version == $version' health.json
```

5. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `main` branch, then select **Commit changes**.

The file goes on `main` because you can only run a workflow manually when it's on the default branch. Committing it doesn't start a run, because `workflow_dispatch` is the only trigger.

### What's in the workflow

Most of the steps come from Lab 1's `build.yml` and Lab 2's `tests.yml`. These parts are new:

- `jobs` has three jobs: `build`, `test`, and `verify`. They have no `name`, so GitHub shows their IDs. Each job gets its own runner, a new virtual machine, so files that one job creates aren't there for the others. That's why `verify` downloads the app instead of looking for `build`'s `publish` folder.
- GitHub starts jobs in parallel unless you tell it otherwise. `build` and `test` don't depend on each other, so they run at the same time.
- `needs: [build, test]` makes `verify` wait until both jobs have finished successfully.
- **Cache NuGet packages** uses `actions/cache` for `~/.nuget/packages`, the folder where `dotnet restore` keeps the packages it downloads. The `key` names the cache: the runner's operating system, `nuget`, and one hash that `hashFiles` calculates from all the `.csproj` files and `global.json`. If a cache with that key exists, the step restores the folder. If not, the action saves the folder under that key at the end of the job, as long as the job succeeds. When a project file changes, for example to add a package, the hash changes too, so the next run misses the cache and saves a new one with the new packages.
- **Read the version** has an `id`, so expressions can refer to it as `steps.version`. It asks MSBuild for the project's `Version` property, prints it, and adds a `version=` line with the value to the file named by `$GITHUB_OUTPUT`. Each `name=value` line in that file becomes an output of the step.
- `outputs` under `build` turns that step output into a job output, also called `version`. A job that lists `build` in `needs` reads it as `needs.build.outputs.version`.
- `retention-days: 1` keeps the artifact for one day. `verify` needs it only during the run, and a day leaves you time to download it yourself. Lab 1's workflow keeps its artifact for 7 days, and without `retention-days`, an artifact follows the repository's retention setting, which is 90 days by default.
- `verify` doesn't check out the repository, because it only needs the published app. Without the repository, there's no `global.json`, so `dotnet-version: '10.0.x'` tells `setup-dotnet` to install the latest .NET 10 SDK, which includes the runtime the app needs.
- **Download the published app** asks for the artifact by the `name` that `build` gave it. When you give a `name`, the action extracts the files straight into the `path` folder, `app`, without adding a folder for the artifact.
- `working-directory: app` runs the step's commands in the `app` folder.
- **Start the app** runs `dotnet WorkshopDashboard.dll`, and `--urls http://localhost:5080` sets the address the app listens on. The `&` at the end starts the app in the background, so the step finishes while the app keeps running. `> app.log 2>&1` writes the app's own output to a file instead of the step's log. Nothing in the workflow stops the app, because the runner does that when the job ends: the job's **Complete job** step logs a `Terminate orphan process` line for it.
- **Check the health endpoint** gets the version from `build` through `env`, as `EXPECTED_VERSION`. `curl` requests the app's `/health` endpoint and saves the JSON response in `health.json`. With `--retry-connrefused`, it tries again once a second, up to 10 times, while the app is still starting. `jq . health.json` prints the response, and the last command checks that the app reports `healthy` and the version that `build` read. With `--exit-status`, `jq` exits with an error code when that check is `false`, which fails the step.

## Run the workflow

1. Select the **Actions** tab.
2. In the left sidebar, select **Build and verify**.
3. Select **Run workflow**. In the menu that opens, leave the branch set to `main`, then select the green **Run workflow** button.
4. When the new run appears at the top of the list, select it. If it doesn't appear, refresh the page.

The run takes about a minute.

## Read the job graph

The run's **Summary** shows a graph of its jobs, and it updates while the run is going. Each job has a status icon to the left of its name, and the lines between jobs show which job depends on which. Here, lines connect `build` and `test` to `verify`.

`build` and `test` start together, and `verify` waits until both have finished. To open a job's log, select the job in the graph, or under **Jobs** in the left sidebar.

## Follow the version between jobs

1. Select `build`, and expand **Read the version**. It prints `Version: 1.0.0`. The project doesn't set a version, so .NET uses its default.
2. Expand **Upload the published app**. Near the end, the line that starts with `SHA256 digest of uploaded artifact is` gives a fingerprint of the zip file that `build` uploaded.
3. Select `verify`, and expand **Download the published app**. The line that starts with `SHA256 digest of downloaded artifact is` shows the same digest, so `verify` got the exact file that `build` uploaded. If the digests didn't match, the step would fail.

   The same log might have a `DeprecationWarning` about `Buffer()`. It's a message from Node.js about code inside the action, and it doesn't affect the download.

4. Expand **Check the health endpoint**, then expand its first line, which starts with `Run curl`. Under `env`, `EXPECTED_VERSION: 1.0.0` is the version that `build` read, passed on as a job output.

The step's output might start with `curl: (7) Failed to connect to localhost port 5080`. That's expected when the app is still starting: `curl` waits a second and tries again. Then comes the JSON from `/health`. `status` is `healthy`, `version` is `1.0.0`, and `lessons` is the number of lessons the app found. `commit` and `buildTimestamp` say which commit `build` compiled and when, because the build wrote them into the app. The last line, `true`, is the result of the version check.

## Run the workflow again to use the cache

1. In your first run, select `build`, and expand **Cache NuGet packages**. There was no cache yet, so it says `Cache not found for input keys:`, followed by `Linux-nuget-` and a long hash.
2. Expand **Post Cache NuGet packages**, near the end of the job. It says `Cache saved with key:` and the same key.
3. Run the workflow again, the same way as before, and open the new run.
4. When the run finishes, select `build`, and expand **Cache NuGet packages**. This time it says `Cache restored from key:`. **Post Cache NuGet packages** now says `Cache hit occurred on the primary key`, then the key and `not saving cache.`, because a cache with that key already exists.

The dashboard depends on one small NuGet package, Markdig, so the cache saves very little time here. It makes a bigger difference in projects that restore many packages.

To see the cache, select the **Actions** tab, and in the left sidebar, under **Management**, select **Caches**. The list shows the `Linux-nuget-` cache, how much space it uses, and when it was created and last used.

The run's **Summary** lists `workshop-dashboard` under **Artifacts**, but it doesn't list the cache. Artifacts pass files between the jobs of a run, and you can download them until they expire. Caches keep files that rarely change, such as downloaded packages, so later runs don't have to fetch them again. GitHub deletes caches that go unused or don't fit in the repository's cache storage, so a job must still work when its cache is gone. See [Artifacts versus dependency caching](https://docs.github.com/en/actions/concepts/workflows-and-actions/workflow-artifacts#artifacts-versus-dependency-caching).

## See what happens when a job fails

This part is optional. You'll break `Subtract` on a new branch, as you did in Lab 2, and run the workflow on that branch.

1. Select the **Code** tab, then the branch menu above the list of files. In the **Find or create a branch...** box, type `break-subtract`, then select **Create branch**.
2. With the branch menu showing `break-subtract`, select `src`, then `WorkshopCore`, then `Calculator.cs`, and select the pencil icon to edit the file.
3. In `Subtract`, change `return a - b;` to `return a + b;`.
4. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `break-subtract` branch, then select **Commit changes**.
5. Select the **Actions** tab, then **Build and verify**, then **Run workflow**. In the menu, select the **Branch** dropdown and choose `break-subtract`, then select the green **Run workflow** button.
6. Open the new run, and wait for it to finish.

The run fails. Select `test` and expand **Run the tests**: `Subtract_ReturnsTheDifference` fails, as it did in Lab 2. `verify` was skipped, because a job it needs failed, so GitHub never started it. `build` doesn't depend on `test`, so it finished and uploaded the artifact anyway, and the **Summary** still lists `workshop-dashboard` under **Artifacts**.

In this run, `build`'s **Cache NuGet packages** step says `Cache restored from key:`, even though nothing on `break-subtract` saved a cache. A run can restore caches saved on its own branch and on the default branch, so this one used the cache from your runs on `main`.

## What happened

A job runs only after every job in its `needs` list has finished successfully. If one of them fails or is skipped, GitHub skips the job, along with any job that needs it. To run a job anyway, for example one that posts a report, add `if: ${{ always() }}` to it. See [Using jobs in a workflow](https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-jobs).

Job outputs are for short values, such as a version number or a commit SHA. GitHub evaluates them when the job ends, and a job can read only the outputs of the jobs listed in its `needs`. A job's outputs can total at most 1 MB, and all the outputs in a run 50 MB, so files belong in artifacts. See [Passing information between jobs](https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/pass-job-outputs).

By default, `download-artifact` only looks for artifacts in the current run, so `verify` always gets the app from the `build` job of the same run. Until the artifact expires, you can also download it from the run's **Summary**, as in Lab 1. In a private repository, artifacts count toward your account's storage until they expire (GitHub Free includes 500 MB), which is another reason to keep `retention-days` short.

Caches are shared between runs. A run can restore caches from its own branch and from the default branch, but not from other branches, so a run on `main` can't restore a cache saved for another branch. By default, GitHub deletes a cache that hasn't been used for 7 days, and when a repository's caches go over its limit, 10 GB by default, it deletes the ones used longest ago first. Anyone who can open a pull request against your repository can read the caches on the pull request's base branch, so never cache a folder that holds secrets. See [Dependency caching reference](https://docs.github.com/en/actions/reference/workflows-and-actions/dependency-caching).

## Check your result

Select the **Actions** tab, then **Build and verify** in the left sidebar. The list shows your two runs on `main` with green check marks and, if you did the optional part, a failed run on `break-subtract`. In the newer run on `main`, `build`'s **Cache NuGet packages** step says `Cache restored from key:`, and `verify`'s **Check the health endpoint** step ends with `true`.

## If something fails

If there's no **Run workflow** button, select **Build and verify** in the left sidebar first. If **Build and verify** isn't in the sidebar, check that the file is on `main` and that its path is exactly `.github/workflows/build-and-verify.yml`.

If a failed run named `.github/workflows/build-and-verify.yml` appears right after you commit the file, GitHub couldn't read the workflow file. Open that run. Under **Annotations**, an **Invalid workflow file** error names the line with the problem. The usual cause is indentation that changed when you pasted the YAML. Compare your file with [the solution file](../../solutions/jobs-and-artifacts/build-and-verify.yml), fix it on `main`, and commit.

If **Download the published app** fails with `Unable to download artifact(s): Artifact not found for name:` and a name, the `name` in that step doesn't match the `name` in **Upload the published app**. Both must be `workshop-dashboard`.

If the second run still says `Cache not found for input keys:`, check that the first run's `build` job succeeded and had finished before the second run started, because the cache is saved at the end of a successful job. The key also changes when a `.csproj` file or `global.json` changes, and a cache that isn't used for 7 days is deleted.

If `verify` is skipped in a run on `main`, a job it needs failed. Select that job in the graph and find the first step that failed.

## Clean up

If you did the optional part, delete the `break-subtract` branch: on the **Code** tab, select the branch menu, then **View all branches**, and select the trash icon next to `break-subtract`.

You don't need to delete anything else. The workflow only runs when you start it, each run's artifact expires after a day, and GitHub deletes the cache after 7 days without use. To delete the cache sooner, open **Caches** and select the trash icon next to it.

## Next step

A reusable workflow returns values to the workflow that calls it through outputs too, and each one comes from a job output like `version` here. To try that, go to [the reusable workflows lab](reusable-workflows.md), or pick another lab from [the lesson list](../../README.md).
