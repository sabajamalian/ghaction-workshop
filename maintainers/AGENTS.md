# AGENTS.md

This repository is a self-paced GitHub Actions workshop. The ASP.NET Core site in `src/WorkshopDashboard` serves the lessons, and the repository is also a template: learners copy it and do every lab in their own repository. Anything you add here ends up in those copies.

These instructions are for everyone who changes the repository, including GitHub Copilot and other coding agents. The writing rules below condense [the editorial guide](editorial-guide.md), which has the full rules and examples. Read it before you write or rewrite anything a person will read. Maintainers review changes with [the editorial checklist](editorial-checklist.md).

This file is in `maintainers/` instead of the repository root on purpose. Copilot CLI and gh-aw load an `AGENTS.md` at the root of every learner's copy as instructions, and these rules are for the workshop's maintainers, not for learners. To load this file, start Copilot CLI with `COPILOT_CUSTOM_INSTRUCTIONS_DIRS` set to this folder, as [CONTRIBUTING.md](../CONTRIBUTING.md) shows. Don't add an `AGENTS.md` or `.github/copilot-instructions.md` at the root.

## Where the writing rules apply

They apply to every piece of text a person reads:

- `README.md`, `CONTRIBUTING.md`, this file, and all Markdown documentation.
- Lessons in `lessons/`.
- Dashboard UI text in `src/WorkshopDashboard`: page titles, headings, lesson cards, buttons, labels, navigation, and every status, error, and success message.
- Code comments that learners will read, in workflow files, solutions, scripts, and `src/WorkshopCore`.
- Content that Copilot or another agent generates for this repository, including pull request descriptions.

Before you write something new, read a few existing lessons and pages and match their voice. A consistent voice doesn't mean identical headings, paragraph lengths, or sentence patterns.

## Voice

Write like a senior developer helping a colleague who's sitting next to them: conversational, practical, technically accurate, and restrained. Learners may be experienced programmers who are new to GitHub, CI/CD, or C#.

- Write the sentence you'd actually say to another developer.
- Don't make a sentence longer or more formal to sound professional. Don't add enthusiasm to make an ordinary instruction sound interesting.
- Don't explain what the previous sentence already made clear, and don't write sentences just to fill out a section.
- Don't call a task easy, simple, or straightforward. It may be new to the reader.
- Don't add jokes, slang, or personal stories to sound human, and never claim to have done or seen something that didn't happen, such as "When I first used GitHub Actions, I was amazed by..."
- Don't compliment the learner or exaggerate what they did.

## Words and phrases to avoid

Don't use these words unless they're the correct technical term: leverage, unlock, elevate, seamless, robust, comprehensive, delve, landscape, journey, transformative, cutting-edge, powerful, game-changing, revolutionary, harness, empower, streamline, optimize, innovative, next-generation, holistic, pivotal, crucial.

Write "use" instead of "leverage," "improve" instead of "elevate," and "look at" or "examine" instead of "delve into." Accuracy comes first: if a technical term needs one of these words, use it.

Don't use these expressions:

- "Let's dive in"
- "Let's embark on"
- "In today's fast-paced world"
- "It's important to note that"
- "It's worth mentioning that"
- "Without further ado"
- "Now more than ever"
- "Take your skills to the next level"
- "Unlock the power of"
- "The possibilities are endless"
- "This is where the magic happens"
- "Let's explore the fascinating world of"
- "Here's the thing"
- "The beauty of this approach"
- "Whether you're a beginner or an expert"
- "This is more than just"
- "At its core"
- "Ultimately"
- "In conclusion"
- "Congratulations on completing"

Don't announce what a section covers when the heading already says it. Under "Run the workflow," start with the first instruction, not "In this section, we'll walk through running your first workflow."

## Sentences

- Mix short and medium-length sentences, and don't repeat one pattern. Don't force every explanation into three parallel ideas.
- Avoid a run of "This allows you to...," "This enables you to...," "This ensures that...," and "This helps you..." sentences. State the result directly: "Open the run to see which steps passed and which failed."
- Don't open an explanation with a rhetorical question such as "So, what exactly is a runner?" Write "A runner is the machine that executes your workflow." A real question is fine when it helps explain a real problem.
- Avoid artificial contrasts such as "It's not just about automation; it's about transforming the way you develop software." State the actual benefit.
- Use contractions (you'll, don't, isn't, can't, you're, it's) where they sound natural. Don't force one into every sentence.

## Punctuation and formatting

- Never use em dashes or en dashes in prose. Use commas, periods, parentheses, or colons. Hyphens in compound words such as "self-hosted" and "browser-based" are fine.
- Use sentence case for headings: "Run your first workflow," not "Run Your First Workflow."
- Don't use exclamation marks to manufacture enthusiasm.
- Don't put emojis in headings, buttons, lesson descriptions, completion messages, or documentation.
- Use bold to help someone find a button, setting, file name, or important instruction. Never bold a whole sentence.
- Save callouts for information that changes what the learner needs to do.

## Page structure

Keep navigation, prerequisites, and completion criteria consistent. Let the explanations follow the topic: some concepts need a paragraph, some a code example, some a comparison table.

