---
title: "Lab 3: Compare runners"
summary: "Run one job on Linux, Windows, and macOS runners at the same time, and compare the machines GitHub gives it."
duration: 20
difficulty: Beginner
order: 3
---

# Lab 3: Compare runners

Every job runs on a runner, the machine its `runs-on` key asks for. You'll run one job on five GitHub-hosted runners at the same time (two Linux, two Windows, and one macOS) and compare what each machine reports about itself.

## Before you start

You need the repository you created in [Lab 0](../foundation/00-create-repository.md). You'll start the workflow from the **Actions** tab, the way you started the build in [Lab 1](../foundation/01-build-application.md). Everything in this lab happens in your browser.

> [!IMPORTANT]
> Runs in a public repository are free. In a private repository, they use your plan's included Actions minutes, and a minute on macOS counts as about 10 of them. One run of this workflow uses about 16 included minutes, 10 of them for the macOS job. GitHub Free includes 2,000 minutes a month. To leave macOS out, delete the `- macos-latest` line and the comment above it before you commit the workflow.

## Add the workflow

1. Select the **Code** tab of your repository, and check that the branch menu shows `main`.
2. Select **Add file**, then **Create new file**.
3. In the file name box, type `.github/workflows/runners.yml`. Each `/` you type starts a new folder.
4. Copy this workflow into the editor. YAML uses indentation to show structure, so keep the spaces exactly as they are.

```yaml source=solutions/runners/runners.yml
name: Inspect runners

on:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  inspect:
    name: Inspect ${{ matrix.os }}
    runs-on: ${{ matrix.os }}
    timeout-minutes: 5
    strategy:
      fail-fast: false
      matrix:
        os:
          - ubuntu-latest
          - ubuntu-24.04-arm
          - windows-latest
          - windows-11-arm
          # In a private repository, a macOS minute costs about as much as ten Linux minutes.
          - macos-latest
    defaults:
      run:
        shell: bash
    steps:
      - name: List the runner variables
        run: env | grep -E '^(RUNNER_|ImageOS|ImageVersion)' | sort

      - name: Collect details on Linux
        if: runner.os == 'Linux'
        run: |
          . /etc/os-release
          {
            echo "OS_NAME=$PRETTY_NAME"
            echo "MACHINE=$(uname -m)"
            echo "CPU_COUNT=$(nproc)"
            echo "MEMORY_GB=$(awk '/^MemTotal:/ { printf "%.1f", $2 / 1048576 }' /proc/meminfo)"
            echo "DISK_FREE_GB=$(df -Pk "$RUNNER_TEMP" | awk 'NR == 2 { printf "%.0f", $4 / 1048576 }')"
          } | tee -a "$GITHUB_ENV"

      - name: Collect details on macOS
        if: runner.os == 'macOS'
        run: |
          {
            echo "OS_NAME=$(sw_vers -productName) $(sw_vers -productVersion)"
            echo "MACHINE=$(uname -m)"
            echo "CPU_COUNT=$(sysctl -n hw.ncpu)"
            echo "MEMORY_GB=$(sysctl -n hw.memsize | awk '{ printf "%.1f", $1 / 1073741824 }')"
            echo "DISK_FREE_GB=$(df -Pk "$RUNNER_TEMP" | awk 'NR == 2 { printf "%.0f", $4 / 1048576 }')"
          } | tee -a "$GITHUB_ENV"

      - name: Collect details on Windows
        if: runner.os == 'Windows'
        shell: pwsh
        run: |
          $os = Get-CimInstance -ClassName Win32_OperatingSystem
          @(
            "OS_NAME=$($os.Caption) $($os.Version)"
            "MACHINE=$env:PROCESSOR_ARCHITECTURE"
            "CPU_COUNT=$([Environment]::ProcessorCount)"
            "MEMORY_GB=$([math]::Round($os.TotalVisibleMemorySize / 1MB, 1))"
            "DISK_FREE_GB=$([math]::Round((Get-Item $env:RUNNER_TEMP).PSDrive.Free / 1GB))"
          ) | Tee-Object -FilePath $env:GITHUB_ENV -Append

      - name: Write the job summary
        env:
          LABEL: ${{ matrix.os }}
        run: |
          {
            echo "| Detail | Value |"
            echo "| --- | --- |"
            echo "| Label | \`$LABEL\` |"
            echo "| Operating system | $OS_NAME |"
            echo "| Runner image | $ImageOS $ImageVersion |"
            echo "| Architecture | $RUNNER_ARCH ($MACHINE) |"
            echo "| CPUs | $CPU_COUNT |"
            echo "| Memory | $MEMORY_GB GB |"
            echo "| Free disk space | $DISK_FREE_GB GB |"
            echo "| Runner name | $RUNNER_NAME |"
            echo "| Runner environment | $RUNNER_ENVIRONMENT |"
          } >> "$GITHUB_STEP_SUMMARY"
```

5. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `main` branch, then select **Commit changes**.

The file has to be on `main`, your repository's default branch, for GitHub to show a **Run workflow** button for it. Committing doesn't start a run, because `workflow_dispatch` is the workflow's only trigger.

### What's in the workflow

- `strategy.matrix.os` lists five runner labels. GitHub creates one job for each label and puts the label wherever the job uses `${{ matrix.os }}`: in `runs-on`, which picks the runner, and in `name`, so the jobs are called **Inspect ubuntu-latest**, **Inspect ubuntu-24.04-arm**, and so on.
- `fail-fast: false` keeps the other jobs running when one fails. By default, a failing job in a matrix cancels the rest, and here you want a result from every runner.
- `timeout-minutes: 5` stops a job that's still running after five minutes. Each job needs only a few seconds.
- `defaults.run.shell: bash` runs every `run` step in Bash unless the step sets its own `shell`. Without it, steps on Windows would run in PowerShell. Windows runners also have the Bash that comes with Git for Windows, so the steps that work the same everywhere can share one script.
- **Collect details on Linux**, **Collect details on macOS**, and **Collect details on Windows** each have an `if` that checks `runner.os`, so only one of them runs in each job. Each operating system needs its own commands. Linux reads `/etc/os-release` and `/proc/meminfo`. macOS has no `/proc`, so it asks `sw_vers` and `sysctl` instead. The Windows step switches to PowerShell with `shell: pwsh` and queries Windows with `Get-CimInstance`.
- Those steps write `NAME=value` lines to the file named by `GITHUB_ENV`, which turns them into environment variables for the rest of the job. `tee`, and `Tee-Object` on Windows, also print the lines to the log.
- **Write the job summary** builds a Markdown table and appends it to the file named by `GITHUB_STEP_SUMMARY`, and GitHub shows the table on the run's summary page. The label reaches the script as the `LABEL` environment variable. GitHub pastes the value of a `${{ }}` expression into the script before running it, so passing values through `env` is the safer habit.
- There's no checkout step. The job only looks at the machine, so it doesn't need your repository's files.

## Run the workflow

1. Select the **Actions** tab, then **Inspect runners** in the left sidebar.
2. Select **Run workflow**. In the menu that opens, leave the branch set to `main`, then select the green **Run workflow** button.
3. After a few seconds, a new run appears at the top of the list. If it doesn't, refresh the page. Select the run.

Under **Jobs** in the left sidebar, the run lists five jobs, one for each label. They start within seconds of each other, because each job gets its own machine, and most finish in under a minute. The macOS job sometimes waits in the queue longer than the others.

## Compare the runners

1. When every job has a green check mark, select **Summary** in the left sidebar and scroll down to the job summaries. Each job wrote one table, and the tables appear in the order the jobs finished.

   Compare the operating system, architecture, CPU, and memory rows. When this lab was tested in a private repository in October 2026, the jobs reported these values:

| Label | Operating system | Architecture | CPUs | Memory |
| --- | --- | --- | --- | --- |
| `ubuntu-latest` | Ubuntu 24.04.5 LTS | X64 (x86_64) | 2 | 7.8 GB |
| `ubuntu-24.04-arm` | Ubuntu 24.04.5 LTS | ARM64 (aarch64) | 2 | 7.7 GB |
| `windows-latest` | Microsoft Windows Server 2025 Datacenter 10.0.26100 | X64 (AMD64) | 2 | 8 GB |
| `windows-11-arm` | Microsoft Windows 11 Enterprise 10.0.26200 | ARM64 (ARM64) | 2 | 8 GB |
| `macos-latest` | macOS 26.6.2 | ARM64 (arm64) | 5 | 14.0 GB |

