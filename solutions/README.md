# Solutions

This folder has the finished workflow files for the labs. GitHub only runs workflow files that are in the `.github/workflows` folder, so nothing in this folder runs on its own.

Use a solution to check your work, or to catch up if a lab asks for a workflow you haven't written.

| Folder | Lab |
| --- | --- |
| `tests` | [Lab 2: Run tests on pull requests](../lessons/foundation/02-test-pull-request.md) |
| `runners` | [Lab 3: Compare runners](../lessons/explore/runners.md) |
| `triggers` | [Lab 4: Trigger workflows and pass inputs](../lessons/explore/triggers-and-inputs.md) |
| `jobs-and-artifacts` | [Lab 5: Pass files between jobs](../lessons/explore/jobs-and-artifacts.md) |
| `copilot-cli` | [Lab 6: Generate a report with Copilot CLI](../lessons/explore/copilot-cli.md) |
| `azure-deployment` | [Lab 7: Deploy to Azure with OIDC](../lessons/explore/azure-oidc.md) |
| `environments` | [Lab 8: Add a staging environment](../lessons/explore/environments.md) |
| `self-hosted-runners` | [Lab 9: Run jobs on a self-hosted runner](../lessons/explore/self-hosted-runners.md) |
| `reusable-workflows` | [Lab 10: Reuse workflows and steps](../lessons/explore/reusable-workflows.md) |
| `agentic-workflows` | [Lab 11: Review CI failures with an agentic workflow](../lessons/explore/agentic-workflows.md) |
| `migration` | [Lab 12: Migrate an Azure DevOps pipeline](../lessons/explore/azure-devops-migration.md) |
| `debug-workflows` | [Lab 13: Debug a broken workflow](../lessons/explore/debug-workflows.md) |
| `automate-issues-prs` | [Lab 14: Automate issues and pull requests](../lessons/explore/automate-issues-prs.md) |
| `secure-workflows` | [Lab 15: Secure your workflows](../lessons/explore/secure-workflows.md) |
| `custom-action` | [Lab 16: Build your own action](../lessons/explore/custom-action.md) |
| `service-containers` | [Lab 17: Run integration tests with a service container](../lessons/explore/service-containers.md) |
| `foundry-standards` | [Lab 18: Check coding standards with a Microsoft Foundry model](../lessons/explore/foundry-standards.md) |

## Copy a solution into your repository

A workflow starts running on its triggers as soon as it's in `.github/workflows`, so only copy the solution the lab asks for.

1. In your repository on GitHub, open the solution file, such as `solutions/tests/tests.yml`, and copy its contents.
2. Select the **Code** tab, and switch the branch menu to the branch the lab uses.
3. Select **Add file**, then **Create new file**.
4. In the file name box, type the path the lab gives, such as `.github/workflows/tests.yml`.
5. Paste the contents into the editor. Keep the indentation exactly as it is.
6. Select **Commit changes**, then select **Commit changes** in the dialog.

Lab 9's solution goes in the separate practice repository that the lab creates, not in your copy. Open it in your copy, then follow the steps above in the practice repository.

Lab 11's solution has two files: `ci-review.md`, which you add during the lab, and the compiled `ci-review.lock.yml`, which is almost 2,000 lines long. To copy the lock file, follow [Use the compiled solution instead](../lessons/explore/agentic-workflows.md#use-the-compiled-solution-instead) in Lab 11.

Lab 12 has a solution for each way of converting the pipeline, and both go in `.github/workflows/migrated-ci.yml`. `migrated-ci.yml` is the repaired workflow from Part B. `imported-ci.yml` is GitHub Actions Importer's output after the repairs in Part C, with two comment lines at the top that your file won't have.

Some solutions aren't workflows, so they go somewhere other than `.github/workflows`. Lab 14's `labeler.yml` goes in `.github/labeler.yml`, and Lab 15's `dependabot.yml` goes in `.github/dependabot.yml`. Lab 16's `count-lessons` folder holds the action, and its two files go in `.github/actions/count-lessons`. Each lab gives the exact path.

## Exercises

The `exercises` folder at the root of the repository has the starting files that some labs ask you to copy, as they are before you change them. Lab 13's `broken-ci.yml` has the planted problems you fix, and its fixed version is `solutions/debug-workflows/fixed-ci.yml`. Lab 18's `coding-standards.md` and `HistoryStore.cs` are the team's rules and a file that breaks them.
