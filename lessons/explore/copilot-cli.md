---
title: "Lab 6: Generate a report with Copilot CLI"
summary: "Run GitHub Copilot CLI in a workflow that writes a report about your repository, and practice with inputs, variables, secrets, and token permissions."
duration: 30
difficulty: Intermediate
order: 6
requires: "Copilot CLI, through your Copilot plan or your organization."
---

# Lab 6: Generate a report with Copilot CLI

GitHub Copilot CLI is Copilot in a terminal: you describe a task, and it reads files, edits them, and runs commands to get it done. In this lab, a workflow runs it to write a short Markdown report about your repository. The workflow reads values from four places: a workflow input, a repository variable, a repository secret, and the `GITHUB_TOKEN` that GitHub creates for each job. Nobody's there to approve Copilot's actions during a run, so you'll also limit what it's allowed to do.

## Before you start

You need the repository you created in [Lab 0](../foundation/00-create-repository.md). You'll add a workflow file the way you did in [Lab 2](../foundation/02-test-pull-request.md) and start it the way you did in [Lab 1](../foundation/01-build-application.md). Everything happens in your browser.

You also need access to Copilot CLI. What that takes, and who pays for Copilot's work, depends on who owns your copy of the template:

| Your copy belongs to | What you need | Who pays for Copilot's work |
| --- | --- | --- |
| Your personal account | A GitHub Copilot plan | You, from your plan |
| An organization | The organization's **Allow use of Copilot CLI billed to the organization** policy, which is on by default when the organization has Copilot CLI turned on | The organization |