- Use numbered lists for procedures, bullet lists for real collections of related items, and tables when the reader needs to compare values.
- Avoid long introductions before the first step, a summary after every section, repeated learning objectives, "key takeaways" sections, decorative headings, lists where a sentence is clearer, tables of obvious information, and several paragraphs that make the same point.
- Lessons don't need the same length or the same headings.

## Be specific

- Use real file names, commands, UI labels, and results. Write "Open the **Actions** tab, select the latest run, and check that the `test` job has a green check mark," not "Monitor the pipeline and verify successful execution."
- When GitHub has a label for something, use that exact label. Don't invent one because it sounds better.
- Explain a concept where the learner first meets it, in a sentence or two, and link to the official documentation for the rest. Don't open a lesson with paragraphs of theory, and don't turn the first lesson into a YAML reference.
- Describe what actually happens, in the lesson where it happens: a run can wait for a runner, a workflow can fail before its first build command, a missing permission can break an otherwise valid workflow, masking a secret isn't a complete security guarantee, features depend on plan, repository visibility, and organization policy, Azure OIDC needs a trust configuration that matches the repository, and migration tools don't always produce a working workflow.
- Base troubleshooting on actual tool behavior, validated examples, or official documentation. Don't invent failure cases.
- Connect the next exercise to what the learner just did: "The workflow builds the app when you run it manually. Next, you'll add another workflow that runs tests when someone opens a pull request." Leave the transition out when it isn't needed.

## Messages

- Completion messages say what the learner did, briefly and factually: "The build passed, and GitHub saved the published application as an artifact."
- Error messages say what happened, why it happened if that's known, and what to do next: "The lesson couldn't be loaded. Refresh the page. If the problem continues, use the lesson list to open it again." Don't guess at a cause.
- Buttons and links name exactly what happens or where they go, such as **Start workshop**.

## Before you finish

Review every page you changed as if you were a developer seeing it for the first time, using [the editorial checklist](editorial-checklist.md). Two questions matter most: would you send these instructions to a colleague, and could that colleague finish the task without asking what you meant? Don't trade technical accuracy for a conversational tone.

The tests in `tests/WorkshopDashboard.Tests` check mechanical rules such as dashes, emojis, code block languages, links, and pinned actions. Passing them, a spell checker, or an AI-detection tool doesn't make the writing good. Read the actual prose.

## Working in this repository

### Layout

- `src/WorkshopDashboard`: the Razor Pages site that renders the lessons.
- `src/WorkshopCore`: the `Calculator` class that learners test in Lab 2. The dashboard uses it to total lesson durations.
- `tests/WorkshopCore.Tests`: the tests learners run. `tests/WorkshopCore.IntegrationTests`: the PostgreSQL tests Lab 17 runs against a service container, skipped when `WORKSHOP_POSTGRES` isn't set. `tests/WorkshopDashboard.Tests`: renderer security, page, and content tests.
- `lessons/foundation`: Labs 0 to 2, the foundation labs that every learner does first, in order. `lessons/explore`: Labs 3 and later, which learners do in any order after the foundation. The README and the dashboard show the two groups separately, each sorted by `order`.
- `.github/workflows/build.yml`: the Lab 1 workflow.
- `solutions/`: the finished workflows for the labs. They're outside `.github/workflows` so they don't run in learners' copies until someone copies one there. Lessons show them in `source=` blocks, so the content tests fail when a solution and its lesson drift apart.
- `exercises/`: starter files that a lab has learners copy into their repository, such as the broken workflow in Lab 13 and the coding standards and sample class in Lab 18. Like `solutions/`, nothing in it runs until a learner copies it.
- `scripts/azure`: the Azure CLI scripts that create the web app and the managed identity for Labs 7 and 8. Their README says who runs which one.
- `migration/azure-devops`: the Azure Pipelines file that Lab 12 migrates, and the workflows learners copy from it. Nothing in it runs. `solutions/migration/migrated-ci.yml` has to stay an exact copy of `expected-workflow.yml`, and the content tests check that.
- `docs/`: documents for learners and instructors: what each lab needs (`prerequisites.md`), `troubleshooting.md`, and `instructor-guide.md`.
- `maintainers/`: files only maintainers need: this file, the editorial guide and checklist, how the site works (`architecture.md`), deploying the site (`deployment.md`), and the scripts that set up and deploy the site (`scripts/`). Keep maintainer-only files here, not in `docs/` or at the root.

### Build and test

You need the .NET 10 SDK. `global.json` accepts any .NET 10 SDK from 10.0.100 up.

```bash
dotnet build
dotnet test
dotnet run --project src/WorkshopDashboard
```

The site runs at `http://localhost:5080`. In the Development environment, lesson changes show up when you refresh the page.

### Workflows

