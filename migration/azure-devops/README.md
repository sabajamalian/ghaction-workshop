# Azure DevOps migration files

These files are for [Lab 12: Migrate an Azure DevOps pipeline](../../lessons/explore/azure-devops-migration.md). Nothing in this folder runs. GitHub only runs workflow files that are in `.github/workflows`, and the lab doesn't use an Azure DevOps organization.

| File | What it is |
| --- | --- |
| `azure-pipelines.yml` | The Azure Pipelines CI pipeline the lab migrates. It builds the workshop app, runs the tests with Coverlet, and publishes the test results, a Cobertura coverage report, and the app. |
| `workflow-to-repair.yml` | A hand-converted workflow with three problems in it. In Part B, learners copy it to `.github/workflows/migrated-ci.yml`, run it, and fix it. |
| `expected-workflow.yml` | The reference conversion. It builds the same app and does the same testing and reporting in GitHub Actions. |

## The reference workflow and the solutions

`expected-workflow.yml` is the canonical reference. [`solutions/migration/migrated-ci.yml`](../../solutions/migration/migrated-ci.yml) is an exact copy, named for the path learners create, so the `solutions` folder works the same way for every lab. [`solutions/migration/imported-ci.yml`](../../solutions/migration/imported-ci.yml) is GitHub Actions Importer's output for the pipeline after the repairs in Part C. It keeps the importer's formatting, so learners can compare their file with it using `diff`.

The lesson shows `azure-pipelines.yml`, `workflow-to-repair.yml`, and `solutions/migration/migrated-ci.yml` in full, and quotes parts of `expected-workflow.yml` and `solutions/migration/imported-ci.yml`. The content tests compare those code blocks with the files, and check that `solutions/migration/migrated-ci.yml` is still an exact copy of `expected-workflow.yml`. To change the reference, edit `expected-workflow.yml`, copy it over `solutions/migration/migrated-ci.yml`, and run the content tests.

`workflow-to-repair.yml` matches the reference except for its header comment and the three problems: no `--collect "XPlat Code Coverage"` in the test step, an Azure Pipelines path in the test results upload, and no step that uploads the published app. Keep it that way, because the lesson's hints depend on it.

If you change `azure-pipelines.yml`, or a newer importer release converts it differently, run the Part C dry run again. Update the converted workflow, the log lines, and the version numbers that the lesson shows, repair the new output into `solutions/migration/imported-ci.yml`, and check that the lesson's repair steps still turn one into the other.

## How the pipeline was checked

The pipeline hasn't run in Azure DevOps. In October 2026, it was checked three ways:

- Every task name and input comes from the [Azure Pipelines task reference](https://learn.microsoft.com/azure/devops/pipelines/tasks/reference/?view=azure-pipelines), and was compared with each task's `task.json` in [microsoft/azure-pipelines-tasks](https://github.com/microsoft/azure-pipelines-tasks).
- The file validates against `service-schema.json` from [microsoft/azure-pipelines-vscode](https://github.com/microsoft/azure-pipelines-vscode), the schema the Azure Pipelines extension for Visual Studio Code uses. That schema has no entry for `PublishCodeCoverageResults@2`, so one was added from the task's `task.json` before validating.
- GitHub Actions Importer reads it with `--source-file-path` and produces the workflow that Part C of the lab shows.