Every Copilot plan includes Copilot CLI, but if you get Copilot through an organization or enterprise, an administrator can turn the CLI off. See [Plans for GitHub Copilot](https://docs.github.com/en/copilot/get-started/plans). [Copilot CLI in GitHub Actions](https://docs.github.com/en/copilot/concepts/agent-capabilities/agent-powered-workflows-and-operations/copilot-cli-in-github-actions) explains who pays, and [Using Copilot CLI in GitHub Actions with GITHUB_TOKEN](https://docs.github.com/en/copilot/how-tos/copilot-cli/use-copilot-cli-in-actions) explains the policy. If your organization has the policy turned off, you can [use a personal access token instead](#use-a-personal-access-token-instead), which bills your own Copilot plan.

Copilot's work is measured in AI credits. A credit is worth $0.01, and the number a run uses depends on the model and on how much Copilot reads and writes. One test run of this lab used about 4 credits. On your own plan, they come out of the plan's monthly allowance. See [Usage-based billing for individuals](https://docs.github.com/en/copilot/concepts/billing-and-usage/individuals/billing). If you have an annual Copilot Pro or Pro+ plan that stayed on request-based billing, each run uses one premium request with the default model instead. See [Requests in GitHub Copilot (legacy)](https://docs.github.com/en/copilot/reference/copilot-billing/request-based-billing-legacy/copilot-requests).

The workflow only runs when you start it. If your repository is public, the runs are free. If it's private, each run uses about 1 of the 2,000 Actions minutes a month that GitHub Free includes, because GitHub rounds each job up to a whole minute. See [GitHub Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions).

## Add a repository variable

The report's title comes from a repository variable, so you can change it without editing the workflow.

1. Select the **Settings** tab. In the left sidebar, in the "Security" section, select **Secrets and variables**, then **Actions**.
2. Select the **Variables** tab, then **New repository variable**.
3. In **Name**, type `REPORT_TITLE`. In **Value**, type a title for your report, such as `My repository report`.
4. Select **Add variable**.

Workflows in your repository read it as `vars.REPORT_TITLE`. Logs show variable values as they are, so use variables only for settings that aren't sensitive, never for a password or token. Organizations and environments can have variables too. See [Variables](https://docs.github.com/en/actions/concepts/workflows-and-actions/variables).

## Add a practice secret

The workflow signs in to Copilot with `GITHUB_TOKEN`, so it doesn't need a secret of its own. Add a practice secret to see how GitHub handles one.

> [!CAUTION]
> The workflow prints this secret in capital letters, which GitHub doesn't hide in the log. Use made-up text, never a real password or token.

1. On the same page, select the **Secrets** tab. If it already lists `PRACTICE_SECRET` from another lab, skip to the next section, because any made-up value works. Otherwise, select **New repository secret**.
2. In **Name**, type `PRACTICE_SECRET`. In **Secret**, type made-up text with some lowercase letters in it, such as `practice-value-123`.
3. Select **Add secret**.

Your browser encrypts the value before it reaches GitHub, and a workflow can only read a secret that it names, as in `secrets.PRACTICE_SECRET`. Organizations and environments can have secrets too. See [Secrets](https://docs.github.com/en/actions/concepts/security/secrets).

## Add the workflow

1. Select the **Code** tab, and make sure the branch menu shows `main`.
2. Select **Add file**, then **Create new file**.
3. In the file name box, type `.github/workflows/copilot-report.yml`.
4. Copy this workflow into the editor. Keep the spaces exactly as they are.

```yaml source=solutions/copilot-cli/copilot-report.yml
name: Copilot report

on:
  workflow_dispatch:
    inputs:
      focus:
        description: What the report should focus on
        type: choice
        options:
          - overview
          - workflows
          - tests
        default: overview

permissions:
  contents: read
  copilot-requests: write

env:
  REPORT_FILE: copilot-report.md

jobs:
  report:
    name: Generate the report
    runs-on: ubuntu-latest
    timeout-minutes: 10
    env:
      COPILOT_VERSION: 1.0.98
    steps:
      - name: Check out the repository
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          # Later steps don't run git, so don't save the token for them.
          persist-credentials: false

      - name: Practice with a secret
        env:
          PRACTICE_SECRET: ${{ secrets.PRACTICE_SECRET }}
        run: |
          if [ -z "$PRACTICE_SECRET" ]; then
            echo "PRACTICE_SECRET isn't set, so there's nothing to mask."
            exit 0
          fi
          # Print a secret only when it's a practice value like this one.
          echo "The secret: $PRACTICE_SECRET"
          capitals=$(echo "$PRACTICE_SECRET" | tr '[:lower:]' '[:upper:]')
          echo "The secret in capitals: $capitals"
          echo "::add-mask::$capitals"
          echo "The same value after add-mask: $capitals"

      - name: Install Copilot CLI
        run: |
          npm install -g "@github/copilot@$COPILOT_VERSION"
          copilot --version

      - name: Run Copilot CLI
        env:
          GITHUB_TOKEN: ${{ github.token }}
          FOCUS: ${{ inputs.focus }}
          REPORT_TITLE: ${{ vars.REPORT_TITLE }}
        run: |
          prompt="Write a short Markdown report about this repository for a developer who is new to it. Focus on this topic: $FOCUS. Read the files you need instead of guessing. Use level-two headings, stay under 300 words, and don't add a title. Reply with the report only."
          report=$(copilot --prompt "$prompt" --silent \
            --available-tools='view,glob,grep' \
            --deny-tool='write,shell,url' \
            --disable-builtin-mcps \
            --max-ai-credits 50)
          {
            echo "# ${REPORT_TITLE:-Repository report}"
            echo
            echo "Focus: $FOCUS. Repository: $GITHUB_REPOSITORY at commit ${GITHUB_SHA:0:7}. Written by GitHub Copilot CLI $COPILOT_VERSION."
            echo
            echo "$report"
          } > "$REPORT_FILE"

      - name: Show the report in the job summary
        run: cat "$REPORT_FILE" >> "$GITHUB_STEP_SUMMARY"

      - name: Upload the report
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: copilot-report
          path: ${{ env.REPORT_FILE }}
          if-no-files-found: error
          retention-days: 7
```

5. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `main` branch, then select **Commit changes**.

Committing the file doesn't start a run, because `workflow_dispatch` is the workflow's only trigger.

### What's in the workflow

- `workflow_dispatch` adds the **Run workflow** button, and `focus` is a `choice` input, so the menu that the button opens offers a list of three topics. [Lab 4: Trigger workflows and pass inputs](triggers-and-inputs.md) covers inputs in more detail.
- `permissions` gives the job's `GITHUB_TOKEN` two permissions: `contents: read` to check out the code, and `copilot-requests: write` to send requests to Copilot. [How Copilot CLI signs in](#how-copilot-cli-signs-in) explains why it gets nothing else.
- `env` sets environment variables at three levels: `REPORT_FILE` for the whole workflow, `COPILOT_VERSION` for every step of the job, and each step's own `env` for that step only. If two levels set the same name, the most specific one wins. The secret and the token are only in the environment of the steps that use them.
- The `${{ }}` expressions read from contexts, which are objects that GitHub fills in for each run. `inputs` holds the workflow inputs, `vars` the variables, and `secrets` the secrets. `github` holds the token and details such as the repository, the commit, and the event that started the run. A variable or secret that doesn't exist reads as an empty string. See [Contexts reference](https://docs.github.com/en/actions/reference/workflows-and-actions/contexts).
- The scripts handle those empty strings. `[ -z "$PRACTICE_SECRET" ]` checks whether the secret is empty, and `${REPORT_TITLE:-Repository report}` uses `Repository report` when there's no title.
- None of the `run` scripts contain `${{ }}`. Values reach them through environment variables in double quotes, so an input can't become part of a command. [Lab 4](triggers-and-inputs.md#pass-an-input-that-looks-like-a-command) shows what happens otherwise, and [Script injections](https://docs.github.com/en/actions/concepts/security/script-injections) explains the risk.
- **Run Copilot CLI** puts the focus into a prompt, runs Copilot, and writes the report file: a heading with your title, a line about the run, and then Copilot's answer. GitHub sets environment variables such as `GITHUB_REPOSITORY` and `GITHUB_SHA` in every job, but not `GITHUB_TOKEN`, so the step sets that one itself. `${GITHUB_SHA:0:7}` is the first seven characters of the commit SHA.
- Values under `with` go to an action as they are, without a shell to expand `$REPORT_FILE`, so **Upload the report** uses the expression `${{ env.REPORT_FILE }}` instead.
- `persist-credentials: false` stops `actions/checkout` from saving the token for later steps. None of them runs Git, so none of them needs it.
- `COPILOT_VERSION` pins the Copilot CLI release that npm installs, so a new release can't change how the workflow behaves until you choose to update. Copilot CLI turns off its automatic updates when the `CI` environment variable is set, and GitHub always sets `CI` on its runners. The CLI needs Node.js 22 or later, which `ubuntu-latest` already has. Signing in with `GITHUB_TOKEN` needs a recent version, so when you update, change `COPILOT_VERSION` to the latest release, not a pre-release, from [the Copilot CLI releases](https://github.com/github/copilot-cli/releases).
- **Show the report in the job summary** adds the report to the file that `GITHUB_STEP_SUMMARY` names, and GitHub shows that Markdown on the run's **Summary** page.
- **Upload the report** saves the file as an artifact named `copilot-report`, and `retention-days: 7` keeps it for 7 days. `if-no-files-found: error` fails the step if the report is missing, where the default would only warn. `timeout-minutes: 10` stops the job if it's still running after 10 minutes, as in [Lab 1](../foundation/01-build-application.md).

## Run the workflow

1. Select the **Actions** tab, then **Copilot report** in the left sidebar.
2. Select **Run workflow**. In the menu that opens, leave the branch under **Use workflow from** set to `main`. Below it, the list for the `focus` input shows `overview`.
3. Choose `workflows` in the list, then select the green **Run workflow** button.
4. When the run appears at the top of the list, select it. If it doesn't appear after a few seconds, refresh the page.
5. Under **Jobs** in the left sidebar, select **Generate the report**.

The job takes about 30 seconds.

## Read the log

Expand **Set up job**, then **GITHUB_TOKEN Permissions**:

```text
Contents: read
CopilotRequests: write
Metadata: read
```

The first two lines come from `permissions`. GitHub added `Metadata: read`, read-only access to basic information about the repository, even though the workflow didn't ask for it.

Expand **Practice with a secret**, then select the line under it that starts with `Run if`. It opens a group with the step's script, its shell, and its environment variables, where the secret is already hidden:

```text
env:
  REPORT_FILE: copilot-report.md
  COPILOT_VERSION: 1.0.98
  PRACTICE_SECRET: ***
```

Below the group, the step prints three lines. With `practice-value-123` as the secret, they look like this:

```text
The secret: ***
The secret in capitals: PRACTICE-VALUE-123
The same value after add-mask: ***
```

GitHub replaces every exact copy of a secret's value in the log with `***`, so the secret in the first line is hidden. The capitals are different text, so they show in full, and so would a Base64 or URL-encoded copy of the value. `::add-mask::` is a workflow command that tells GitHub to hide a value for the rest of the job. The step's `::add-mask::` line doesn't appear in the log itself, and after it, the capitals show as `***` too. If your secret has no lowercase letters, its capitals are the same text, so that line shows `***` as well. See [Workflow commands for GitHub Actions](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-commands#masking-a-value-in-a-log).

Expand **Install Copilot CLI** to see the version it installed:

```text
added 3 packages in 4s
GitHub Copilot CLI 1.0.98.
Run 'copilot update' to check for updates.
```

Ignore the last line. To update, change `COPILOT_VERSION` in the workflow instead.

Expand **Run Copilot CLI**, then the line that starts with `Run prompt=`. The token shows as `***`, like a secret, while the input and the variable appear as they are, with your own title:

```text
env:
  REPORT_FILE: copilot-report.md
  COPILOT_VERSION: 1.0.98
  GITHUB_TOKEN: ***
  FOCUS: workflows
  REPORT_TITLE: My repository report
```

The step prints nothing else, because the script saves Copilot's answer in a file.

## Read the report

1. Select **Summary** in the left sidebar. Further down the page is the report, under your title.
2. Under **Artifacts**, select `copilot-report`. Your browser downloads `copilot-report.zip`, which contains `copilot-report.md`.

The title and the line under it come from the workflow, so they're the same every time. Copilot writes the rest, and it changes from run to run. It can also be wrong. In a test run, the report warned that `tests.yml` breaks a rule in `AGENTS.md` about using only `workflow_dispatch`. That rule is for the people who maintain the template, and [Lab 2](../foundation/02-test-pull-request.md) adds `tests.yml` with a `pull_request` trigger on purpose. Check what a report claims against the files before you rely on it.

To try the other topics, run the workflow again with `overview` or `tests`. Each run uses AI credits again.

## How Copilot CLI signs in

Copilot CLI looks for a token in three environment variables, in this order: `COPILOT_GITHUB_TOKEN`, `GH_TOKEN`, and `GITHUB_TOKEN`. It uses the first one it finds. On your own computer, it can also use the token that `copilot login` saves, or the one GitHub CLI uses. See [Authenticating GitHub Copilot CLI](https://docs.github.com/en/copilot/how-tos/copilot-cli/set-up-copilot-cli/authenticate-copilot-cli).

**Run Copilot CLI** sets `GITHUB_TOKEN` to `github.token`, the same token that `secrets.GITHUB_TOKEN` holds. GitHub creates it at the start of each job, and its permissions are limited to the repository that contains the workflow. It expires when the job finishes, or after 6 hours at most on GitHub-hosted runners, so there's nothing to store or rotate. GitHub hides it in the log like a secret, and Copilot CLI hides the values of `GITHUB_TOKEN` and `COPILOT_GITHUB_TOKEN` in its own output too. See [GITHUB_TOKEN](https://docs.github.com/en/actions/concepts/security/github_token).

`permissions` decides what the token can do. When a workflow lists any permission, every permission it doesn't list is set to `none`, so this token can't push commits, open issues, or comment on pull requests. `copilot-requests` only accepts `write` or `none`. Giving a token only the permissions its job needs is called least privilege: if a step misbehaves, or Copilot follows instructions it shouldn't, the token still can't do more than read the code and send requests to Copilot. See [`permissions` in the workflow syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#permissions).

Without `copilot-requests: write`, **Run Copilot CLI** fails within a few seconds with an error that blames policy settings. [If something fails](#if-something-fails) shows it.

Until July 2026, a workflow had to give Copilot CLI a personal access token. See [GitHub's changelog post from July 2, 2026](https://github.blog/changelog/2026-07-02-copilot-cli-no-longer-needs-a-personal-access-token-in-github-actions/).

## What Copilot CLI is allowed to do

The `focus` input only accepts three fixed values, but it still ends up inside a prompt written in plain language, and so does the content of every file Copilot reads. If `focus` were a text box, or a file in the repository contained instructions, Copilot could follow them. That's called prompt injection, and quoting can't prevent it the way it prevents script injection. What Copilot is allowed to do is the real boundary, so the workflow sets it with flags:

- `--prompt` runs one prompt without the interactive interface and exits when it's done. Nobody's there to approve anything during the run, so the other flags decide in advance what Copilot may do.
- `--silent` prints only Copilot's answer, without usage statistics, so the script can save it as the report.
- `--available-tools='view,glob,grep'` gives Copilot three tools and no others: `view` reads files, `glob` finds files by name, and `grep` searches their contents. Without this flag, Copilot CLI would also run read-only shell commands without asking.
- `--deny-tool='write,shell,url'` is a second layer: it denies writing files, running shell commands, and fetching URLs. Deny rules win over allow rules, even over `--allow-all`, which would otherwise allow every tool, path, and URL.
- `--disable-builtin-mcps` turns off the MCP (Model Context Protocol) servers that come with Copilot CLI, such as the GitHub MCP server. MCP servers give Copilot more tools, and the report doesn't need them.
- `--max-ai-credits 50` stops the run when it reaches 50 AI credits ($0.50). The limit is soft, so Copilot finishes the response it's working on first. The lowest value allowed is 30, and the feature is in public preview. See [Setting an AI credit session limit in GitHub Copilot CLI](https://docs.github.com/en/copilot/how-tos/copilot-cli/use-copilot-cli/set-session-limit).

Copilot CLI also follows instruction files it finds in the repository, such as `AGENTS.md` and `.github/copilot-instructions.md`. Your copy has an `AGENTS.md` with the template's writing and workflow rules, which is where the rule about `tests.yml` came from. To stop Copilot CLI from loading them as instructions, add `--no-custom-instructions`. Copilot can still read them with `view`, like any other file.

Keep a workflow like this one manual. GitHub warns that running Copilot CLI directly in a workflow gives it broad access to the workflow's environment, and that workflows triggered by pull requests from forks are particularly at risk. For most automation, GitHub recommends GitHub Agentic Workflows instead, which [Lab 11](agentic-workflows.md) covers. See [Copilot CLI in GitHub Actions](https://docs.github.com/en/copilot/concepts/agent-capabilities/agent-powered-workflows-and-operations/copilot-cli-in-github-actions), [Allowing and denying tool use](https://docs.github.com/en/copilot/how-tos/copilot-cli/use-copilot-cli/allowing-tools), and the [GitHub Copilot CLI command reference](https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-command-reference).

## Use a personal access token instead

If your copy belongs to an organization that doesn't allow Copilot CLI billed to the organization, Copilot CLI can sign in with a fine-grained personal access token instead. The token acts as you, and Copilot's work is billed to your own Copilot plan. Classic personal access tokens don't work. Anyone with write access to your repository can read its secrets, so give the token as little access as possible and a short expiration.

1. Select your profile picture in the upper-right corner of any page, then **Settings**.
2. In the left sidebar, select **Developer settings**. Under **Personal access tokens**, select **Fine-grained tokens**, then **Generate new token**.
3. In **Token name**, type a name such as `Copilot report`. Under **Expiration**, choose a short period, such as 7 days.
4. Under **Resource owner**, select your personal account. The **Copilot requests** permission is only available on tokens that a personal account owns.
5. Under **Repository access**, select **Public repositories**, the smallest choice. The workflow checks out your code with `GITHUB_TOKEN`, so this token only has to send requests to Copilot.
6. Under **Permissions**, select the **Account** tab, then **Add permissions**, and select **Copilot requests**.
7. Select **Generate token**, and copy the token.
8. In your repository, select the **Settings** tab, then **Secrets and variables** and **Actions** in the left sidebar.
9. On the **Secrets** tab, select **New repository secret**. In **Name**, type `COPILOT_GITHUB_TOKEN`. Paste the token into **Secret**, then select **Add secret**.

Then edit `.github/workflows/copilot-report.yml` on `main`. The `GITHUB_TOKEN` no longer sends requests to Copilot, so remove its `copilot-requests` permission:

```diff
 permissions:
   contents: read
-  copilot-requests: write
```

In **Run Copilot CLI**, replace the `GITHUB_TOKEN` line with the secret:

```diff
         env:
-          GITHUB_TOKEN: ${{ github.token }}
+          COPILOT_GITHUB_TOKEN: ${{ secrets.COPILOT_GITHUB_TOKEN }}
           FOCUS: ${{ inputs.focus }}
```

`COPILOT_GITHUB_TOKEN` is the first variable Copilot CLI checks. GitHub CLI reads `GH_TOKEN` and `GITHUB_TOKEN` but not this one, so if a step also runs `gh`, it won't use your token. Never put the token itself in the workflow file: anyone who can read the repository could see it, and Git keeps it in the history after you delete the line. See [Managing your personal access tokens](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens).

## What happened

GitHub hides secrets in logs by looking for their exact values, so masking has limits. It isn't guaranteed, because a secret can be transformed in many ways, as the capitals showed, and the runner only hides secrets that the current job uses. A few habits cover the gaps:

- Don't print secrets, even ones GitHub would mask. To check that a secret is there, test whether it's empty, as the practice step does first.
- When a step creates a new value from a secret, such as an encoded copy, register it with `::add-mask::` before anything prints it.
- Don't store structured data, such as a block of JSON or YAML, as one secret. Masking looks for the whole value, so parts of it can end up in the log. Create one secret per sensitive value instead.
- Pass secrets to commands through environment variables, as this workflow does, not as command-line arguments, which other users on the machine might see. See [Using secrets in GitHub Actions](https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-secrets).
- Test a workflow with both valid and invalid inputs, then read the logs to check that secrets are hidden. A tool's error messages can end up with a secret in them, and it isn't always obvious when.
- If a secret shows up in a log anyway, delete the log and rotate the secret: replace it with a new credential and make the old one stop working, for example by deleting the old token. See [Using workflow run logs](https://docs.github.com/en/actions/how-tos/monitor-workflows/use-workflow-run-logs#deleting-logs).

Anyone with write access to your repository can read all of its secrets, which is another reason to give every credential only the access it needs. When a workflow runs for a pull request from a fork, it gets no secrets except `GITHUB_TOKEN`, and that token is read-only. Runs that Dependabot starts with events such as `push` and `pull_request` are treated the same way, and the only secrets they get are the ones stored as Dependabot secrets. See [Events that trigger workflows](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#workflows-in-forked-repositories), [Troubleshooting Dependabot on GitHub Actions](https://docs.github.com/en/code-security/reference/supply-chain-security/troubleshoot-dependabot/dependabot-on-actions), and [Secure use reference](https://docs.github.com/en/actions/reference/security/secure-use).

## Check your result

Select the **Actions** tab, then **Copilot report** in the left sidebar. Your run has a green check mark. On its **Summary**, the report appears under your title, and under **Artifacts**, `copilot-report` downloads as `copilot-report.zip` with `copilot-report.md` inside. In the job's log, **Practice with a secret** shows `***` wherever it printed the secret unchanged.

## If something fails

If **Run Copilot CLI** fails within a few seconds with this error, the token couldn't send requests to Copilot. The request ID is different every time:

```text
Error: Access denied by policy settings (Request ID: 4C24:192F58:7A4FD2:8C119F:6AD4717E)

Your Copilot CLI policy setting may be preventing access. This can happen when:
  • Your organization has restricted Copilot access
  • Your Copilot subscription does not include this feature
  • Required policies have not been enabled by your administrator

To resolve this, visit your Copilot settings: https://github.com/settings/copilot
```

In the test run that produced it, the workflow didn't have `copilot-requests: write`, even though the message talks about policies. Check that **Set up job** lists `CopilotRequests: write` under **GITHUB_TOKEN Permissions**. If it does, check the plan and the policy in [Before you start](#before-you-start).

If **Copilot report** isn't in the left sidebar of the **Actions** tab, check that the file is on `main` with the path `.github/workflows/copilot-report.yml`. The **Run workflow** button only works for workflows on the default branch.

If **Practice with a secret** prints `PRACTICE_SECRET isn't set, so there's nothing to mask.`, check the name on the **Secrets** tab. A value saved on the **Variables** tab isn't part of the `secrets` context.

If the report's heading is `Repository report` instead of your title, check that the **Variables** tab lists `REPORT_TITLE`. A value saved as a secret isn't part of the `vars` context.

## Clean up

The workflow only runs when you start it, so it can stay. When you no longer need the practice values, select the **Settings** tab, then **Secrets and variables** and **Actions** in the left sidebar. Delete `PRACTICE_SECRET` on the **Secrets** tab and `REPORT_TITLE` on the **Variables** tab.

If you created a personal access token, delete it too. Select your profile picture, then **Settings**. Select **Developer settings**, then **Fine-grained tokens** under **Personal access tokens**, and select **Delete** next to the token. Then delete the `COPILOT_GITHUB_TOKEN` secret from your repository.

## Next step

You can do the labs after Lab 2 in any order, so pick your next one from [the lesson list](../../README.md). To automate with Copilot, try [Lab 11](agentic-workflows.md), which uses GitHub Agentic Workflows.
