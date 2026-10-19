---
title: "Lab 15: Secure your workflows"
summary: "Limit the workflow token, audit your workflows with zizmor, keep pinned actions current with Dependabot, and learn the patterns that let attackers into a workflow."
duration: 40
difficulty: Intermediate
order: 15
---

# Lab 15: Secure your workflows

A workflow runs code with access to your repository, and often to secrets and cloud accounts too. So a mistake in a workflow can do more damage than a mistake in the app it builds. In this lab, you'll make the workflow token read-only by default, run zizmor, a tool that audits workflows for security problems, and fix what it finds. Then you'll set up Dependabot to keep your pinned actions current, and learn the patterns attackers use against workflows so you can spot them in review.

## Before you start

Finish the foundation labs, [Lab 0](../foundation/00-create-repository.md) to [Lab 2](../foundation/02-test-pull-request.md), first. They cover the workflow basics this lab builds on.

This lab works in the repository you created in Lab 0. Everything happens in your browser. It builds on the script injection section of [Lab 4](triggers-and-inputs.md#pass-an-input-that-looks-like-a-command), and it mentions workflows from [Lab 14](automate-issues-prs.md), but you don't need to do either first.

If your repository is public, the runs are free. If it's private, they use your account's included Actions minutes. The audit takes about a minute and runs on each push to `main`, so the lab uses about 5 of the 2,000 minutes a month that GitHub Free includes. See [Actions runner pricing](https://docs.github.com/en/billing/reference/actions-runner-pricing).

## Make the token read-only by default

Every workflow in this workshop has a `permissions` block that says what its token, `GITHUB_TOKEN`, can do. A workflow without one gets the repository's default, and that default can be read and write for everything. Set it to read-only, so a workflow that forgets `permissions` can't write.

1. Select the **Settings** tab of your repository.
2. In the sidebar, select **Actions**, then **General**.
3. Under **Workflow permissions**, select **Read repository contents and packages permissions**.
4. Leave **Allow GitHub Actions to create and approve pull requests** cleared, then select **Save**.

A workflow that lists `permissions` still gets exactly what it lists, so none of your workflows change. The setting only affects workflows that don't say.

When you write a `permissions` block, start from nothing and add what the job needs. A few common ones:

| The job needs to | Permission |
| --- | --- |
| Check out the repository | `contents: read` |
| Push commits or create a release | `contents: write` |
| Comment on or label issues | `issues: write` |
| Comment on, label, or review pull requests | `pull-requests: write` |
| Post a commit status | `statuses: write` |
| Sign in to a cloud with OIDC | `id-token: write` |
| Upload code scanning results | `security-events: write` |

Set them on the job rather than the workflow when only one job needs them, as [Lab 13](debug-workflows.md) and Lab 14 do. A job's `permissions` replace the workflow's, so `permissions: {}` at the top and a short list on each job gives every job only what it uses. See [Use GITHUB_TOKEN for authentication in workflows](https://docs.github.com/en/actions/tutorials/authenticate-with-github_token).

## Audit your workflows with zizmor

zizmor reads workflow files and reports patterns that are known to be risky, such as script injection, dangerous triggers, and credentials left where other steps can reach them. It's a static analyzer: it reads the files and doesn't run them.

### Add the audit workflow

1. Select the **Code** tab, then **Add file**, then **Create new file**.
2. In the file name box, type `.github/workflows/zizmor.yml`.
3. Copy this workflow into the editor.

```yaml source=solutions/secure-workflows/zizmor.yml
name: Audit workflows

on:
  push:
    branches: [main]
  pull_request:
  workflow_dispatch:

permissions: {}

jobs:
  zizmor:
    runs-on: ubuntu-latest
    timeout-minutes: 10
    permissions:
      contents: read
    steps:
      - name: Check out the repository
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          persist-credentials: false

      - name: Run zizmor
        uses: zizmorcore/zizmor-action@85b2b9fb8e3325e7a23d7d0a9d7ba5ae0f999e3e # v0.7.1
        with:
          version: 1.31.1
          advanced-security: false
          annotations: true
```

4. Select **Commit changes**, then **Commit changes** again in the dialog to commit to `main`.

What's in the workflow:

- It runs on pushes to `main` and on pull requests, so a risky change is caught in review.
- `zizmorcore/zizmor-action` runs zizmor on every workflow in the repository. `version` pins zizmor itself, so a new release with new checks doesn't fail your build unexpectedly.
- `advanced-security: false` reports findings as annotations on the run, with `annotations: true`. With `true`, the action uploads them to the **Security** tab instead, which needs code scanning and `security-events: write`.
- The step fails when zizmor finds anything, so the check stays red until each finding is fixed or explained.

### Read the findings

1. Select the **Actions** tab, and open the **Audit workflows** run. It has failed.
2. Under **Annotations**, find two warnings titled `artipacked`, one for `.github/workflows/build.yml` and one for `.github/workflows/tests.yml`. Each says `credential persistence through GitHub Actions artifacts: does not set persist-credentials: false`.

If you've done other labs, you may see more findings, from the workflows you added there. Each one has a title you can look up in [zizmor's list of audits](https://docs.zizmor.sh/audits/).

`artipacked` is about `actions/checkout`. By default, checkout keeps the job's token in a credentials file on the runner, and the repository's `.git/config` points to it, so later `git` commands can push. That means every later step in the job can read the token, including any action or script you didn't write. Neither workflow pushes, so neither needs the token saved.

### Fix the findings

1. On the **Code** tab, open `.github/workflows/build.yml`, and select the pencil icon to edit it.
2. Under the **Check out the repository** step's `uses:` line, add these two lines, with `with:` lined up with `uses:`:

   ```yaml
   with:
     persist-credentials: false
   ```

3. Commit to `main`.
4. Make the same change to `.github/workflows/tests.yml`, and commit to `main`.

After the second commit, the **Audit workflows** run passes. Lab 1's **Build application** workflow and Lab 2's tests still work: checkout still downloads the code, it just doesn't keep the token.

### When a finding doesn't apply

A static analyzer can't always tell a risky workflow from a safe one. If you did Lab 14, zizmor also reports two errors titled `dangerous-triggers`: one for `.github/workflows/slash-commands.yml`, because `issue_comment` runs with a write token on comments from anyone, and one for `.github/workflows/label-pull-requests.yml`, because `pull_request_target` runs with a write token on pull requests from forks. Both are safe. The labeler never checks out or runs the pull request's code. The slash command workflow doesn't check out code, its `if` only lets collaborators through, and the comment's text never reaches a shell. When you've checked a finding like that, tell zizmor to ignore it, and say why on the same line. In `slash-commands.yml`:

```text
  issue_comment: # zizmor: ignore[dangerous-triggers] -- no checkout, and comment text never reaches a shell
```

And in `label-pull-requests.yml`:

```text
  pull_request_target: # zizmor: ignore[dangerous-triggers] -- never checks out or runs the pull request's code
```

The comment goes on the line zizmor reports. The reason is for the next person who reads the workflow: an ignore with no reason looks like someone silencing a warning. See [Ignoring results](https://docs.zizmor.sh/usage/#ignoring-results).

## Keep pinned actions current

Every action in this workshop is pinned to a full commit SHA, with the version in a comment:

```yaml
uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
```

A tag like `v7` can be moved to point at different code, by the action's maintainer or by anyone who takes over their account. A commit SHA can't. Pinning means you run the code you reviewed, but it also means you don't get fixes until you update the pin. Dependabot does that for you: it opens a pull request when a pinned action has a new release, and updates both the SHA and the comment.

1. Select the **Code** tab, then **Add file**, then **Create new file**.
2. In the file name box, type `.github/dependabot.yml`.
3. Copy this configuration into the editor.

```yaml source=solutions/secure-workflows/dependabot.yml
version: 2
updates:
  - package-ecosystem: github-actions
    directory: /
    schedule:
      interval: weekly
    cooldown:
      default-days: 7
    groups:
      actions:
        patterns: ["*"]
```

4. Commit directly to `main`.

What's in the file:

- `package-ecosystem: github-actions` with `directory: /` checks the actions used in `.github/workflows`.
- `schedule` checks once a week.
- `cooldown` waits 7 days after a release before suggesting it. If a release turns out to be malicious, that's usually found and pulled within days, before you'd adopt it.
- `groups` puts all the updates in one pull request instead of one each.

To see that Dependabot read the file, select the **Insights** tab, then **Dependency graph**, then **Dependabot**. The list shows `.github/dependabot.yml`, with a link to its last check. If your pins are already current, there's no pull request to open, so you may not see one until a new release comes out.

Dependabot's pull requests run your `pull_request` workflows like anyone else's, with a read-only token, and secrets come from a separate list, **Dependabot secrets**, rather than your Actions secrets. See [Keeping your actions up to date with Dependabot](https://docs.github.com/en/code-security/how-tos/secure-your-supply-chain/manage-your-dependency-security/keeping-your-actions-up-to-date-with-dependabot).

## Patterns to watch for

You won't build these. They're the mistakes behind most real workflow attacks, so you can recognize them in a review.

### Untrusted text in a script

[Lab 4](triggers-and-inputs.md#pass-an-input-that-looks-like-a-command) showed that `${{ }}` inside `run` is pasted into the script before it runs. If the value is text someone else chose, such as an issue title, a branch name, or a commit message, they can write shell commands into it. Pass it through `env` instead, and quote the variable.

### Running a pull request's code with a write token

`pull_request_target` runs the workflow from your default branch with a write token and your secrets, even for pull requests from forks. That's safe for a workflow like Lab 14's labeler, which never runs the pull request's code. It stops being safe the moment the workflow checks the pull request's code out and runs it. **Don't use this workflow**:

```yaml
on: pull_request_target

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          ref: ${{ github.event.pull_request.head.sha }}
      - run: dotnet test
```

Anyone can open a pull request that changes a test or a project file to read the token and secrets, then send them out or use them to push to your repository. `dotnet test` doesn't have to run anything obviously malicious: a build script, a test, or a package restore can run code. Use `pull_request` to build and test pull requests. It runs fork pull requests with a read-only token and no secrets. `issue_comment` and `workflow_run` have the same risk as `pull_request_target`, because they also run with a write token for events that outsiders can cause. zizmor reports all three as `dangerous-triggers`.

### Poisoned caches

[Lab 5](jobs-and-artifacts.md) caches NuGet packages. A cache saved by a workflow on `main` can be restored by any workflow on any branch, so if a workflow that runs untrusted code on `main`, like the `pull_request_target` example above, can save a cache, it can plant files that your release workflow later restores and runs. Keep caching out of workflows that run untrusted code, and in release and deployment workflows, either skip the cache or use `actions/cache/restore`, which restores but never saves. See [Cache access](https://docs.github.com/en/actions/reference/workflows-and-actions/dependency-caching#restrictions-for-accessing-a-cache).

### Secrets in logs

GitHub replaces a secret's value with `***` in logs, but only the exact value. If a step prints the secret base64-encoded, split up, or as part of a URL, it shows. Don't print secrets, and when a step creates a new sensitive value, such as a token it fetched, mask it first with `echo "::add-mask::$VALUE"`, as [Lab 18](foundry-standards.md) does.

## Turn on code scanning for workflows

CodeQL, GitHub's code analysis engine, can also check workflow files, with checks similar to zizmor's. Its results go to the **Security** tab instead of the run, and it can check code in other languages at the same time. Code scanning is free for public repositories. For a private one, it needs GitHub Code Security, so skip this part if your repository is private.

1. Select the **Settings** tab.
2. In the sidebar's **Security and quality** section, select **Advanced Security**.
3. Under **Code Security**, next to **CodeQL analysis**, select **Set up**, then **Default**.
4. The dialog lists the languages it found, including **GitHub Actions** and **C#**. Select **Enable CodeQL**.

The first analysis takes a few minutes and runs on GitHub-hosted runners. When it's done, select the **Security** tab, then **Code scanning**. Because you fixed the zizmor findings, there may be nothing to show, which is what you want. See [About code scanning with CodeQL](https://docs.github.com/en/code-security/concepts/code-scanning/codeql/codeql-code-scanning).

Running both is reasonable: zizmor fails the pull request's check, and CodeQL keeps a history of alerts on the **Security** tab.

## What happened

You made a workflow token read-only unless a workflow asks for more, and you checked every workflow's `permissions` against what its jobs do. zizmor found the token that `actions/checkout` saved where later steps could read it, and you removed it. Dependabot will now keep your pinned actions current, waiting a week after each release. And you saw the three patterns behind most workflow attacks: untrusted text in a script, untrusted code with a write token, and a cache that untrusted code can write.

For the full list of recommendations, see [Secure use reference](https://docs.github.com/en/actions/reference/security/secure-use).

## Check your result

- **Settings**, **Actions**, **General** shows **Read repository contents and packages permissions** selected.
- The latest **Audit workflows** run on `main` passed, with no annotations.
- `.github/workflows/build.yml` and `.github/workflows/tests.yml` both set `persist-credentials: false` on checkout.
- **Insights**, **Dependency graph**, **Dependabot** lists `.github/dependabot.yml`.

## If something fails

If **Run zizmor** fails with findings you don't recognize, open its log. zizmor prints each finding with the file, the line, and a link to its documentation. Findings in workflows from other labs are expected: fix them, or if a finding doesn't apply, ignore it with a reason as shown above.

If the run fails after you add `persist-credentials: false`, check the indentation: `with:` lines up with `uses:`, and `persist-credentials` is two spaces further in.

If **Dependency graph** has no **Dependabot** tab, check that the file is `.github/dependabot.yml`, not in `.github/workflows`. Dependabot reports errors in the file on the same page.

If **Advanced Security** doesn't list **CodeQL analysis**, your repository is probably private. Code scanning needs a public repository or GitHub Code Security.

## Clean up

Keep the read-only default, the `persist-credentials` fixes, and Dependabot: they're good settings for any repository. The audit workflow runs on each push to `main` and each pull request. To stop it, delete `.github/workflows/zizmor.yml`. To turn off CodeQL, go back to **Advanced Security**, and next to **CodeQL analysis**, select the **...** menu, then **Disable CodeQL**.

## Next step

To build an action of your own, try [the custom action lab](custom-action.md). Or pick another lab from [the lesson list](../../README.md).
