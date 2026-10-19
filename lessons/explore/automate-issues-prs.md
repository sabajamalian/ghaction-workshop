---
title: "Lab 14: Automate issues and pull requests"
summary: "Label pull requests by the files they change, welcome new issues, and answer a slash command in a comment, using the workflow's own token."
duration: 35
difficulty: Intermediate
order: 14
---

# Lab 14: Automate issues and pull requests

So far, your workflows have built and tested code. Workflows can also look after the repository itself: label a pull request, welcome someone who opens an issue, or answer a command typed in a comment. In this lab, you'll add three workflows that do those things. Each one writes to the repository with the token GitHub gives every run, so you'll also see how to give a job only the permissions it needs.

## Before you start

Finish the foundation labs, [Lab 0](../foundation/00-create-repository.md) to [Lab 2](../foundation/02-test-pull-request.md), first. They cover the workflow basics this lab builds on.

This lab works in the repository you created in Lab 0. Everything happens in your browser. The repository needs issues turned on. They're on for a repository created from a template, but if the **Issues** tab is missing, select **Settings**, and under **Features**, select **Issues**.

If your repository is public, the runs are free. If it's private, they use your account's included Actions minutes. Each run takes under a minute, and the lab starts about 10 of them, so it uses about 10 of the 2,000 minutes a month that GitHub Free includes. See [Actions runner pricing](https://docs.github.com/en/billing/reference/actions-runner-pricing).

## The workflow's token

Every run gets a token, `GITHUB_TOKEN`, that lets it call the GitHub API for its own repository. A workflow reads it as `${{ github.token }}` or `${{ secrets.GITHUB_TOKEN }}`, and GitHub revokes it when the job ends. The `permissions` block decides what the token can do. The workflows in this lab start with `permissions: {}`, which gives the token nothing, then give each job the permissions it needs. A job's `permissions` replace the workflow's, so each job gets exactly what's listed under it.

## Label pull requests by the files they change

`actions/labeler` adds labels to a pull request based on which files it changes. The rules live in a file of their own, `.github/labeler.yml`.

### Add the rules

1. Select the **Code** tab of your repository, then **Add file**, then **Create new file**.
2. In the file name box, type `.github/labeler.yml`.
3. Copy these rules into the editor.

```yaml source=solutions/automate-issues-prs/labeler.yml
documentation:
  - changed-files:
      - any-glob-to-any-file: ["**/*.md", "docs/**"]

workflows:
  - changed-files:
      - any-glob-to-any-file: ".github/workflows/**"

app:
  - changed-files:
      - any-glob-to-any-file: "src/**"

tests:
  - changed-files:
      - any-glob-to-any-file: "tests/**"
```

4. Select **Commit changes**, then **Commit changes** again in the dialog to commit to `main`.

Each top-level key is a label. Under it, `changed-files` with `any-glob-to-any-file` means: add this label when any changed file matches any of these patterns. `**` matches any number of folders, so `**/*.md` matches a Markdown file anywhere in the repository.

### Add the workflow

1. Select **Add file**, then **Create new file**.
2. In the file name box, type `.github/workflows/label-pull-requests.yml`.
3. Copy this workflow into the editor.

```yaml source=solutions/automate-issues-prs/label-pull-requests.yml
name: Label pull requests

on:
  pull_request_target:
    types: [opened, synchronize, reopened]

permissions: {}

jobs:
  label:
    runs-on: ubuntu-latest
    timeout-minutes: 5
    permissions:
      contents: read
      pull-requests: write
      issues: write
    steps:
      - name: Label by changed files
        uses: actions/labeler@bf12e9b00b37c5c0ca2b87b79b2daf7891dbda13 # v7.0.0
        with:
          sync-labels: true
```

4. Commit directly to `main`.

What's in the workflow:

- `pull_request_target` runs when something happens to a pull request, like `pull_request` does, but it runs the workflow from the base branch, `main`, instead of from the pull request. That's what lets it label pull requests from forks, which only get a read-only token under `pull_request`. It also means it doesn't run the pull request's code, which matters: [the security lab](secure-workflows.md) explains why checking out a pull request's code under `pull_request_target` is dangerous.
- `types` lists the activity that starts a run. `opened` and `reopened` are what they say, and `synchronize` means new commits were pushed to the pull request. Most events have activity types. Without `types`, `pull_request_target` runs on `opened`, `synchronize`, and `reopened` anyway, so listing them here just makes it visible.
- `contents: read` lets labeler read `.github/labeler.yml`, `pull-requests: write` lets it add labels, and `issues: write` lets it create a label that doesn't exist yet.
- `sync-labels: true` removes a label when the pull request no longer changes matching files.

### Try it

Because the workflow runs from `main`, it has to be on `main` before you open the pull request.

1. On the **Code** tab, open `README.md`, and select the pencil icon to edit it.
2. Add a line anywhere, for example `Labeled by a workflow.`
3. Select **Commit changes**. In the dialog, select **Create a new branch for this commit and start a pull request**, name the branch `try-labels`, and select **Propose changes**.
4. Select **Create pull request**.

Within a minute, the pull request gets a `documentation` label, and the **Label pull requests** check passes. Select the check's **Details** to see the run.

Leave the pull request open for now.

## Welcome new issues

The next workflow runs when someone opens an issue. It adds a `triage` label and posts a comment thanking the author.

1. Select the **Code** tab, then **Add file**, then **Create new file**.
2. In the file name box, type `.github/workflows/welcome-issues.yml`.
3. Copy this workflow into the editor.

```yaml source=solutions/automate-issues-prs/welcome-issues.yml
name: Welcome new issues

on:
  issues:
    types: [opened]

permissions: {}

jobs:
  welcome:
    runs-on: ubuntu-latest
    timeout-minutes: 5
    permissions:
      issues: write
    env:
      GH_TOKEN: ${{ github.token }}
      NUMBER: ${{ github.event.issue.number }}
      AUTHOR: ${{ github.event.issue.user.login }}
    steps:
      - name: Add the triage label
        run: |
          gh label create triage --repo "$GITHUB_REPOSITORY" \
            --color FBCA04 --description "Needs a first look" --force
          gh api "repos/$GITHUB_REPOSITORY/issues/$NUMBER/labels" -f "labels[]=triage"

      - name: Thank the author
        run: |
          gh api "repos/$GITHUB_REPOSITORY/issues/$NUMBER/comments" \
            -f body="Thanks for opening this issue, @$AUTHOR. It has the \`triage\` label until someone takes a look."
```

4. Commit directly to `main`.

What's in the workflow:

- `issues` with `types: [opened]` runs only when an issue is opened. Without `types`, it would also run when an issue is edited, labeled, closed, and so on.
- The job's `env` is shared by every step. `GH_TOKEN` is where the GitHub CLI, `gh`, looks for a token, and it's preinstalled on GitHub-hosted runners.
- `github.event` holds the event's payload, the details GitHub sends with it. For an `issues` event, `github.event.issue` is the issue, so `github.event.issue.number` is its number and `github.event.issue.user.login` is the author's username.
- `gh label create ... --force` creates the label, or updates it if it exists, so the step works every time.
- `gh api` calls the REST API with the token. `-f` adds a field to the request body. `labels[]=triage` sends a list with one item.
- The issue's author and number reach the script as environment variables, never as `${{ }}` inside `run`. The [inputs lab](triggers-and-inputs.md#pass-an-input-that-looks-like-a-command) shows why: text from an issue is chosen by whoever opened it.

### Try it

1. Select the **Issues** tab, then **New issue**.
2. Give it any title, for example `Try the welcome workflow`, and select **Create**.
3. Wait a few seconds, then refresh the page.

The issue has a `triage` label and a comment from **github-actions**, the name GitHub shows for `GITHUB_TOKEN`.

## Answer a slash command

The last workflow reacts to comments. When someone with write access comments `/ping` on an issue or pull request, it adds a rocket reaction and replies.

1. Select the **Code** tab, then **Add file**, then **Create new file**.
2. In the file name box, type `.github/workflows/slash-commands.yml`.
3. Copy this workflow into the editor.

```yaml source=solutions/automate-issues-prs/slash-commands.yml
name: Slash commands

on:
  issue_comment:
    types: [created]

permissions: {}

jobs:
  ping:
    if: >-
      startsWith(github.event.comment.body, '/ping') &&
      contains(fromJSON('["OWNER", "MEMBER", "COLLABORATOR"]'), github.event.comment.author_association)
    runs-on: ubuntu-latest
    timeout-minutes: 5
    permissions:
      issues: write
      pull-requests: write
    env:
      GH_TOKEN: ${{ github.token }}
      NUMBER: ${{ github.event.issue.number }}
      COMMENT_ID: ${{ github.event.comment.id }}
      AUTHOR: ${{ github.event.comment.user.login }}
      RUN_URL: ${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}
    steps:
      - name: React to the comment
        run: |
          gh api "repos/$GITHUB_REPOSITORY/issues/comments/$COMMENT_ID/reactions" \
            -f content=rocket

      - name: Reply
        run: |
          gh api "repos/$GITHUB_REPOSITORY/issues/$NUMBER/comments" \
            -f body="Pong, @$AUTHOR. This reply came from [a workflow run]($RUN_URL)."
```

4. Commit directly to `main`.

What's in the workflow:

- `issue_comment` runs for comments on issues and on pull requests, because GitHub treats a pull request as an issue with code. `github.event.issue` is the issue or pull request, and `github.event.comment` is the comment. Like `pull_request_target`, it always runs the workflow from `main`.
- The job's `if` has two parts. `startsWith` checks that the comment begins with `/ping`. `author_association` says how the commenter relates to the repository, and the job only runs for `OWNER`, `MEMBER`, or `COLLABORATOR`. Without that check, anyone who can comment on a public repository could start runs.
- `>-` is YAML for a long value written across several lines. The lines are joined with spaces, so the `if` is one expression.
- `issues: write` lets the job comment on issues, and `pull-requests: write` lets it comment on pull requests.

### Try it

1. Open the issue you created.
2. Add a comment that says `/ping`.
3. Wait a few seconds, then refresh the page.

Your comment has a rocket reaction, and **github-actions** has replied with a link to the run. Now comment something else, such as `Thanks`. On the **Actions** tab, **Slash commands** has a new run for that comment too, but its `ping` job was skipped. The event always starts a run, and the `if` decides whether the job does anything.

## What happened

All three workflows use events about the repository itself rather than pushes of code, and each narrows the event with `types`. They use the run's own token, and each job asks only for the permissions it needs.

### Events from the workflow's token don't start runs

The welcome workflow's comment and the slash command's reply were made with `GITHUB_TOKEN`, and neither started a new **Slash commands** run, even though both are new comments. GitHub doesn't start runs for events caused by `GITHUB_TOKEN`, apart from `workflow_dispatch` and `repository_dispatch`. That prevents a workflow from triggering itself in a loop. If you need a workflow's change to start another workflow, use a GitHub App token instead, or call the other workflow directly. See [Triggering a workflow from a workflow](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow#triggering-a-workflow-from-a-workflow).

### Pull requests from forks

When someone without write access opens a pull request from a fork, a `pull_request` workflow gets a read-only token and no secrets, whatever its `permissions` say. That's because the workflow runs the fork's code, and that code could do anything with a write token. `pull_request_target` and `issue_comment` get a write token because they run the workflow from `main`, which only people with write access can change. That's safe as long as the workflow doesn't run the pull request's code or treat its text as commands. [The security lab](secure-workflows.md) goes further.

### Other events to automate

The same pattern works for many other events, for example `pull_request_review` to react to approvals, `discussion` for discussions, `release` when you publish a release, and `schedule` to clean up stale issues on a timer. See [Events that trigger workflows](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows).

## Check your result

- The pull request from `try-labels` has a `documentation` label and a passing **Label pull requests** check.
- Your issue has a `triage` label, a welcome comment, and a reply to your `/ping` comment.
- On the **Actions** tab, **Slash commands** has one run with a successful `ping` job and one where `ping` was skipped.

## If something fails

If the pull request gets no label and no **Label pull requests** check, check that `.github/labeler.yml` and the workflow were both committed to `main` before you opened the pull request. If they weren't, close the pull request and open a new one.

If **Label by changed files** fails with `Resource not accessible by integration`, the token is missing a permission. Check that the job's `permissions` lists all three, indented under `permissions:`, and that `permissions:` lines up with `runs-on:`.

If the welcome comment appears but there's no label, open the run, and check **Add the triage label**. An error about `labels[]` usually means a missing quote or backslash in the copy.

If `/ping` gets no reply, open the **Slash commands** run on the **Actions** tab. A skipped `ping` job means the `if` was false: check that the comment starts with `/ping` and that you're the repository's owner or a collaborator. If there's no run at all, check that the workflow is on `main`.

## Clean up

Close the `try-labels` pull request without merging it, and delete its branch. Close the issue too.

The three workflows keep running on new pull requests, issues, and comments. To stop them, delete the files from `.github/workflows`, or on the **Actions** tab, select a workflow, then the **...** menu, then **Disable workflow**.

## Next step

To make sure workflows like these can't be turned against your repository, try [the security lab](secure-workflows.md). Or pick another lab from [the lesson list](../../README.md).