- Every workflow in `.github/workflows/` shows up in every learner's copy, so that folder holds only `build.yml`, which Lab 1 uses. It runs only on `workflow_dispatch`, so nothing runs until a learner starts it. Don't add maintainer workflows: the site is deployed by hand with `maintainers/scripts/deploy-site.sh`. In a learner's copy, the workflows that the labs add, such as `tests.yml` with its `pull_request` trigger, are supposed to be there.
- Keep lab reference workflows and starter files outside `.github/workflows/`, in `solutions/` and `exercises/`, so they don't run in learners' copies. Lessons tell learners when to copy one in.
- Pin every action to a full 40-character commit SHA, with the version in a comment: `uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1`. Resolve the SHA from the action's release tag yourself, and use the same SHA everywhere the action appears, including lessons. The tests fail when two files pin an action differently.
- Set `permissions` explicitly, and grant only what each job needs.
- Set `timeout-minutes` on every job you write, so a stuck job can't use up minutes. `build.yml` uses 10. A job that calls a reusable workflow can't take `timeout-minutes`, so set it on the jobs in the reusable workflow instead. Files that a tool generates, such as gh-aw `.lock.yml` files, are exempt.
- Never put a `${{ }}` expression with a value someone else controls (inputs, issue or pull request titles, branch names) directly in a `run` script. Pass it through `env` and quote the shell variable.
- Run `actionlint` on every workflow file you change. actionlint 1.7.12 doesn't know the `copilot-requests` permission yet ([rhysd/actionlint#686](https://github.com/rhysd/actionlint/issues/686)), so add `-ignore 'unknown permission scope "copilot-requests"'` for files that use it. For gh-aw `.lock.yml` files, also add `-ignore 'unexpected key "queue" for "concurrency"'`, because actionlint doesn't know `concurrency.queue` yet either. For `solutions/self-hosted-runners/self-hosted.yml`, add `-ignore 'label "workshop-lab9" is unknown'`, because actionlint only knows the custom labels listed in an `actionlint.yaml`, and this repository doesn't have one.
- Don't run `gh aw` commands in this repository, not even `gh aw compile --no-emit`. gh-aw writes `.gitattributes` and `.github/aw/actions-lock.json` into the repository it runs in, and Lab 11 has learners create and commit those files in their copies. Compile in a copy of the template, as [the Workflows section of CONTRIBUTING.md](../CONTRIBUTING.md#workflows) describes.

### Lessons

- Each lesson is one Markdown file in `lessons/foundation` (Labs 0 to 2) or `lessons/explore` (the rest). The folder decides the group: the dashboard and the README list foundation labs as required, in order, and explore labs as any order. Every explore lab's "Before you start" begins with the same line asking learners to finish the foundation labs first. Don't call an individual explore lab required or optional. The file name sets the address: `01-build-application.md` becomes `/lessons/build-application`. A numeric prefix only sorts the files.
- The front matter has `title`, `summary`, `duration` (minutes), `difficulty` (`Beginner`, `Intermediate`, or `Advanced`), `order`, and an optional `requires`. Titles start with the lab number, as in `"Lab 3: Compare runners"`, and `order` is that number. Wrap `title`, `summary`, and `requires` in double quotes. The [editorial guide](editorial-guide.md#lesson-files) shows the format. The site skips a lesson whose front matter is invalid and logs why.
- Start the body with an H1 that matches `title`, so the file reads well on GitHub. The dashboard shows the title from the front matter and drops that H1.
- Hints go in a `<details>` block. Put `<details>`, a one-line `<summary>Plain text</summary>`, and `</details>` on lines of their own, with a blank line after the summary and before `</details>`. Those are the only HTML tags the site renders. It shows any other HTML as plain text.
- Use GitHub alerts such as `> [!NOTE]` and `> [!WARNING]` only for information that changes what the learner does.
- Give every fenced code block a language. When a block shows a whole file from this repository, add its path after the language, like `yaml source=.github/workflows/build.yml`. For part of a file, use `excerpt=` instead. The tests fail when a snippet no longer matches its file.
- Link to other lessons and repository files with relative paths, so the links work on GitHub and in the dashboard. A link to the root `README.md` opens the lesson list on the dashboard. Link text must say where the link goes. Never use "click here" or "this link."
- Don't add images or screenshots. GitHub's interface changes often, so screenshots go out of date, and the site's Content Security Policy (`img-src 'self'`) blocks images from other sites, including files linked on GitHub.
- When you add a lab, or change its time or what it needs, update its row in each list of labs: `README.md`, `docs/prerequisites.md`, `solutions/README.md`, and `docs/troubleshooting.md`. Keep them consistent with the lesson's front matter and its "Before you start" section.

### Dashboard

- Don't add inline `<script>` blocks, `style` attributes, or event handler attributes. The Content Security Policy blocks them. Put scripts in `wwwroot/js` and styles in `wwwroot/css/site.css`.
- Keep dependencies to a minimum. Markdig is the site's only NuGet package. highlight.js is vendored in `wwwroot/lib/highlight` and isn't loaded from a CDN.
- The footer and `/health` show the version, commit, and build time. A GitHub Actions build sets them from the run's environment variables. Local builds use fallbacks.

### Claims

- Don't describe a workflow, integration, or UI behavior as working unless someone ran it. When something couldn't be tested, say exactly what and why in the pull request.
- Check technical details against the official GitHub and Microsoft documentation, not memory.
