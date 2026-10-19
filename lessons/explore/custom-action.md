---
title: "Lab 16: Build your own action"
summary: "Write a JavaScript action with inputs and outputs, call it from a workflow, release it with a version tag, and compare it with composite and Docker actions."
duration: 40
difficulty: Intermediate
order: 16
---

# Lab 16: Build your own action

Every `uses:` line you've written runs an action someone else built. In this lab, you'll build one: a small JavaScript action that counts this workshop's lessons and adds up how long they take. It has an input, two outputs, and a job summary, and it runs on Node.js without installing anything. You'll call it from a workflow, release it with a version tag the way published actions are, and then compare it with the other two kinds of action, composite and Docker.

## Before you start

Finish the foundation labs, [Lab 0](../foundation/00-create-repository.md) to [Lab 2](../foundation/02-test-pull-request.md), first. They cover the workflow basics this lab builds on.

This lab works in the repository you created in Lab 0. Everything happens in your browser. The action is written in JavaScript, but you don't need to know JavaScript: the lab explains what each part does. [Lab 10](reusable-workflows.md) builds a composite action, and this lab compares the two, but you don't need to do Lab 10 first.

If your repository is public, the runs are free. If it's private, they use your account's included Actions minutes. Each run takes under a minute, so the lab uses about 3 of the 2,000 minutes a month that GitHub Free includes. See [Actions runner pricing](https://docs.github.com/en/billing/reference/actions-runner-pricing).

## What an action is

An action is a folder with a metadata file, `action.yml`, that says what inputs the action takes, what outputs it sets, and how to run it. There are three kinds, depending on that last part:

- A JavaScript action runs a script with the Node.js that's built into the runner.
- A composite action runs a list of steps, like a job does.
- A Docker container action builds or pulls an image and runs it.

An action can live in its own repository, which is how actions like `actions/checkout` are shared, or in a folder of the repository that uses it. You'll start with a folder in yours: `.github/actions/count-lessons`.

## Create the action

### Add the metadata file

1. Select the **Code** tab of your repository, then **Add file**, then **Create new file**.
2. In the file name box, type `.github/actions/count-lessons/action.yml`.
3. Copy this into the editor.

```yaml source=solutions/custom-action/count-lessons/action.yml
name: Count lessons
description: Counts the lessons in a folder and adds up how long they take.

inputs:
  folder:
    description: The folder to search for lessons, including its subfolders.
    required: false
    default: lessons

outputs:
  count:
    description: The number of lessons found.
  minutes:
    description: The total duration of the lessons, in minutes.

runs:
  using: node24
  main: index.js
```

4. Select **Commit changes**, then **Commit changes** again in the dialog to commit to `main`.

What's in the file:

- `name` and `description` describe the action. GitHub shows them when someone browses for actions.
- `inputs` lists what a workflow can pass in with `with:`. `folder` is optional, and if a workflow leaves it out, the runner uses `default`.
- `outputs` lists the values the action sets for later steps to read.
- `runs` says how to run it: `using: node24` means a JavaScript action on Node.js 24, and `main` is the script, relative to the folder.

### Add the script

1. Select **Add file**, then **Create new file**.
2. In the file name box, type `.github/actions/count-lessons/index.js`.
3. Copy this into the editor.

```javascript source=solutions/custom-action/count-lessons/index.js
const fs = require("node:fs");
const path = require("node:path");

function getInput(name) {
  return (process.env[`INPUT_${name.toUpperCase()}`] || "").trim();
}

function setOutput(name, value) {
  fs.appendFileSync(process.env.GITHUB_OUTPUT, `${name}=${value}\n`);
}

function readLesson(file) {
  const text = fs.readFileSync(file, "utf8");
  const frontMatter = text.match(/^---\r?\n([\s\S]*?)\r?\n---/);
  if (!frontMatter) {
    return null;
  }

  const title = frontMatter[1].match(/^title:\s*"?(.*?)"?\s*$/m);
  const duration = frontMatter[1].match(/^duration:\s*(\d+)\s*$/m);
  const order = frontMatter[1].match(/^order:\s*(\d+)\s*$/m);
  if (!title || !duration) {
    return null;
  }

  return {
    title: title[1],
    minutes: Number(duration[1]),
    order: order ? Number(order[1]) : Number.MAX_SAFE_INTEGER,
  };
}

const folder = getInput("folder") || "lessons";
if (!fs.existsSync(folder)) {
  console.log(`::error::The folder ${folder} doesn't exist. Check the folder input.`);
  process.exit(1);
}