Your versions will be newer, and `ubuntu-latest` may report Ubuntu 26.04. In a public repository, the Linux and Windows runners have more CPUs and memory. [What happened](#what-happened) explains both differences.

2. Under **Jobs** in the left sidebar, select **Inspect windows-latest**. Expand **Set up job**, then **Runner Image** inside it. The label `windows-latest` doesn't say which version of Windows you get, but this group does, and it links to the list of software installed on the image:

```text
Image: windows-2025-vs2026
Version: 20261007.264.1
Included Software: https://github.com/actions/runner-images/blob/win25-vs2026/20261007.264/images/windows/Windows2025-VS2026-Readme.md
Image Release: https://github.com/actions/runner-images/releases/tag/win25-vs2026%2F20261007.264
```

3. Expand **List the runner variables**. The runner describes itself in environment variables, and on Windows, the paths are Windows paths:

```text
ImageOS=win25-vs2026
ImageVersion=20261007.264.1
RUNNER_ARCH=X64
RUNNER_ENVIRONMENT=github-hosted
RUNNER_NAME=GitHub Actions 1000002762
RUNNER_OS=Windows
RUNNER_TEMP=D:\a\_temp
RUNNER_TOOL_CACHE=C:\hostedtoolcache\windows
RUNNER_TRACKING_ID=github_e31f52d1-e58c-4f49-916e-47ae17739152
RUNNER_WORKSPACE=D:\a\ghaction-workshop-learner-test
```

Your runner name, image version, and tracking ID will differ, and `RUNNER_WORKSPACE` ends with your repository's name.

4. Expand **Collect details on Windows**, then the line at the top of its log that starts with `Run`. Under the script, the `shell:` line shows that the step ran in PowerShell 7:

```text
shell: C:\Program Files\PowerShell\7\pwsh.EXE -command ". '{0}'"
```

In **List the runner variables**, the same line shows `C:\Program Files\Git\bin\bash.EXE`, the Bash from Git for Windows. The Linux and macOS steps in this job were skipped, because their `if` conditions were false.

## What happened

### How a job gets a machine

When a job starts, GitHub matches its `runs-on` label to a runner. For a GitHub-hosted label, GitHub creates a new virtual machine from the matching image, runs the job on it, and removes the machine when the job ends. Nothing carries over from one job to the next, and your five jobs ran at the same time on five separate machines. On GitHub Free, up to 20 jobs can run at once on standard GitHub-hosted runners, and up to 5 of them on macOS. See [job concurrency limits](https://docs.github.com/en/actions/reference/limits#job-concurrency-limits-for-github-hosted-runners).

### Labels and images

A label names a kind of runner. For GitHub-hosted runners, each label maps to an image: an operating system with a set of preinstalled tools. These labels are current in October 2026:

| Operating system | Architecture | Labels |
| --- | --- | --- |
| Ubuntu | x64 | `ubuntu-latest`, `ubuntu-26.04`, `ubuntu-24.04` |
| Ubuntu | arm64 | `ubuntu-26.04-arm`, `ubuntu-24.04-arm` |
| Ubuntu, 1 CPU | x64 | `ubuntu-slim` |
| Windows Server | x64 | `windows-latest`, `windows-2025`, `windows-2022` |
| Windows 11 | arm64 | `windows-11-arm` |
| macOS | arm64 | `macos-latest`, `macos-26`, `macos-15` |
| macOS | x64 (Intel) | `macos-26-intel`, `macos-15-intel` |

The documentation lists every label in this table for private repositories as well as public ones, and the test run for this lab used `ubuntu-24.04-arm` and `windows-11-arm` in a private repository on GitHub Free. The [GitHub-hosted runners reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners) has the full list, including a few aliases and images that are being retired, and links each label to the software installed on its image.

### Latest labels and pinned versions

`ubuntu-latest`, `windows-latest`, and `macos-latest` don't name a version. Each one points to whichever version GitHub currently treats as the newest stable one, and that changes over time. A new version usually gets its own label first: `ubuntu-26.04` was available before `ubuntu-latest` started moving to Ubuntu 26.04, for example. Before GitHub moves a `-latest` label, it announces the change in the [actions/runner-images repository](https://github.com/actions/runner-images), and then it moves the label gradually over a month or two. During the move, a job on that label can get either version.

`ubuntu-latest` moves from Ubuntu 24.04 to Ubuntu 26.04 between October 19 and November 19, 2026, according to [the Ubuntu 26.04 announcement](https://github.com/actions/runner-images/issues/14748). Around that time, your run may show a notice about it under **Annotations**. To see which version a job got, check its summary table, or **Runner Image** in its **Set up job** step. In the test run, the day before the move started, `ubuntu-latest` got `Image: ubuntu-24.04`.

Use a `-latest` label when the job only needs a recent operating system and you'd rather get upgrades without editing the workflow. Pin a version, such as `ubuntu-24.04`, when the job depends on something that changes between versions, like a system package, a compiler, or the default version of a tool, so that you choose when to upgrade. A pinned label doesn't freeze the machine, though. GitHub updates the software on its images about once a week and retires old versions. Jobs on `ubuntu-22.04`, for example, fail after April 17, 2027, as [the Ubuntu 22.04 deprecation notice](https://github.com/actions/runner-images/issues/14254) explains. The arm64 Linux and Windows labels have no `-latest` form, so you always pick a version for them.

### Architecture

On GitHub-hosted runners, `RUNNER_ARCH` and `runner.arch` are `X64` or `ARM64`, whatever the operating system. Each operating system has its own name for the same thing, which is what the parentheses in your summary tables show: `x86_64` or `aarch64` on Linux, `AMD64` or `ARM64` on Windows, and `arm64` on macOS.

Scripts, and code that runs on a runtime such as .NET or Node.js, usually work the same on both architectures. Native binaries don't: a program compiled for x64 needs a separate arm64 build, and an arm64 runner lets you build and test that version on the kind of hardware it targets. Every standard macOS label without `-intel` runs on Apple silicon, which is arm64.

### Hardware

On Linux and Windows, standard runners in public repositories get more CPUs and memory than the same runners in private repositories. The [GitHub-hosted runners reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners) lists these specifications:

| Runner | Public repository | Private repository |
| --- | --- | --- |
| Linux or Windows, x64 or arm64 | 4 CPUs, 16 GB of memory | 2 CPUs, 8 GB of memory |
| macOS, arm64 | 3 CPUs (M1), 7 GB of memory | 3 CPUs (M1), 7 GB of memory |
| macOS, Intel | 4 CPUs, 14 GB of memory | 4 CPUs, 14 GB of memory |
| `ubuntu-slim` | 1 CPU, 5 GB of memory | 1 CPU, 5 GB of memory |

The reference lists 14 GB of SSD storage for each of them. The free space in your summary tables depends on the image: in the test run, it ranged from 13 GB on `ubuntu-latest` to 124 GB on `windows-11-arm`. The macOS job in the test run reported 5 CPUs and 14 GB of memory, more than the reference lists. Plan around the documented numbers rather than what one run reports.

`ubuntu-slim` is a 1-CPU runner for short, light jobs, such as automating issues. It runs the job in a container instead of a full virtual machine, has fewer tools installed, and stops any job after 15 minutes.

### Runner variables and the runner context

Every runner sets environment variables that describe it, and the `runner` context gives expressions the same information:

| Environment variable | Context property | Example from the test run |
| --- | --- | --- |
| `RUNNER_OS` | `runner.os` | `Linux`, `Windows`, or `macOS` |
| `RUNNER_ARCH` | `runner.arch` | `X64` or `ARM64` |
| `RUNNER_NAME` | `runner.name` | `GitHub Actions 1000002762` |
| `RUNNER_ENVIRONMENT` | `runner.environment` | `github-hosted` |
| `RUNNER_TEMP` | `runner.temp` | `/home/runner/work/_temp` on Linux |
| `RUNNER_TOOL_CACHE` | `runner.tool_cache` | `/opt/hostedtoolcache` on Linux |

Use the variables in scripts and the context in expressions, like the `if: runner.os == 'Windows'` condition in this workflow. `RUNNER_TEMP` is emptied at the start and end of every job, so it's a good place for scratch files. The `runner` context isn't available in `runs-on` or in a job-level `if`, because those are evaluated before the job has a runner.

`ImageOS` and `ImageVersion` aren't in GitHub's list of default variables. The GitHub-hosted images set them, so other runners might not. `RUNNER_TRACKING_ID` and `RUNNER_WORKSPACE` aren't in the list either. For the documented variables, see [the runner context](https://docs.github.com/en/actions/reference/workflows-and-actions/contexts#runner-context) and [default environment variables](https://docs.github.com/en/actions/reference/workflows-and-actions/variables#default-environment-variables).

### Larger and self-hosted runners

Larger runners are GitHub-hosted runners with more CPUs, memory, and disk than the standard ones, and options such as GPUs, static IP addresses, and Azure private networking. Organizations and enterprises on GitHub Team or GitHub Enterprise Cloud can add them. GitHub always bills for them, even in public repositories and even when included minutes are left. See [About larger runners](https://docs.github.com/en/actions/concepts/runners/larger-runners).

A self-hosted runner is a machine you set up and register with GitHub. You choose its hardware and operating system, keep its software up to date, and decide whether it's cleaned up between jobs. GitHub doesn't charge for the minutes your jobs spend on it, but you pay for the machine. A pull request from a fork of a public repository could run code on it, so GitHub recommends self-hosted runners only for private repositories. [Lab 9](self-hosted-runners.md) sets one up.

### What a run costs

Standard GitHub-hosted runners are free in public repositories. In a private repository, every job uses your account's included minutes: 2,000 a month on GitHub Free and 3,000 on GitHub Pro, reset at the start of each month. GitHub rounds each job up to a whole minute, so each of your five jobs counts as a minute even though it ran for a few seconds.

Windows and macOS runners also cost more per minute than Linux runners. These are the list prices for standard runners:

| Runner | Price per minute |
| --- | --- |
| Linux, 1 CPU (`ubuntu-slim`) | $0.002 |
| Linux, arm64 | $0.005 |
| Linux, x64 | $0.006 |
| Windows, x64 or arm64 | $0.010 |
| macOS | $0.062 |

Your included minutes are used up at these prices, so a minute on Windows counts as about 1.7 Linux minutes, and a minute on macOS as about 10. One run of this lab's workflow comes to $0.093, or about 16 included minutes, and the macOS job accounts for about 10 of them. GitHub's billing pages may show your usage as a dollar amount rather than as minutes. Older documentation described the same idea as multipliers: 2 for Windows and 10 for macOS.

When an account without a payment method runs out of included minutes, GitHub blocks further usage until the minutes reset. With a payment method, the extra minutes are billed, and budgets can limit the spending. See [GitHub Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions) and [the runner pricing reference](https://docs.github.com/en/billing/reference/actions-runner-pricing).

## Check your result

Select the **Actions** tab, then **Inspect runners** in the left sidebar. Your run has a green check mark, and so do its five jobs: **Inspect ubuntu-latest**, **Inspect ubuntu-24.04-arm**, **Inspect windows-latest**, **Inspect windows-11-arm**, and **Inspect macos-latest**. If you left macOS out, there are four.

The run's **Summary** shows one table per job. Every table says the runner environment is `github-hosted`, and the architecture is `X64` for `ubuntu-latest` and `windows-latest` and `ARM64` for the others. In each job, **List the runner variables** prints this line:

```text
RUNNER_ENVIRONMENT=github-hosted
```

The **Summary** can also show notices under **Annotations**, such as one about the `ubuntu-latest` move. A notice is information only. It doesn't mean the run failed.

## If something fails

If the macOS job is still queued after the other jobs finish, macOS runners are probably busy. In the test run, a notice under **Annotations** warned that jobs on macOS arm64 runners may wait longer than usual because of capacity constraints. Give it a few minutes. If it still hasn't started, select **Cancel workflow**, and run the workflow again later or without `macos-latest`.

If a job whose label you changed stays queued while the others finish, check the label in the job's name for a typo. GitHub doesn't reject a label it doesn't recognize. The job waits for a runner with that label, and the run can't finish without it. When this lab was tested with `ubuntu-24.04-arm64` instead of `ubuntu-24.04-arm`, that job stayed queued until the run was canceled two minutes later. Select **Cancel workflow**, fix the label in `.github/workflows/runners.yml` on `main`, and run the workflow again.

If the **Actions** tab shows a failed run named `.github/workflows/runners.yml` right after you commit, GitHub couldn't read the workflow file. Open that run. Under **Annotations**, an **Invalid workflow file** error names the line with the problem. The usual cause is indentation that changed when you pasted the YAML. Compare your file with [the finished workflow](../../solutions/runners/runners.yml), fix it on `main`, and commit.

If there's no **Run workflow** button, select **Inspect runners** in the left sidebar first. If the button still doesn't appear, check that `.github/workflows/runners.yml` is on `main`. GitHub only shows the button for workflows on the default branch.

If every job stays queued for several minutes, check [GitHub Status](https://www.githubstatus.com) for an incident affecting GitHub Actions.

## Next step

The workflow only runs when you start it, so you can leave it in your repository and run it again later to see where the `-latest` labels point. To run jobs on a machine you manage, try [Lab 9](self-hosted-runners.md), or pick another lab from [the lesson list](../../README.md).
