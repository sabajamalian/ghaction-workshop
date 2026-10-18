# Contributing

This repository holds the workshop itself: the lessons, the dashboard that serves them, and the workflows that learners run. If you're working through the labs, do that in your own copy of the template, not here.

## Before you write anything

Read [AGENTS.md](AGENTS.md) and [the editorial guide](docs/editorial-guide.md). Between them they cover the writing rules and the conventions for lessons, workflows, and the dashboard. Before you open a pull request, review your change with [the editorial checklist](docs/editorial-checklist.md).

## Build and run

You need the .NET 10 SDK. `global.json` asks for 10.0.100 and rolls forward, so any .NET 10 SDK you have installed works.

```bash
dotnet build
dotnet test
dotnet run --project src/WorkshopDashboard
```

The dashboard runs at `http://localhost:5080`. In the Development environment it reads the lessons again when you refresh the page, so you can edit a lesson and check the result without restarting.

## Tests

`dotnet test` runs two projects:

| Project | What it covers |
| --- | --- |
| `tests/WorkshopCore.Tests` | The `Calculator` tests that learners run and break in Lab 2. |
| `tests/WorkshopDashboard.Tests` | Markdown rendering and its security rules, the pages and response headers, and the content checks for lessons and documentation. |

The content checks in `ContentTests` read every lesson, document, and workflow in the repository. When one fails, it lists each problem as `path:line: message`, and the message says how to fix it. To run only those checks while you edit:

```bash
dotnet test tests/WorkshopDashboard.Tests --filter "FullyQualifiedName~ContentTests"
```

The editorial guide lists the rules in [What the tests check](docs/editorial-guide.md#what-the-tests-check).

Keep `tests/WorkshopCore.Tests` small and readable. Learners open those tests in the browser, and Lab 2 depends on a test failing when they change `Calculator`. If you change the tests or the class, check that Lab 2 still describes what happens.

Both projects reference `xunit.v3.mtp-off`, which keeps them on the VSTest runner, so `dotnet test` works without extra configuration. Coverage comes from `coverlet.collector`. This command writes a Cobertura file into each test project's `TestResults` folder:

```bash
dotnet test --collect "XPlat Code Coverage"
```

## Workflows

Every workflow in `.github/workflows/` ends up in every copy of the template, so those workflows use only `workflow_dispatch`. The rules for pinning actions, permissions, and untrusted input are in [the Workflows section of AGENTS.md](AGENTS.md#workflows). Run `actionlint` on every workflow you change.

To move an action to a new release, resolve the release tag to its commit SHA, then update the SHA and the version comment together:

```bash
gh api repos/actions/checkout/commits/v7.0.1 --jq .sha
```

Update every file that uses the action, except Lab 11's lock file, which gh-aw generates. If a lesson shows that workflow, the content tests fail until the lesson's snippet matches the file again.

Lab 6 installs a fixed release of Copilot CLI, set by `COPILOT_VERSION` in `solutions/copilot-cli/copilot-report.yml`. `npm view @github/copilot version` shows the latest release. To move to it, change the version in the workflow and in the lesson's copy of the workflow, run the workflow in a copy of the template, and update the version in the lesson's log excerpts. The content tests don't check the excerpts.

Lab 11's lock file, `solutions/agentic-workflows/ci-review.lock.yml`, was compiled with gh-aw v0.92.0, which also chose the actions it pins. Never edit it by hand. To compile it with a newer release:

1. Run `gh extension upgrade gh-aw`, then `gh aw version` to see the new version.
2. In a copy of the template, add `solutions/agentic-workflows/ci-review.md` as `.github/workflows/ci-review.md`, run `gh aw compile ci-review`, and push. Run the workflow once to check it. Don't compile in this repository: every `gh aw compile`, even with `--no-emit`, also writes `.gitattributes` and `.github/aw/actions-lock.json`, and Lab 11 has learners create and commit those files themselves.
3. Copy the new `.github/workflows/ci-review.lock.yml` over the solution's lock file.
4. Update the gh-aw version in the lesson. The lesson also shows two excerpts of the lock file, and the content tests fail until they match the new file.

## highlight.js

The dashboard highlights code with highlight.js 11.12.0. It's stored in `src/WorkshopDashboard/wwwroot/lib/highlight`, so the site doesn't load anything from a CDN. `highlight.min.js` is the standard build with the common languages, and `languages/powershell.min.js` adds PowerShell. The colors are in `wwwroot/css/site.css`, not in a highlight.js theme file.

To update it:

1. From the [highlight.js CDN release repository](https://github.com/highlightjs/cdn-release), download `build/highlight.min.js`, `build/languages/powershell.min.js`, and `LICENSE` at the new version's tag.
2. Replace the files in `src/WorkshopDashboard/wwwroot/lib/highlight`, keeping the same names.
3. Run the dashboard, open a lesson that has YAML, Bash, and C# blocks, and check that they're highlighted in both the light and dark themes.

## Pull requests

- Say what you changed and how you checked it.
- If something couldn't be tested, such as a deployment that needs an Azure subscription, say what and why.
- When the change includes text that people read, copy [the editorial checklist](docs/editorial-checklist.md) into the description and work through it.