const lessons = fs
  .readdirSync(folder, { recursive: true, withFileTypes: true })
  .filter((entry) => entry.isFile() && entry.name.endsWith(".md"))
  .map((entry) => readLesson(path.join(entry.parentPath, entry.name)))
  .filter((lesson) => lesson !== null)
  .sort((a, b) => a.order - b.order);

const minutes = lessons.reduce((total, lesson) => total + lesson.minutes, 0);
console.log(`Found ${lessons.length} lessons in ${folder}, ${minutes} minutes in total.`);

setOutput("count", lessons.length);
setOutput("minutes", minutes);

const rows = lessons.map((lesson) => `| ${lesson.title.replaceAll("|", "\\|")} | ${lesson.minutes} |`);
const summary = [
  "## Lessons",
  "",
  "| Lesson | Minutes |",
  "| --- | ---: |",
  ...rows,
  `| **Total** | **${minutes}** |`,
  "",
].join("\n");
fs.appendFileSync(process.env.GITHUB_STEP_SUMMARY, summary);
```

4. Commit directly to `main`.

The script talks to the runner the same way a `run` step does, through environment variables and files:

- **Inputs** arrive as environment variables. The runner sets `INPUT_FOLDER` from the `folder` input, so `getInput` reads `process.env.INPUT_FOLDER`.
- **Outputs** are lines of `name=value` appended to the file named in `GITHUB_OUTPUT`, as in a `run` step that writes `>> "$GITHUB_OUTPUT"`.
- **The job summary** is Markdown appended to the file named in `GITHUB_STEP_SUMMARY`. GitHub shows it on the run's summary page.
- **Errors** are workflow commands printed to the log. `::error::` adds an annotation, and a non-zero exit code fails the step.

The rest reads each `.md` file in the folder, picks `title`, `duration`, and `order` out of the front matter at the top, the same fields this dashboard reads, and sorts the lessons by `order`.

`require("node:fs")` and `require("node:path")` load modules built into Node.js, so there's nothing to install. Most published actions use GitHub's toolkit packages, such as `@actions/core`, which wrap these files in functions like `core.getInput` and `core.setOutput`. Because an action can't run `npm install` when it starts, those actions commit their dependencies, usually bundled into one file with a tool like `@vercel/ncc`. See [Creating a JavaScript action](https://docs.github.com/en/actions/tutorials/create-actions/create-a-javascript-action).

## Call the action

1. Select **Add file**, then **Create new file**.
2. In the file name box, type `.github/workflows/count-lessons.yml`.
3. Copy this workflow into the editor.

```yaml source=solutions/custom-action/count-lessons.yml
name: Count lessons

on:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  count:
    runs-on: ubuntu-latest
    timeout-minutes: 5
    steps:
      - name: Check out the repository
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          persist-credentials: false

      - name: Count the lessons
        id: lessons
        uses: $/.github/actions/count-lessons
        with:
          folder: lessons

      - name: Use the outputs
        env:
          COUNT: ${{ steps.lessons.outputs.count }}
          MINUTES: ${{ steps.lessons.outputs.minutes }}
        run: echo "The workshop has $COUNT lessons, about $MINUTES minutes of work."
