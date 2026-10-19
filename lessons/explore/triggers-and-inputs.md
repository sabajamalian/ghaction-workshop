---
title: "Lab 4: Trigger workflows and pass inputs"
summary: "Add a manual workflow with inputs and conditions, pass the inputs to the shell safely, and see how the push, pull_request, and schedule triggers start runs."
duration: 30
difficulty: Beginner
order: 4
---

# Lab 4: Trigger workflows and pass inputs

In Lab 1, you started a workflow with **Run workflow**, and in Lab 2, opening a pull request started one. Here you'll add a manual workflow that asks for a greeting and an environment, uses the answers to decide which steps and jobs run, and passes them to the shell safely. Then you'll see how the `push`, `pull_request`, and `schedule` triggers decide when a workflow runs.

## Before you start

You need the repository you created in [Lab 0](../foundation/00-create-repository.md). You'll start runs the way you did in [Lab 1](../foundation/01-build-application.md), and the optional part edits a file the way you did in [Lab 2](../foundation/02-test-pull-request.md). Everything happens in your browser.

The workflow only runs when you start it, unless you do the optional part, which adds a `push` trigger for a few minutes. If your repository is public, the runs are free. If it's private, they use your account's included Actions minutes, and GitHub rounds each job up to a whole minute. Four jobs run during the lab, so it uses about 4 of the 2,000 minutes a month that GitHub Free includes, or 5 with the optional part. See [GitHub Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions).

## Add the workflow

1. Select the **Code** tab of your repository, and make sure the branch menu shows `main`.
2. Select **Add file**, then **Create new file**.
3. In the file name box, type `.github/workflows/greeting.yml`.
4. Copy this workflow into the editor. Keep the spaces exactly as they are.

```yaml source=solutions/triggers/greeting.yml
name: Greeting

on:
  workflow_dispatch:
    inputs:
      greeting:
        description: Greeting to print
        type: string
        required: true
        default: Hello
      environment:
        description: Environment this run is for
        type: choice
        options:
          - development
          - staging
          - production
        default: development
      shout:
        description: Shout the greeting in capital letters
        type: boolean
        default: false

permissions:
  contents: read

jobs:
  greet:
    runs-on: ubuntu-latest
    timeout-minutes: 5
    env:
      # Inputs are empty when another event, such as push, starts the run.
      GREETING: ${{ inputs.greeting || format('Hello from a {0} event', github.event_name) }}
      AUDIENCE: ${{ vars.GREETING_AUDIENCE || 'world' }}
      TARGET_ENVIRONMENT: ${{ inputs.environment || 'none' }}
    steps:
      - name: Print the greeting
        run: echo "$GREETING, $AUDIENCE"

      - name: Shout the greeting
        if: inputs.shout
        run: echo "$GREETING, $AUDIENCE" | tr '[:lower:]' '[:upper:]'

      - name: Print where the run came from
        env:
          EVENT_NAME: ${{ github.event_name }}
          BRANCH_NAME: ${{ github.ref_name }}
          OPERATING_SYSTEM: ${{ runner.os }}
        run: |
          echo "Event: $EVENT_NAME"
          echo "Branch: $BRANCH_NAME"
          echo "Runner: $OPERATING_SYSTEM"
          echo "Environment: $TARGET_ENVIRONMENT"

      - name: Write the job summary
        run: |
          {
            echo "### $GREETING, $AUDIENCE"
            echo ""
            echo "- Environment: $TARGET_ENVIRONMENT"
            echo "- Event: $GITHUB_EVENT_NAME"
            echo "- Started by: $GITHUB_ACTOR"
          } >> "$GITHUB_STEP_SUMMARY"

  production-check:
    if: inputs.environment == 'production'
    runs-on: ubuntu-latest
    timeout-minutes: 5
    steps:
      - name: Warn about production
        run: echo "::warning::This run is for production."
```

5. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `main` branch, then select **Commit changes**.

Committing the file doesn't start a run, because `workflow_dispatch` is the workflow's only trigger. The file has to be on `main`, your default branch, for its **Run workflow** button to appear.

### What's in the workflow

