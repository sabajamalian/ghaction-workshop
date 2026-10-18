---
title: "Lab 9: Run jobs on a self-hosted runner"
summary: "Register a self-hosted runner on a disposable machine, run a workflow on it, then remove the runner and its files."
duration: 60
difficulty: Advanced
order: 9
requires: "A new private practice repository, a disposable Linux machine such as a codespace or a virtual machine, and a terminal on that machine."
---

# Lab 9: Run jobs on a self-hosted runner

In the earlier labs, every job ran on a fresh virtual machine that GitHub created for it and deleted afterward. A self-hosted runner is a machine you manage: you install GitHub's runner application on it, register it with a repository, and it runs the jobs whose `runs-on` labels match it. You'll register one on a disposable machine, run a workflow that shows what changes, and then remove the runner and delete the machine.

## Before you start

This lab needs more setup than the others, and part of it happens in a terminal on a Linux machine. In a codespace, that terminal opens in your browser. On a virtual machine, you use a terminal on your computer. You need:

- A new private repository to practice in. Don't use your copy from [Lab 0](../foundation/00-create-repository.md): Lab 0 suggests making it public, and GitHub advises against self-hosted runners on public repositories, because anyone who can open a pull request could get their code to run on your machine. Even if your copy is private, a separate repository lets you delete everything together at the end. In this lab, "the practice repository" always means this new one.
- A disposable Linux machine: a codespace, a virtual machine on your computer, or a small virtual machine in the cloud. [Choose a machine](#choose-a-machine) compares them. Don't use your everyday computer. Every job the runner picks up can run any command with the rights of the runner's user.
- A terminal on that machine, and about an hour.

GitHub doesn't charge for the minutes jobs spend on a self-hosted runner, so this lab doesn't use your included Actions minutes. The machine can cost money, though: a codespace uses your free monthly Codespaces quota, and a cloud virtual machine is billed until you delete it.

> [!IMPORTANT]
> If you stop partway through, still follow [Clean up](#clean-up). A registered runner takes jobs whenever its application is running, and its credentials stay on the machine until you remove them.

## Choose a machine

| | Codespace | Virtual machine on your computer | Virtual machine in the cloud |
| --- | --- | --- | --- |
| You need | A browser | Multipass, UTM, or Hyper-V, and about 2 GB of free memory | A cloud account and an SSH client |
| Cost | Free monthly quota on personal accounts | Free | Billed until you delete it |
| GitHub credentials on the machine | A token in the `GITHUB_TOKEN` variable | Only what you add | Only what you add |
| Service section of this lab | Skip it | Do it | Do it |
| Cleanup | Delete the codespace | Delete the virtual machine | Delete the virtual machine and everything created with it |

Use a codespace unless you already have a virtual machine you're happy to throw away. A codespace is a container on a virtual machine that GitHub creates for you. It needs nothing but a browser, it's separate from your computer, and deleting it removes everything the runner leaves behind. Personal accounts get a free monthly quota of compute time and storage. Compute time counts per core: GitHub Free includes 120 hours, so an hour on a 2-core codespace uses 2 of them, and GitHub Pro includes 180. Storage counts until you delete the codespace, even while it's stopped. Usage over the quota is billed, or blocked if your account has no payment method. See [GitHub Codespaces billing](https://docs.github.com/en/billing/concepts/product-billing/github-codespaces).

A codespace has three catches:

- It has a GitHub token in its `GITHUB_TOKEN` environment variable. The token represents you and can read and write the repository the codespace was created from. A job on the runner can read it too, so the workflow checks for it, and [Run the workflow](#run-the-workflow) shows how to start the runner without it. See [Security in GitHub Codespaces](https://docs.github.com/en/codespaces/reference/security-in-github-codespaces#authentication).
- It stops after 30 minutes without activity, by default. Typing and terminal output count as activity, but a runner that's waiting for a job prints nothing. If you step away, the codespace stops and takes the runner offline.
- [GitHub's terms for Codespaces](https://docs.github.com/en/site-policy/github-terms/github-terms-for-additional-products-and-features#codespaces) say a codespace shouldn't be used for anything unrelated to developing or testing the project in the repository it was created from. So create it from the practice repository, register the runner only with that repository, and delete the codespace when you're done. A codespace isn't a place for a long-running runner.

A virtual machine on your computer is the next best choice. [Multipass](https://canonical.com/multipass/install) creates Ubuntu virtual machines with one command on Windows, macOS, and Linux, and this lab gives its commands. [UTM](https://getutm.app) on macOS and [Hyper-V](https://learn.microsoft.com/windows-server/virtualization/hyper-v/get-started/install-hyper-v) on Windows 10 or 11 Pro or Enterprise work too. Give the machine 2 CPUs, 2 GB of memory, and 10 GB of disk, install Ubuntu 24.04 or another Linux from [the supported operating systems](https://docs.github.com/en/actions/reference/runners/self-hosted-runners#supported-operating-systems), and don't share any folders between it and your computer.

A virtual machine in the cloud works if you already use a cloud provider. A small Linux machine with 2 CPUs and 2 GB of memory is enough. When you create it, sign in with an SSH key rather than a password, and allow inbound SSH only from your own IP address. The runner itself needs no inbound ports. Don't attach a managed identity, an IAM role, or other cloud credentials, because any job could ask the provider's metadata service for them. Write down everything the provider creates with the machine, such as its disk, public IP address, and network security group, so you can delete all of it in [Clean up](#clean-up).

## Create a practice repository

1. Go to [github.com/new](https://github.com/new).
2. Use the **Owner** menu to select your personal account.
3. In the **Repository name** box, type `self-hosted-practice`.
4. Choose **Private**.
5. Turn on the option to add a README file. The repository then starts with a commit on `main`, which the next steps expect.
6. Select **Create repository**.

## Add the workflow

1. Select the **Code** tab of the practice repository, and check that the branch menu shows `main`.
2. Select **Add file**, then **Create new file**.
3. In the file name box, type `.github/workflows/self-hosted.yml`. Each `/` you type starts a new folder.
4. Copy this workflow into the editor. YAML uses indentation to show structure, so keep the spaces exactly as they are.

```yaml source=solutions/self-hosted-runners/self-hosted.yml
name: Self-hosted runner

on:
  workflow_dispatch:

permissions:
  contents: read

defaults:
  run:
    shell: bash

jobs:
  inspect:
    name: Inspect the runner
    # If you gave your runner a different custom label, use it here and in the second job.
    runs-on: [self-hosted, workshop-lab9]
    timeout-minutes: 10
    steps:
      - name: Show the runner details
        run: |
          echo "Runner name:        $RUNNER_NAME"
          echo "Runner environment: $RUNNER_ENVIRONMENT"
          echo "Operating system:   $RUNNER_OS $RUNNER_ARCH ($(uname -sm))"
          echo "User:               $(whoami)"
          echo "Home folder:        $HOME"
          echo "Workspace:          $GITHUB_WORKSPACE"
          echo "Temporary folder:   $RUNNER_TEMP"

      - name: List the installed tools
        run: |
          for tool in git curl docker dotnet node python3 gh; do
            if command -v "$tool" > /dev/null; then
              echo "$tool: $(command -v "$tool")"
            else
              echo "$tool: not installed"
            fi
          done

      - name: Check for admin rights
        run: |
          if sudo -n true 2> /dev/null; then
            echo "This job can run any command as root with sudo, without a password."
          else
            echo "This job can't use sudo without a password."
          fi

      - name: Check the environment for a token
        run: |
          if [ -n "${GITHUB_TOKEN:-}" ]; then
            echo "GITHUB_TOKEN is set. It came from the environment the runner started in, and every job on this runner can read it."
          else
            echo "GITHUB_TOKEN isn't set."
          fi

      - name: Leave files on the runner
        run: |
          echo "Run $GITHUB_RUN_NUMBER, attempt $GITHUB_RUN_ATTEMPT, at $(date -u '+%Y-%m-%d %H:%M:%S') UTC" >> "$GITHUB_WORKSPACE/runs.txt"
          echo "Written by run $GITHUB_RUN_NUMBER" > "$RUNNER_TEMP/scratch.txt"
          ls -l "$GITHUB_WORKSPACE/runs.txt" "$RUNNER_TEMP/scratch.txt"

  leftovers:
    name: Look for files from earlier jobs
    needs: inspect
    runs-on: [self-hosted, workshop-lab9]
    timeout-minutes: 10
    steps:
      - name: Read the run history
        run: |
          if [ -f "$GITHUB_WORKSPACE/runs.txt" ]; then
            cat "$GITHUB_WORKSPACE/runs.txt"
          else
            echo "There's no runs.txt in $GITHUB_WORKSPACE."
          fi

      - name: Look for the temporary file
        run: |
          if [ -f "$RUNNER_TEMP/scratch.txt" ]; then
            echo "scratch.txt is still in $RUNNER_TEMP."
          else
            echo "scratch.txt is gone. The runner empties $RUNNER_TEMP at the start and end of every job."
          fi
```

5. Select **Commit changes**. In the dialog, make sure the commit goes directly to the `main` branch, then select **Commit changes**.

The file has to be on `main`, the practice repository's default branch, for GitHub to show a **Run workflow** button for it. Committing doesn't start a run, because `workflow_dispatch` is the workflow's only trigger. Don't run it yet: until a runner with matching labels is online, its jobs wait in the queue.

### What's in the workflow

- `runs-on: [self-hosted, workshop-lab9]` lists two labels, and a job only goes to a runner that has all of them. Every self-hosted runner gets three default labels: `self-hosted`, its operating system, and its architecture. `workshop-lab9` is a custom label you'll give your runner when you register it, so these jobs only go to a runner you gave that label.
- `timeout-minutes: 10` stops a job that's still running after 10 minutes.
- `defaults.run.shell: bash` runs every `run` step in Bash, which every machine in this lab has.
- **Show the runner details** prints variables the runner sets for every job, such as `RUNNER_NAME` and `RUNNER_ENVIRONMENT`, which is `self-hosted` here and `github-hosted` on GitHub's runners. It also prints the user the job runs as and the folders it works in.
- **List the installed tools** checks for seven common tools. GitHub-hosted runners come with a long list of software. A self-hosted runner has only what's installed on the machine.
- **Check for admin rights** runs `sudo -n true`, which only succeeds when the job's user can run commands as root without typing a password.
- **Check the environment for a token** looks for a `GITHUB_TOKEN` environment variable. The runner doesn't set one: workflows get their own token through `secrets.GITHUB_TOKEN`. If the variable is set, it came from the machine.
- **Leave files on the runner** appends a line to `runs.txt` in the workspace, the folder a job works in, and writes `scratch.txt` to the runner's temporary folder.
- The second job, **Look for files from earlier jobs**, waits for the first one (`needs: inspect`) and looks for both files. On a GitHub-hosted runner, it would get a new machine and find neither.
- There's no checkout step. The jobs don't need the repository's files, and `actions/checkout` cleans the workspace by default, which would delete `runs.txt`.

## Open a terminal on the machine

Follow the steps for the machine you chose, then [check the user and architecture](#check-the-user-and-architecture).

### In a codespace

1. In the practice repository, select the **Code** tab, then the **Code** button, then the **Codespaces** tab. A message at the bottom of the dialog says who pays for the codespace. For a repository in your personal account, that's you, from your free quota.
2. Select the plus button, **Create a codespace on main**. The codespace opens in the browser version of VS Code.
3. If there's no terminal at the bottom of the window, press Ctrl+Shift+P (Shift+Command+P on a Mac), type `View: Toggle Terminal`, and press Enter.

### In a virtual machine on your computer

With Multipass installed, run these commands in a terminal on your computer. The first creates an Ubuntu 24.04 virtual machine named `lab9`, and the second opens a shell in it:

```bash
multipass launch 24.04 --name lab9 --cpus 2 --memory 2G --disk 10G
multipass shell lab9
```

> [!WARNING]
> Always include the name `lab9` in Multipass commands. `multipass shell` without a name starts Multipass's [primary instance](https://canonical.com/multipass/docs/latest/how-to-guides/manage-instances/use-the-primary-instance/), a different virtual machine. On macOS and Linux, Multipass mounts your home folder inside the primary instance, where any job could read or change your files.

With UTM or Hyper-V, create an Ubuntu Server 24.04 virtual machine with 2 CPUs, 2 GB of memory, and a 10 GB disk, skip the shared folder options, and sign in.

### In a virtual machine in the cloud

Connect from a terminal on your computer with `ssh`, using the user name and address your provider shows for the machine.

### Check the user and architecture

In the terminal on the machine, run:

```bash
whoami
uname -m
```

`whoami` prints the user the runner will run as: `codespace` in a codespace and `ubuntu` in Multipass. It must not be `root`. `config.sh`, the runner's setup script, refuses to run as root, and jobs shouldn't have root's rights. If it is `root`, sign in as a regular user instead. Most cloud providers create one for you.

`uname -m` prints `x86_64` on an x64 machine and `aarch64` on an ARM64 machine. On a Mac with Apple silicon, Multipass and UTM virtual machines are ARM64.

## Download the runner

1. Go to your home folder. In a codespace, the terminal starts in the repository's folder under `/workspaces`, and keeping the runner out of it means it never shows up as changes to commit.

   ```bash
   cd ~
   ```

2. In the practice repository, select the **Settings** tab, then **Actions** and **Runners** in the left sidebar. Select **New self-hosted runner**.
3. Select Linux and the architecture that matches `uname -m`. The page shows the commands to download and configure the runner, with the current version and a registration token. Keep the page open.
4. Run the download commands from the page one at a time. For version 2.337.0 on x64, they look something like this. Use the version, file name, and checksum from your page, not these.

   ```bash
   # Create a folder
   mkdir actions-runner && cd actions-runner
   # Download the latest runner package
   curl -o actions-runner-linux-x64-2.337.0.tar.gz -L https://github.com/actions/runner/releases/download/v2.337.0/actions-runner-linux-x64-2.337.0.tar.gz
   # Optional: Validate the hash
   echo "70920811a4f8ad4328818682bca5c6469c1c942fab52448868071d0063816613  actions-runner-linux-x64-2.337.0.tar.gz" | shasum -a 256 -c
   # Extract the installer
   tar xzf ./actions-runner-linux-x64-2.337.0.tar.gz
   ```

5. Install the system libraries the runner needs. The runner is a .NET application, and this script installs what .NET needs with the machine's package manager. It ends with `Finish Install Dependencies`.

   ```bash
   sudo ./bin/installdependencies.sh
   ```

Here's what each download command does:

- `mkdir actions-runner && cd actions-runner` creates the runner folder in your home folder and moves into it. The runner keeps everything in this folder: the program, its settings, its logs, and the files jobs leave behind.
- `curl -o FILE -L URL` downloads the runner package from the [actions/runner releases](https://github.com/actions/runner/releases). `-o` names the saved file, and `-L` follows GitHub's redirect to the file.
- The `echo` line feeds the expected SHA-256 checksum and the file name to `shasum -a 256 -c`, which checks the download and prints the file name followed by `OK` when it matches. If the machine doesn't have `shasum`, use `sha256sum -c` instead.
- `tar xzf` extracts the package into the folder. `config.sh` registers and removes the runner, `run.sh` starts it, and `bin` and `externals` hold the runner itself.

In a minimal Ubuntu 24.04 container on an ARM64 machine, the ARM64 package for version 2.337.0 was 139 MB, and `sha256sum -c` printed this:

```text
actions-runner-linux-arm64-2.337.0.tar.gz: OK
```

In that container, `config.sh` stopped with a message about missing `Libicu` dependencies until `installdependencies.sh` installed `libicu74`.

The page can offer a version older than the latest one on the releases page. In October 2026, GitHub's list of runner downloads offered 2.337.0 while the latest release was 2.338.0. Use the version the page gives you. The runner updates itself later, as [Start the runner](#start-the-runner) explains.

## Register the runner

1. On the new-runner page, the configure command includes your repository's address and a registration token after `--token`. Don't copy that command. Run this one instead, with your GitHub user name in place of `OWNER`:

   ```bash
   ./config.sh --url https://github.com/OWNER/self-hosted-practice --name lab9-runner --labels workshop-lab9
   ```

2. When it asks `What is your runner register token?`, copy the token from the page, the value after `--token`, and paste it. The terminal shows a `*` for each character. Press Enter.
3. When it asks for a runner group, press Enter to accept `Default`. When it asks `Enter name of work folder:`, press Enter to accept `_work`.

After a banner, the output should look something like this:

```text
# Authentication

What is your runner register token? *****************************

√ Connected to GitHub

# Runner Registration

Enter the name of the runner group to add this runner to: [press Enter for Default]

√ Runner successfully added

# Runner settings

Enter name of work folder: [press Enter for _work]

√ Settings Saved.
```

> [!WARNING]
> Treat the registration token like a password. Paste it only at this prompt, never into a file or a script, and don't share it. Until it expires, an hour after the page created it, anyone who has it can register a runner with your repository, and that runner could receive your jobs.

Typing the token at the prompt also keeps it out of your shell history, where the page's command, with `--token`, would leave it.

### The configuration options

| Option | What it does |
| --- | --- |
| `--url` | The repository, organization, or enterprise to register with. Here, the practice repository. |
| `--token` | The registration token. Without it, `config.sh` asks for it. |
| `--name` | The runner's name on the **Runners** page. The default is the machine's host name. |
| `--labels` | Custom labels, separated by commas. The runner also gets the default labels. |
| `--ephemeral` | Takes one job, then removes the runner's registration. You'll try it later in this lab. |
| `--unattended` | Never asks a question. Missing options get their defaults, and `--url` and `--token` become required. Scripts that set up runners use it. |
| `--runnergroup` | The runner group to join. Without it, the runner joins the default group. |
| `--work` | The work folder for jobs. The default is `_work`, inside the runner folder. |
| `--replace` | Replaces a runner that already has the same name, instead of asking. |
| `--disableupdate` | Turns off automatic updates. You then have to update the runner yourself. |
| `--no-default-labels` | Leaves out the default labels. You then have to give `--labels`. |

`./config.sh --help` lists every option.

Registration saved the runner's settings in `.runner` and the credentials it uses to connect to GitHub in `.credentials` and `.credentials_rsaparams`, all in the runner folder. `ls -a` shows them. Jobs run as the same user as the runner, so any job can read these files. Registration also created `svc.sh`, which installs the runner as a service.

## Start the runner

Start the runner in the terminal:

```bash
./run.sh
```

It connects to GitHub and waits for jobs. The output should look something like this:

```text
√ Connected to GitHub

Current runner version: '2.337.0'
2026-10-18 10:31:02Z: Listening for Jobs
```

Leave it running. In the practice repository, select the **Settings** tab, then **Actions** and **Runners** in the left sidebar. `lab9-runner` is listed with the status **Idle**, which means it's connected and waiting for a job. Select its name to see its labels: `self-hosted`, `Linux`, the architecture, and `workshop-lab9`.

When a new version of the runner is released, the runner updates itself the next time it's assigned a job, or within a week if it gets no jobs. If yours is older than the latest release, it may update before it runs its first job. It prints `Runner update in progress, do not shutdown runner.`, downloads the new version, and restarts. Let it finish.

## Run the workflow

1. Select the **Actions** tab, then **Self-hosted runner** in the left sidebar.
2. Select **Run workflow**. In the menu that opens, leave the branch set to `main`, then select the green **Run workflow** button.
3. After a few seconds, a new run appears at the top of the list. If it doesn't, refresh the page. Select the run.

The terminal should show each job as the runner takes it, something like this:

```text
2026-10-18 10:35:12Z: Running job: Inspect the runner
2026-10-18 10:35:20Z: Job Inspect the runner completed with result: Succeeded
2026-10-18 10:35:23Z: Running job: Look for files from earlier jobs
2026-10-18 10:35:27Z: Job Look for files from earlier jobs completed with result: Succeeded
```

While a job runs, the **Runners** page shows the runner as **Active**.

### Read the logs

1. Under **Jobs** in the left sidebar, select **Inspect the runner**, and expand **Set up job**. Where a job on a GitHub-hosted runner describes the runner image, this one names the runner version, your runner, and the machine. It may also name a runner group. The lines should look something like this, with your machine's host name:

   ```text
   Current runner version: '2.337.0'
   Runner name: 'lab9-runner'
   Machine name: 'lab9'
   ```

2. Expand **Show the runner details**. In a codespace, it should look something like this. In Multipass, the user is `ubuntu` and the folders start with `/home/ubuntu`.

   ```text
   Runner name:        lab9-runner
   Runner environment: self-hosted
   Operating system:   Linux X64 (Linux x86_64)
   User:               codespace
   Home folder:        /home/codespace
   Workspace:          /home/codespace/actions-runner/_work/self-hosted-practice/self-hosted-practice
   Temporary folder:   /home/codespace/actions-runner/_work/_temp
   ```

   The job ran as the user who started `run.sh`, inside the runner's `_work` folder. On a GitHub-hosted Linux runner, the user is `runner`, and the folders are under `/home/runner/work`.

3. Expand **List the installed tools**. GitHub-hosted Ubuntu runners have all seven tools. Yours has whatever the machine came with. A workflow that needs a tool on a self-hosted runner has to install it or rely on the machine having it, and whatever one job installs is still there for the next.
4. Expand **Check for admin rights**. In a codespace and in Multipass, the default user can use `sudo` without a password, so every job on this runner can run commands as root: install software, read any file, or change the system.
5. Expand **Check the environment for a token**. On a virtual machine, it says `GITHUB_TOKEN isn't set`. In a codespace, it should say `GITHUB_TOKEN is set`: the runner passes the environment it started in to every job, so any job could read the codespace's token and use it to read and write the practice repository as you.
6. Under **Jobs**, select **Look for files from earlier jobs**, and expand its steps. **Read the run history** prints the line the first job wrote, so the second job found the first job's file. **Look for the temporary file** says `scratch.txt is gone`. The runner empties `_work/_temp` at the start and end of every job, but it leaves the workspace alone.

In a codespace, you can start the runner without the token. Stop it with Ctrl+C, and start it again with the variable removed:

```bash
env -u GITHUB_TOKEN ./run.sh
```

That's only a partial fix. Jobs still run as the same user as your terminal and VS Code, and on Linux, a process can read the environment of other processes that belong to the same user. A codespace is fine for this lab, but it's not a place to run code you don't trust.

### Run it again

Run the workflow again, the same way. This time, **Read the run history** prints two lines, something like this:

```text
Run 1, attempt 1, at 2026-10-18 10:35:18 UTC
Run 2, attempt 1, at 2026-10-18 10:41:03 UTC
```

If you restarted the runner in a codespace with `env -u GITHUB_TOKEN`, **Check the environment for a token** now says `GITHUB_TOKEN isn't set`.

Everything a job leaves outside the temporary folder is still there for the next job: files in the workspace and the home folder, installed packages, changed settings, and caches. On a GitHub-hosted runner, each job gets a new machine and finds none of it.

## Run the runner as a service

A runner started with `run.sh` stops when you close the terminal. On a server, you install it as a service that starts with the machine, and on Linux, that's a systemd service. Skip this section in a codespace, which is a container: the service script needs systemd, and containers don't usually run it. Continue with [Try an ephemeral runner](#try-an-ephemeral-runner).

1. Stop the runner with Ctrl+C. It prints `Exiting...`.
2. Install the service and start it:

   ```bash
   sudo ./svc.sh install
   sudo ./svc.sh start
   ```

   `install` creates a systemd service named after the repository and the runner, such as `actions.runner.OWNER-self-hosted-practice.lab9-runner.service`, and sets it to start with the machine. It runs the runner as the user who ran `sudo`, and its output names that user. `start` starts the service and shows its status, which should include `active (running)`.

3. Check that the **Runners** page shows `lab9-runner` as **Idle**, then run the workflow again. To follow the runner's output while it runs, use this command, and press Ctrl+C to stop following. `.service` is a file that holds the service's name.

   ```bash
   sudo journalctl -u "$(cat .service)" -f
   ```

4. Stop and remove the service:

   ```bash
   sudo ./svc.sh stop
   sudo ./svc.sh uninstall
   ```

On a real server, run the service as a dedicated user that can't use `sudo`, so jobs can't change the rest of the system. `sudo ./svc.sh install USERNAME` sets that user, and the user needs to own the runner folder. See [Configuring the self-hosted runner application as a service](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/configure-the-application).

## Try an ephemeral runner

An ephemeral runner takes one job, and then GitHub removes its registration. Combined with a machine that's deleted after each job, every job starts clean, the way it does on GitHub-hosted runners. GitHub recommends ephemeral runners when you add and remove self-hosted runners automatically.

1. If the runner is running in the terminal, stop it with Ctrl+C.
2. Remove the `lab9-runner` registration. On the **Runners** page, select `lab9-runner`, then **Remove**. The instructions include a command with a removal token. Run the command without the token:

   ```bash
   ./config.sh remove
   ```

   When it asks `Enter runner remove token:`, paste only the token from the instructions and press Enter. It should print `√ Runner removed successfully`, then remove `.credentials` and `.runner`. The runner disappears from the **Runners** page.

3. Select **New self-hosted runner** again for a new registration token. Register an ephemeral runner, answering the questions as in [Register the runner](#register-the-runner), and start it:

   ```bash
   ./config.sh --url https://github.com/OWNER/self-hosted-practice --name lab9-ephemeral --labels workshop-lab9 --ephemeral
   ./run.sh
   ```

4. Run the workflow. The runner takes **Inspect the runner**, and when that job finishes, the runner removes its registration and exits. The end of its output should look something like this:

   ```text
   2026-10-18 10:52:40Z: Job Inspect the runner completed with result: Succeeded
   √ Removed .credentials
   √ Removed .runner
   Runner listener exit with 0 return code, stop the service, no retry needed.
   Exiting runner...
   ```

   `lab9-ephemeral` is gone from the **Runners** page, and **Look for files from earlier jobs** waits in the queue, because no runner with its labels is left.

5. Repeat step 3 to register and start another ephemeral runner. It takes the waiting job, and **Read the run history** prints a line for every run so far. GitHub removed the registration, but the files on the machine stayed. A clean machine for every job takes more than `--ephemeral`: something also has to replace the machine.

Doing that by hand doesn't scale, so teams automate it. [JIT runners](https://docs.github.com/en/actions/reference/security/secure-use#using-just-in-time-runners) are created with the REST API instead of `config.sh`, and like ephemeral runners, they run at most one job before GitHub removes them. [Actions Runner Controller](https://docs.github.com/en/actions/concepts/runners/actions-runner-controller) runs ephemeral runners in containers on Kubernetes and scales them up and down with the number of jobs. See [ephemeral runners for autoscaling](https://docs.github.com/en/actions/reference/runners/self-hosted-runners#ephemeral-runners-for-autoscaling).

## What happened

### GitHub-hosted and self-hosted runners

| | GitHub-hosted runner | Self-hosted runner |
| --- | --- | --- |
| Machine | A new virtual machine for each job, deleted afterward | Your machine, reused for every job until you replace it |
| Software | An image with a long list of tools, updated weekly | Whatever you install, and you keep it up to date |
| Between jobs | Nothing carries over | Files, installed software, and settings carry over |
| Network | GitHub's network | Your network, and anything the machine can reach |
| Cost | Free in public repositories. In private ones, included minutes, then a price per minute | No charge for the minutes, but you pay for the machine |
| Setup | None | You install, register, update, and remove it |

Self-hosted runners make sense when jobs need hardware, software, or network access that GitHub-hosted runners don't have, such as a device attached to the machine, a licensed tool, or a server on a private network. See [About self-hosted runners](https://docs.github.com/en/actions/concepts/runners/self-hosted-runners).

### How the job found your runner

GitHub never connected to your machine. The runner connected out to GitHub over HTTPS on port 443 and waited for jobs, and GitHub's [communication requirements](https://docs.github.com/en/actions/reference/runners/self-hosted-runners#requirements-for-communication-with-github) list only outbound connections. A self-hosted runner needs no open inbound ports, so it can sit behind a firewall that allows outbound HTTPS to [GitHub's domains](https://docs.github.com/en/actions/reference/runners/self-hosted-runners#accessible-domains-by-function). Every runner needs `github.com`, `api.github.com`, and `*.actions.githubusercontent.com`. Other domains are needed for downloading actions, artifacts and caches, runner updates, and packages.

When a job is queued, GitHub looks for an online, idle runner that has every label in its `runs-on` list. If it finds one, it assigns the job, and if that runner doesn't pick it up within 60 seconds, the job goes back in the queue. If there isn't one, the job waits in the queue until a matching runner is free, for up to 24 hours. See [routing precedence for self-hosted runners](https://docs.github.com/en/actions/reference/runners/self-hosted-runners#routing-precedence-for-self-hosted-runners). Labels aren't case-sensitive, and you can add or remove custom labels on the runner's page. See [Using labels with self-hosted runners](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/apply-labels).

### Runner groups

Organizations and enterprises use runner groups to control which repositories can send jobs to which runners. Every organization has a default group, and organization owners on the GitHub Team plan can create more groups and limit each one to selected repositories. A runner belongs to one group at a time and joins the default group unless `--runnergroup` names another. A repository owned by a personal account, like the practice repository, has no runner groups to manage: a runner registered with it serves only that repository. See [Runner groups](https://docs.github.com/en/actions/concepts/runners/runner-groups).

### What stays on the machine

Every job runs with the rights of the runner's user, and a workflow can run any command. Whoever gets a workflow onto your runner can read the runner's credentials, read or change the user's files, and reach anything on the machine's network, including a cloud provider's metadata service. A job can also change the runner's own files, which belong to the same user, or install a program that later jobs run. GitHub's [hardening guidance for self-hosted runners](https://docs.github.com/en/actions/reference/security/secure-use#hardening-for-self-hosted-runners) warns that a self-hosted runner can be persistently compromised by untrusted code in a workflow.

That's why this lab uses a private repository. GitHub says self-hosted runners should almost never be used for public repositories, because anyone can open a pull request against one, and a workflow could run that pull request's code on your machine. A private repository narrows that down to the people who can open pull requests in it, which generally means anyone with read access.

The runner cleans up very little. When a job ends, it stops the processes it can tell the job started, and it empties `_work/_temp` at the start and end of every job. Everything else stays: the workspace, the home folder, installed packages, and settings. For a runner you keep, use a user without `sudo` rights on a machine that holds no other credentials, and prefer ephemeral runners on machines that are replaced after every job.

### What it costs

GitHub doesn't charge for Actions minutes on self-hosted runners, in public or private repositories, and they don't use your included minutes. You pay for the machine and its network. GitHub announced a $0.002 per minute platform charge for self-hosted runners in private repositories, starting March 1, 2026, and then postponed it to re-evaluate the plan. In October 2026, GitHub's billing documentation still said self-hosted runner usage is free. Check [GitHub Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions) for the current rules.

### Keeping the runner up to date

The runner application updates itself, but the operating system and tools are yours to maintain. If you turn off updates with `--disableupdate`, you have to install each new runner version within 30 days of its release, or GitHub stops sending jobs to the runner. It also stops sending jobs to an out-of-date runner when a critical security update is required. See [runner software updates](https://docs.github.com/en/actions/reference/runners/self-hosted-runners#runner-software-updates-on-self-hosted-runners).

## Check your result

In the practice repository, select the **Actions** tab, then **Self-hosted runner** in the left sidebar. Your runs have green check marks, including the ephemeral one, once the second ephemeral runner finished its waiting job. In every run, **Show the runner details** prints this line:

```text
Runner environment: self-hosted
```

In the latest run, **Read the run history** prints one line for each run, because every job used the same workspace on the same machine.

## If something fails

If `config.sh` stops with `Libicu's dependencies is missing for Dotnet Core 6.0`, the machine is missing libraries the runner needs. Run `sudo ./bin/installdependencies.sh` in the runner folder, then run `config.sh` again.

If `config.sh` prints `Must not run with sudo`, you ran it with `sudo` or as `root`. Run it as a regular user. Only `installdependencies.sh` and `svc.sh` need `sudo`.

If registration ends with `Response status code does not indicate success: 404 (Not Found).`, GitHub didn't accept the registration. The token may be mistyped or expired, since registration tokens expire after an hour, or `--url` may not be the practice repository's address. Select **New self-hosted runner** again for a new token, check the address, and run `config.sh` again.

If `config.sh` says `Cannot configure the runner because it is already configured.`, the folder still holds a registration. Remove it with `./config.sh remove` first, as in [Try an ephemeral runner](#try-an-ephemeral-runner).

If `config.sh` says `A runner exists with the same name` and asks `Would you like to replace the existing runner? (Y/N)`, a runner with that name is already registered with the repository, maybe from an earlier attempt. Type `Y` to replace it.

If `./run.sh` prints `An error occurred: Not configured. Run config.(sh/cmd) to configure the runner.`, the folder has no registration, which is what happens after an ephemeral runner finishes its job. Run `config.sh` first.

If the run stays queued and the job's page says `Waiting for a runner to pick up this job...`, no online, idle runner has every label in `runs-on`. The job waits, with no steps, until a matching runner is free or 24 hours pass. Check that the terminal says `Listening for Jobs`, that the **Runners** page shows your runner as **Idle**, and that its labels include `workshop-lab9`. If you gave the runner another label, use that label in both `runs-on` lines. To stop waiting, select **Cancel workflow**.

If `./run.sh` says `A session for this runner already exists.`, another copy of the runner is already running with the same registration, such as the service. Stop the other copy, for example with `sudo ./svc.sh stop`.

If `./config.sh remove` stops with `Uninstall service first`, run `sudo ./svc.sh stop` and `sudo ./svc.sh uninstall`, then try again. If `svc.sh` prints `Must run as sudo`, run it with `sudo`.

If the **Runners** page shows your runner as **Offline**, the runner application isn't running or can't reach GitHub. Check the terminal or the service. A codespace stops after 30 minutes without activity, and the runner stops with it: restart the codespace from [your codespaces](https://github.com/codespaces), then run `cd ~/actions-runner` and `env -u GITHUB_TOKEN ./run.sh`. The runner's logs are in the `_diag` folder, in files whose names start with `Runner_` and `Worker_`. See [Monitoring and troubleshooting self-hosted runners](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/monitor-and-troubleshoot).

If every job stays queued while your runner shows as **Idle**, check [GitHub Status](https://www.githubstatus.com) for an incident affecting GitHub Actions.

## Clean up

Do every step, even if you stopped partway through the lab.

1. Stop the runner. If it's running in the terminal, press Ctrl+C. If you installed the service and didn't uninstall it, run `sudo ./svc.sh stop` and then `sudo ./svc.sh uninstall` in the runner folder.
2. Remove the registration. On the **Runners** page, select the runner, then **Remove**, and run `./config.sh remove` with the token from the instructions, as in [Try an ephemeral runner](#try-an-ephemeral-runner). If your last runner was ephemeral and already removed itself, skip this step. `config.sh remove` would only report that the config files are missing.
3. Delete the runner folder. It holds the runner, its logs, and the files your jobs left behind.

   ```bash
   cd ~ && rm -rf ~/actions-runner
   ```

4. Delete the machine:
   - Codespace: on [your codespaces](https://github.com/codespaces), select the three dots to the right of the codespace, then **Delete**.
   - Multipass: run `multipass delete --purge lab9` in a terminal on your computer.
   - UTM or Hyper-V: delete the virtual machine and its disk.
   - Cloud: delete the virtual machine and everything you wrote down when you created it.
5. In the practice repository, select the **Settings** tab, then **Actions** and **Runners** in the left sidebar. The list should be empty. If a runner is still listed, for example because you deleted the machine first, select it, then **Remove**, then **Force remove this runner**.
6. If you don't need the practice repository anymore, delete it. See [Deleting a repository](https://docs.github.com/en/repositories/creating-and-managing-repositories/deleting-a-repository).

## Next step

The optional labs can be done in any order, so pick your next one from [the lesson list](../../README.md). To compare what you saw here with GitHub's own machines, try [Lab 3: Compare runners](runners.md).