```

4. Commit directly to `main`.

What's in the workflow:

- `uses: $/.github/actions/count-lessons` runs the action from this repository. `$/` means this repository, at the same commit as the workflow that's running, so you always get the action that matches the workflow.
- `with: folder: lessons` sets the input. It's the same as the default, so you could leave it out.
- **Use the outputs** reads them as `steps.lessons.outputs.count` and `steps.lessons.outputs.minutes`, where `lessons` is the step's `id`.
- The checkout is there for the lesson files the action reads, not for the action itself: GitHub fetches a `$/` action without a checkout.

You may see older workflows, including the ones in Lab 10, use `./` instead, as in `uses: ./.github/actions/count-lessons`. `./` is a path in the job's working folder, so it only works after a checkout, and it runs whatever is in that folder at that moment, even files an earlier step changed. `$/` always runs the committed action. Some tools that check workflows haven't caught up with `$/`: actionlint 1.7.12, from [Lab 13](debug-workflows.md), reports it as an error. See [Using an action in the same repository](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#example-using-an-action-in-the-same-repository-as-the-workflow-at-the-running-commit-recommended).

## Run it

1. Select the **Actions** tab, then **Count lessons** in the list of workflows.
2. Select **Run workflow**, then **Run workflow** in the menu.
3. Open the run when it appears.

The summary page shows a **Lessons** table, with every lab, its minutes, and a total. That's the job summary the action wrote. Open the `count` job:

- **Count the lessons** prints `Found`, the number of lessons, and the total minutes. Expand the step's first line to see `with: folder: lessons`.
- **Use the outputs** prints the same numbers, read from the action's outputs.

### See an error

1. Open `.github/workflows/count-lessons.yml`, select the pencil icon, and change `folder: lessons` to `folder: lesson`.
2. Commit to `main`, then run the workflow again.

**Count the lessons** fails, and the run's **Annotations** show `The folder lesson doesn't exist. Check the folder input.`, the `::error::` line from the script. **Use the outputs** is skipped because a step before it failed.

Change it back to `folder: lessons`, and commit.

## Release a version

A workflow in another repository can't use `$/`. It names the repository, the folder, and a version, such as `octo-org/tools/.github/actions/count-lessons@v1`. The version is usually a Git tag, which you create by publishing a release.

1. On the **Code** tab, select **Releases** on the right, then **Create a new release**. If there's no **Releases** section, go to `https://github.com/YOUR-USERNAME/YOUR-REPOSITORY/releases/new`.
2. Select **Choose a tag**, type `v1`, and select **Create new tag: v1 on publish**. Leave the target as `main`.
3. In **Release title**, type `v1`, then select **Publish release**.

Now call the action by its tag:

1. Edit `.github/workflows/count-lessons.yml` again.
2. Change the action's `uses:` line to name your repository and the tag, replacing `YOUR-USERNAME` and `YOUR-REPOSITORY`:

   ```text
           uses: YOUR-USERNAME/YOUR-REPOSITORY/.github/actions/count-lessons@v1
   ```

3. Commit to `main`, and run the workflow. It works as before. In the log, the **Set up job** step now has a line `Download action repository 'YOUR-USERNAME/YOUR-REPOSITORY@v1'`, which shows the runner fetching the tagged version.

The difference is which code runs. With `$/`, every commit runs the action as it is in that commit. With `@v1`, workflows keep running the commit `v1` points to, until you move the tag. That's how published actions work:

- Each release gets a full version tag, such as `v1.0.0`, `v1.1.0`, `v1.1.1`, that never moves.
- The major version tag, `v1`, moves to the latest `v1.x.y`, so people who use `@v1` get fixes. A change that could break their workflows, such as renaming an input, goes in `v2`.
- People who want to be sure the code never changes use the commit SHA instead, as the workshop's workflows do. See [the security lab](secure-workflows.md#keep-pinned-actions-current).

If your repository is private, other repositories can't use its actions until you allow it: **Settings**, **Actions**, **General**, then **Access**. See [Sharing actions and workflows from your private repository](https://docs.github.com/en/actions/how-tos/reuse-automations/share-with-your-organization).

### Publishing to GitHub Marketplace

You won't publish this action, but this is what it would take. GitHub Marketplace lists actions anyone can find and use. To publish one:

- The repository must be public.
- The action's `action.yml` must be at the root of the repository, and there can be only one there. That's why published actions each have a repository of their own.
- The `name` in `action.yml` must be unique on Marketplace, and can't match a GitHub user, organization, category, or feature.
- You accept the GitHub Marketplace Developer Agreement, and publishing needs two-factor authentication.