- `workflow_dispatch` adds the **Run workflow** button, as in Lab 1, and its `inputs` add a field to the button's menu for each input, labeled with the input's `description`. Inputs let one workflow cover several cases. A deployment workflow, for example, can ask which environment and version to deploy instead of keeping a copy of itself for each.
- `type` sets the kind of field. `string` is a text box, `choice` is a list of the `options`, and `boolean` is a checkbox. The other types are `number` and `environment`.
- `default` is the value an input has if you don't change it, and `required: true` means the input must have a value.
- `permissions` and `timeout-minutes` work as in Lab 2. The limit is 5 minutes here because these jobs only print text.
- `env` on the `greet` job sets three environment variables for all of its steps. Their values are expressions inside `${{ }}`, which GitHub evaluates before the steps run. `inputs.greeting` reads the greeting from the `inputs` context, `vars.GREETING_AUDIENCE` reads a repository variable that you'll add later, and `github.event_name` is the name of the event that started the run.
- `||` gives the value on its right when the value on its left is empty. When an event other than `workflow_dispatch` starts the run, the inputs are empty, so `format` builds a greeting instead by putting the event name in place of `{0}`. Text in an expression goes in single quotes, as in `'world'`.
- `if` makes a step or a job conditional. `if: inputs.shout` runs **Shout the greeting** only when the checkbox is selected, and `if: inputs.environment == 'production'` runs the `production-check` job only for `production`. When a condition is false, GitHub skips the step or job, and the run still succeeds. You can leave out the `${{ }}` in an `if`.
- **Print where the run came from** has its own `env`, because the `runner` context, which holds the runner's operating system in `runner.os`, isn't available in a job's `env`.
- None of the `run` commands contain `${{ }}`. They read values from environment variables in double quotes, such as `"$GREETING"`. [Pass an input that looks like a command](#pass-an-input-that-looks-like-a-command) shows why.
- `GITHUB_STEP_SUMMARY`, `GITHUB_EVENT_NAME`, and `GITHUB_ACTOR` are environment variables that GitHub sets in every job. Markdown that a step writes to the `GITHUB_STEP_SUMMARY` file appears on the run's **Summary** page, and `GITHUB_ACTOR` is the person or app that started the run.
- `echo "::warning::This run is for production."` is a workflow command: a line of output that GitHub turns into a warning annotation on the run.
- The `environment` input is a plain `choice`. It isn't connected to the deployment environments that [Lab 8: Add a staging environment](environments.md) sets up.

## Run it with the defaults

1. Select the **Actions** tab, then **Greeting** in the left sidebar.
2. Select **Run workflow**. In the menu that opens, leave the branch under **Use workflow from** set to `main`.

   Below the branch, the menu has a field for each input, labeled with its description: **Greeting to print**, **Environment this run is for**, and a **Shout the greeting in capital letters** checkbox.

3. Make sure **Greeting to print** is `Hello`, **Environment this run is for** is `development`, and the checkbox is clear. Then select the green **Run workflow** button.
4. When the run appears at the top of the list, select it. If it doesn't appear after a few seconds, refresh the page.
5. Under **Jobs** in the left sidebar, select `greet`, then expand **Print the greeting**.

The step prints `Hello, world`. Select the line above the output, which starts with `Run echo`, to open the group that GitHub adds at the start of each `run` step. It shows the command, the shell, and the step's environment variables, with each expression already replaced by its value:

```text
Run echo "$GREETING, $AUDIENCE"
echo "$GREETING, $AUDIENCE"
shell: /usr/bin/bash -e {0}
env:
  GREETING: Hello
  AUDIENCE: world
  TARGET_ENVIRONMENT: development
Hello, world
```

`AUDIENCE` is `world` because there's no `GREETING_AUDIENCE` variable yet. A context property that doesn't exist evaluates to an empty string, so `||` used `'world'`.

GitHub skipped **Shout the greeting**, because the checkbox was clear and `inputs.shout` was `false`. Expand **Print where the run came from** to see values from the `github` and `runner` contexts:

```text
Event: workflow_dispatch
Branch: main
Runner: Linux
Environment: development
```

Select **Summary** in the left sidebar. `greet` has a green check mark, and `production-check` was skipped, because you didn't choose `production`. Further down the page, under **greet summary**, is the Markdown that the last step wrote: a "Hello, world" heading and a list with the environment, the event, and your username.

## Change the inputs

1. Select **Greeting** in the left sidebar, then **Run workflow**.
2. Set **Greeting to print** to `Good morning` and **Environment this run is for** to `production`, and select the **Shout the greeting in capital letters** checkbox.
3. Select the green **Run workflow** button, then open the new run.

In `greet`, **Print the greeting** prints `Good morning, world`, and this time **Shout the greeting** runs too:

```text
GOOD MORNING, WORLD
```

On the run's **Summary**, `production-check` has a green check mark as well, because `inputs.environment == 'production'` was true. Under **Annotations**, its warning says "This run is for production." A warning doesn't fail the run.

## Add a repository variable

To change `world` without editing the workflow, put the audience in a repository variable.

1. Select the **Settings** tab. In the left sidebar, in the "Security" section, select **Secrets and variables**, then **Actions**.
2. Select the **Variables** tab, then **New repository variable**.
3. In **Name**, type `GREETING_AUDIENCE`. In **Value**, type `everyone`.
4. Select **Add variable**.

Every workflow in the repository can now read `vars.GREETING_AUDIENCE`, and your next run uses it. GitHub doesn't hide a variable's value in logs, so don't use variables for passwords or tokens. Store those on the **Secrets** tab instead, and read them with the `secrets` context, such as `secrets.API_TOKEN`. If a step prints a secret, the log shows `***` in its place.

## Pass an input that looks like a command

A shorter way to write **Print the greeting** would put the input straight into the command. Don't add this version to your workflow:

```yaml
- name: Print the greeting
  run: echo "${{ inputs.greeting }}, $AUDIENCE"
```

GitHub replaces each `${{ }}` expression with its value before the shell starts, so whatever someone types into **Greeting to print** becomes part of the script. In a test run of this version with `$(date)` as the greeting, where `AUDIENCE` was `world`, Bash ran the `date` command:

```text
Run echo "$(date), $AUDIENCE"
Sun Oct 18 07:09:47 UTC 2026, world
```

With `"; echo hi #` as the greeting, the script became `echo ""; echo hi #, $AUDIENCE"`. It printed an empty line, then `hi`, and `#` turned the rest of the line into a comment. Any command typed into the field would run on the runner as part of the job.

Now give your workflow the same kind of input:

1. Select **Greeting** in the left sidebar, then **Run workflow**.
2. Set **Greeting to print** to `$(date)` and **Environment this run is for** to `development`, and make sure the checkbox is clear.
3. Select the green **Run workflow** button, open the new run, and expand **Print the greeting** under `greet`.

The step prints exactly what you typed, followed by the audience from your new variable:

```text
$(date), everyone
```

The group above the output shows `GREETING: $(date)`. GitHub put the input into the `GREETING` environment variable unchanged, and the shell reads it as data: Bash doesn't run commands it finds inside a variable's value when it expands `"$GREETING"`, and the double quotes keep the value in one piece, even if it has spaces. GitHub's security guide calls this pattern [an intermediate environment variable](https://docs.github.com/en/actions/reference/security/secure-use#use-an-intermediate-environment-variable).

Only people with write access can start this workflow, and they could edit it anyway, so the unsafe version would do little harm here. The danger is a value from someone who can't edit the workflow, such as the title of an issue or pull request, or the name of a pull request's branch. See [Script injections](https://docs.github.com/en/actions/concepts/security/script-injections).

## Contexts

Expressions read values from contexts. These are the ones you'll use most:

| Context | What it holds | Example |
| --- | --- | --- |
| `github` | The run and the event that started it | `github.event_name`, `github.ref_name` |
| `inputs` | The values from the **Run workflow** menu | `inputs.greeting`, `inputs.shout` |
| `vars` | Configuration variables from the repository, its organization, or an environment | `vars.GREETING_AUDIENCE` |
| `env` | Variables set with `env` in the workflow, a job, or a step | `env.GREETING` |
| `runner` | The machine that runs the job | `runner.os` |
| `secrets` | Secrets, plus the `GITHUB_TOKEN` that GitHub creates for each run | `secrets.GITHUB_TOKEN` |

Inside a `run` command, `${{ env.GREETING }}` and `$GREETING` both give you the value of `GREETING`, but in different ways: GitHub writes the first into the script before the shell starts, and the shell reads the second while the script runs. Use the shell variable, for the reason in the previous section.

Not every context works everywhere. A job's `if` can't read `env` or `runner`, but a step's `if` can. No `if` can read `secrets`. To run a step only when a secret is set, put the secret in an environment variable on the job and test that variable in the step's `if`. The [context availability table](https://docs.github.com/en/actions/reference/workflows-and-actions/contexts#context-availability) lists what each workflow key can read.

The inputs are also in `github.event.inputs`, but there every value is a string, the checkbox included. A clear checkbox becomes the string `'false'`, and a string that isn't empty counts as true, so `if: github.event.inputs.shout` runs the step whether or not you select the checkbox. Use `inputs`, which keeps `true` and `false` as booleans.

## Expressions and conditions

An expression can compare values with `==`, `!=`, `<`, `<=`, `>`, and `>=`, combine them with `&&` (and), `||` (or), and `!` (not), and group them with parentheses. A few details matter in conditions:

- String comparisons ignore case, so `inputs.environment == 'Production'` is also true when you choose `production`.
- `false`, `0`, an empty string, and `null` count as false, and every other value counts as true.
- Wrap a condition that starts with `!` in `${{ }}`, as in `if: ${{ !inputs.shout }}`, because YAML reserves a leading `!` for its own syntax.
- A step with an `if` still runs only when the steps before it succeeded, unless its condition uses a status function. `if: failure()` runs a step only after an earlier step fails, which is useful for printing diagnostics, and `if: ${{ !cancelled() }}` runs it whether the earlier steps passed or failed.

Expressions also have functions. `contains(inputs.greeting, 'morning')` is true when the greeting includes `morning`, and `startsWith(github.ref_name, 'release/')` is true when the branch name starts with `release/`. Both ignore case. To see what a context holds while you debug a workflow, pass `toJSON(inputs)` to a step through `env` and print the variable. Be careful with the whole `github` context, which includes sensitive values such as `github.token`. [Functions](https://docs.github.com/en/actions/reference/workflows-and-actions/expressions#functions) lists the rest.

## Other triggers

A workflow's `on` can list several events, and any one of them starts a run. For example, `on: [push, workflow_dispatch]` runs the workflow on every push and also adds the **Run workflow** button.

### push

```yaml
on:
  push:
    branches:
      - main
    paths:
      - 'src/**'
```

`push` runs the workflow when someone pushes commits or a tag, and a commit you make in the browser counts. GitHub runs the workflow file as it is in the pushed commit, so `push` works on any branch that has the file, not only `main`. When you set both `branches` and `paths`, a push has to match both, so this example runs only for pushes to `main` that change a file under `src`. You can also filter on `tags`, or exclude branches and paths with `branches-ignore` and `paths-ignore`. See [the `push` event](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#push).

### pull_request

```yaml
on:
  pull_request:
    branches:
      - main
    paths-ignore:
      - '**.md'
```

`pull_request` runs the workflow when a pull request is opened or reopened, and when someone pushes to its branch, as you saw in Lab 2. For this event, `branches` matches the branch the pull request targets, so this example runs for pull requests into `main`, except when every changed file is Markdown. To run on other pull request activity, such as `closed` or `labeled`, list it under `types`. Only the types you list start runs, so `types: [opened, reopened]` ignores new commits. GitHub doesn't run `pull_request` workflows while a pull request has a merge conflict.

Take care with filters on a workflow whose check a ruleset requires. When a branch or path filter skips the workflow, the check stays "Pending", and the pull request can't be merged.

### schedule

```yaml
on:
  schedule:
    - cron: '30 5 * * 1-5'
```

`schedule` starts the workflow at the times you give in cron syntax. The five fields are the minute, the hour, the day of the month, the month, and the day of the week, so `30 5 * * 1-5` means 5:30 every Monday to Friday. The time is in UTC, unless you add a `timezone` key to the same list item, such as `timezone: "America/New_York"`. Before you add a schedule:

- It runs the latest commit on the default branch, and only if the workflow file is there.
- It can run at most once every 5 minutes.
- Runs can start late when GitHub Actions is busy, especially at the start of an hour, and under heavy load some queued jobs are dropped. Choose a minute other than `0`.
- In a public repository, GitHub disables scheduled workflows after 60 days without activity in the repository.
- In a private repository, every run uses minutes. A schedule every 5 minutes starts 288 runs a day, enough to use up GitHub Free's 2,000 minutes in about a week.

To stop a workflow without deleting its file, select it in the left sidebar of the **Actions** tab, then select the **Show workflow options** button (three dots) and **Disable workflow**. **Enable workflow** turns it back on.

## Optional: run the workflow on a push

While this trigger is in your workflow, every commit to `main` that changes `greeting.yml` starts a run, which uses a minute in a private repository. You'll add it, watch it start a run, and remove it.

1. Select the **Code** tab, then `.github`, `workflows`, and `greeting.yml`.
2. Select the pencil icon to edit the file.
3. Add a `push` trigger above `workflow_dispatch:`, with `push:` two spaces from the left edge, like `workflow_dispatch:`. The top of the file should look like this:

```yaml
name: Greeting

on:
  push:
    branches:
      - main
    paths:
      - .github/workflows/greeting.yml
  workflow_dispatch:
    inputs:
```

4. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `main` branch, then select **Commit changes**.

That commit is a push to `main` that changes `greeting.yml`, so it starts a run. On the **Actions** tab, open the new **Greeting** run, which is named after your commit message. **Print the greeting** shows `Hello from a push event, everyone`, and **Print where the run came from** shows `Event: push` and `Environment: none`. A push has no inputs, so the `||` fallbacks supplied the greeting and the environment, and GitHub skipped **Shout the greeting** and `production-check`.

To remove the trigger, edit `greeting.yml` again, delete the five lines from `push:` to `- .github/workflows/greeting.yml`, and commit directly to `main`. That commit doesn't start a run, because the version of the file in it has no `push` trigger.

## Check your result

Select the **Actions** tab, then **Greeting** in the left sidebar. The list shows three successful runs, newest first, or four with the push run at the top if you did the optional part:

- The run with `$(date)` as the greeting, which printed `$(date), everyone`.
- The `production` run, which printed `GOOD MORNING, WORLD` and ran `production-check`.
- The run with the defaults, which printed `Hello, world`.

Each run's **Summary** shows the Markdown that `greet` wrote.

## If something fails

If **Greeting** isn't in the left sidebar of the **Actions** tab, check that the file is on `main` with the path `.github/workflows/greeting.yml`.

If the **Actions** tab shows a failed run named `.github/workflows/greeting.yml`, GitHub couldn't read the workflow file. Open that run. Under **Annotations**, an **Invalid workflow file** error names the line with the problem. The usual cause is indentation that changed when you pasted the YAML. Compare your file with [solutions/triggers/greeting.yml](../../solutions/triggers/greeting.yml), fix it on `main`, and commit.

If the greeting still ends with `world` after you added the variable, open the **Variables** tab and check that the name is `GREETING_AUDIENCE`. A value on the **Secrets** tab isn't part of the `vars` context.

If the optional commit didn't start a run, check that it went to `main`. The `branches` filter ignores pushes to other branches.

## Clean up

If you added the `push` trigger and haven't removed it yet, remove it as described at the end of [Optional: run the workflow on a push](#optional-run-the-workflow-on-a-push). The workflow only runs when someone starts it, so you can keep it, and the variable costs nothing. To delete the variable, select the **Settings** tab, then **Secrets and variables** and **Actions**, and delete `GREETING_AUDIENCE` on the **Variables** tab.

## Next step

You can do the labs after Lab 2 in any order, so pick your next one from [the lesson list](../../README.md). [Lab 10: Reuse workflows and steps](reusable-workflows.md) passes inputs to a reusable workflow, which reads them through the same `inputs` context.