Then you publish a release as you did above, and select **Publish this Action to the GitHub Marketplace**. Marketplace listings also use `branding` from `action.yml`, an icon and a color. See [Publishing actions in GitHub Marketplace](https://docs.github.com/en/actions/how-tos/create-and-publish-actions/publish-in-github-marketplace).

## Other kinds of action

### Docker container actions

A Docker container action runs a container image. This version of `count-lessons` would use a shell script instead of JavaScript. You won't create it. The metadata points `runs` at a Dockerfile:

```yaml
name: Count lessons
description: Counts the lessons in a folder.

inputs:
  folder:
    description: The folder to search for lessons.
    default: lessons

outputs:
  count:
    description: The number of lessons found.

runs:
  using: docker
  image: Dockerfile
  args:
    - ${{ inputs.folder }}
```

The Dockerfile builds the image:

```text
FROM alpine:3.22
COPY entrypoint.sh /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
```

And `entrypoint.sh` gets the input as its first argument:

```bash
#!/bin/sh
set -eu
count=$(grep -rl --include='*.md' '^duration:' "$1" | wc -l)
echo "count=$count" >> "$GITHUB_OUTPUT"
```

The runner builds the image each time a job uses the action, which adds time, and Docker container actions only run on Linux runners. In exchange, the action can use any language or tool you can install in an image. To skip the build, an action can use `image: docker://` and a published image instead. See [Creating a Docker container action](https://docs.github.com/en/actions/tutorials/use-containerized-services/create-a-docker-container-action).

### Choosing a kind

| Kind | Runs | Good for | Watch out for |
| --- | --- | --- | --- |
| Composite | Steps, like a job | Reusing a few `run` and `uses` steps, as Lab 10 does | Logic is limited to what steps and expressions can do |
| JavaScript | Node.js on the runner | Logic, API calls, and anything that should work on Linux, Windows, and macOS | Dependencies have to be committed, usually bundled |
| Docker | A container image | Tools in other languages, or a fixed environment | Linux only, and slower to start |

## What happened

You built a JavaScript action with one input and two outputs, and it read the input from `INPUT_FOLDER`, wrote outputs to `GITHUB_OUTPUT`, wrote a job summary to `GITHUB_STEP_SUMMARY`, and reported an error with `::error::`. That's the same contract a `run` step uses, so anything you've learned about those files in earlier labs works in an action too. You ran it from the same repository with `$/`, then released `v1` and ran it by its tag, the way other repositories would.

## Check your result

- The **Count lessons** run's summary page shows a **Lessons** table with a total.
- **Use the outputs** prints the number of lessons and minutes.
- The **Releases** page of your repository lists `v1`, and the latest run used `@v1`.

## If something fails

If **Count the lessons** fails with `Can't find 'action.yml'` or a similar message, check that the folder is `.github/actions/count-lessons` and the files are `action.yml` and `index.js`, all lowercase.

If the run fails with `Unexpected token` or `SyntaxError`, part of `index.js` didn't copy. Compare it with [the reference script](../../solutions/custom-action/count-lessons/index.js).

If **Use the outputs** prints empty numbers, check that the action step has `id: lessons`, and that the expressions say `steps.lessons.outputs`.

If the run with `@v1` fails with `Unable to resolve action`, check the repository name and the tag. The release must be published, not a draft. In a private repository, also check that the workflow is in the same repository as the action, or that **Access** allows it.

## Clean up

To stop using the tag, change the `uses:` line back to `$/.github/actions/count-lessons`. If you did the security lab, zizmor reports `@v1` as `unpinned-uses`, so switching back keeps it passing. The workflow only runs when you start it, so it costs nothing to keep. To remove the release, open it on the **Releases** page, and delete it, then delete the `v1` tag on the **Tags** tab.

## Next step

To run tests against a real database in a workflow, try [the service containers lab](service-containers.md). Or pick another lab from [the lesson list](../../README.md).
