# Editorial guide

This guide covers everything a person reads in this repository: lessons, the README and other documentation, text in the dashboard, and code comments that learners see. It also applies to anything Copilot or another agent adds here. [AGENTS.md](AGENTS.md) has a condensed version of these rules, and maintainers review changes with [the editorial checklist](editorial-checklist.md).

## Who you're writing for

The workshop is self-paced, so nobody is around to explain a vague step. Learners may be experienced programmers who haven't used GitHub's web interface, CI/CD, or C# before.

Write like a senior developer helping a colleague who's sitting next to them. The voice is conversational, practical, technically accurate, and restrained. You know the subject, you respect the reader's time, and you don't need to keep reminding them how exciting the technology is.

## Write the sentence you'd say

Write the sentence you'd actually say to another developer. Don't make it longer or more formal to sound professional, and don't add enthusiasm to make an ordinary instruction sound interesting. Don't explain what the previous sentence already made clear. Most of all, don't write sentences just to fill out a section.

Instead of:

> Welcome to an exciting hands-on learning experience where you'll unlock the full potential of GitHub Actions and embark on your CI/CD automation journey.

Write:

> You're going to build this website using GitHub Actions. You won't need to install anything.

Instead of:

> In this comprehensive module, we will delve into the fundamental concepts that power modern workflow automation.

Write:

> Before running a workflow, it helps to know what GitHub is going to execute.

Instead of:

> Congratulations! You've successfully completed an important milestone in your GitHub Actions learning journey!

Write:

> Your workflow passed. Open the run to see what happened.

Instead of:

> By leveraging the capabilities of GitHub-hosted runners, developers can seamlessly execute automated processes across diverse operating environments.

Write:

> GitHub runs the job on a temporary machine. You don't have to set one up.

Instead of:

> Now that you've gained a solid understanding of workflow fundamentals, let's dive deeper into the exciting world of automated testing.

Write:

> Next, you'll add a workflow that runs your tests whenever someone opens a pull request.

Use the good examples as a reference for tone, sentence length, and how much explanation is enough. Don't reuse these exact sentences across the workshop. Write for each situation.

## Words and phrases to avoid

Marketing language makes ordinary technical work sound grand. Don't use these words unless one is the correct technical term: leverage, unlock, elevate, seamless, robust, comprehensive, delve, landscape, journey, transformative, cutting-edge, powerful, game-changing, revolutionary, harness, empower, streamline, optimize, innovative, next-generation, holistic, pivotal, crucial.

| Instead of | Write |
| --- | --- |
| leverage | use |
| elevate | improve |
| delve into | look at, examine |

Accuracy comes before the word list. "Test harness" and "compiler optimizations" are the right terms for what they describe, so use them when you mean them.

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

Don't announce what a section covers when its heading already says so. Under a heading called "Run the workflow," don't write "In this section, we'll walk through the process of running your first workflow." Start with the first instruction.

## Sentences

Mix short and medium-length sentences. Don't write every sentence with the same pattern, and don't force every explanation into three parallel ideas.

Watch for runs of sentences that start the same way, especially these:

- "This allows you to..."
- "This enables you to..."
- "This ensures that..."
- "This helps you..."

Usually the sentence can state the result directly.

Instead of:

> This allows you to gain visibility into the execution status of your automation workflow.

Write:

> Open the run to see which steps passed and which failed.

Don't open an ordinary explanation with a rhetorical question.

Instead of:

> So, what exactly is a runner? A runner is the machine responsible for executing your workflow.

Write:

> A runner is the machine that executes your workflow.

A real question is fine when it helps explain a problem. Just don't use one as a default transition.

Avoid artificial contrasts such as "It's not just about automation; it's about transforming the way you develop software." State the actual benefit.

Use contractions where they sound natural: you'll, don't, isn't, can't, you're, it's. Don't force one into every sentence.

## Punctuation and formatting

- Never use em dashes (`—`) or en dashes (`–`) in prose. Use a comma, period, parentheses, or colon instead, and write ranges as "15 to 20 minutes." Hyphens in compound words such as "self-hosted" and "browser-based" are fine.
- Use sentence case for headings: "Run your first workflow," not "Run Your First Workflow." Names keep their capitals, as in "Deploy to Azure App Service."
- Don't use exclamation marks to manufacture enthusiasm.
- Don't put emojis in headings, buttons, lesson descriptions, completion messages, or documentation.
- Use bold to help someone find a button, setting, or important instruction, and never bold a whole sentence. Put UI labels in bold, such as **Actions** and **Run workflow**. Put file names, paths, branch names, commands, and YAML keys in code formatting, such as `.github/workflows/tests.yml`, `main`, and `runs-on`.
- Save callouts for information that changes what the learner needs to do. Don't put every other paragraph in a box. [Lesson files](#lesson-files) shows the alert syntax.

## Page structure

Keep navigation, prerequisites, and completion criteria consistent from page to page. Let the explanations follow the topic. Some concepts need a paragraph, some need a code example, and some need a comparison table. Use whichever format makes the content easiest to understand.

Avoid:

- Long introductions before the first step.
- A summary at the end of every section.
- Repeating the learning objectives after the exercise.
- Decorative headings with no useful content.
- Lists where a sentence would be clearer.
- Tables that contain only obvious information.
- Several paragraphs that make the same point.
- "Key takeaways" sections added for consistency.

Use numbered lists for procedures, bullet lists for real collections of related items, and tables when the reader needs to compare values. Don't turn every explanation into a list.

## Be specific

Someone should be able to follow the instructions without guessing.

Instead of:

> Navigate to your workflow settings and configure the appropriate trigger.

Write:

> Open `.github/workflows/tests.yml`. Under `on`, add `pull_request` so GitHub runs the workflow when someone opens or updates a pull request.

Instead of:

> Monitor the pipeline and verify successful execution.

Write:

> Open the **Actions** tab, select the latest run, and check that the `test` job has a green check mark.

Use real file names, commands, UI labels, and results. When GitHub has a label for something, use that exact label. Don't invent one because it sounds better. GitHub's interface changes, so check labels against the live site when you write or review a lesson.

Don't describe a task as easy, simple, or straightforward. It may be new to the reader. Instead of "Simply configure your OIDC credentials," say exactly what to configure and where.

## Explain concepts where they're used

Don't open a lesson with several paragraphs of theory. Introduce a concept when the learner runs into it.

For example, when a workflow contains this line:

```yaml
runs-on: ubuntu-latest
```

explain it in a sentence:

> This tells GitHub to run the job on a GitHub-hosted Ubuntu machine.

When the next step contains this one:

```yaml
uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
```

explain:

> This action copies your repository's files onto the runner so the next steps can use them. The long string after `@` pins the action to one exact commit, and the comment says which release that commit is.

Workflows in this repository pin every action to a commit SHA, so lessons show the pinned form.

Don't turn the first lesson into a reference for every YAML property. Give learners enough to understand what they're doing, then link to the official documentation for the details.

## Describe what actually happens

Write from real behavior, not an idealized demo. Tell learners about the things they'll actually run into, such as these:

- A run can wait in the queue before a runner picks it up.
- A workflow can fail before it reaches the first build command.
- A missing permission can make an otherwise valid workflow fail.
- GitHub redacts secret values in the logs, but it can't catch every way a value might be transformed, so redaction isn't a complete security guarantee.
- Some features depend on the GitHub plan, the repository's visibility, and organization policies.
- Azure OIDC needs a trust configuration that matches the repository.
- Migration tools don't always produce a working workflow.

Mention each detail in the lesson where it matters, not in a general disclaimer at the start of the workshop.

Don't invent troubleshooting cases. Base them on how the tool actually behaves, on examples you've run, or on the official documentation.

## Transitions

Skip generic transitions such as "Now that you've mastered the fundamentals, you're ready to take the next step." Connect the next exercise to what the learner just did:

> The workflow builds the app when you run it manually. Next, you'll add another workflow that runs tests automatically when a pull request is opened.

Sometimes no transition is needed. Don't add one just to make the page feel complete.

## Completion and error messages

These rules apply to messages in lessons and in the dashboard.

A completion message says what the learner did, briefly and factually. Don't praise ordinary actions or exaggerate what they mean.

Instead of:

> Amazing work! You've successfully conquered your first GitHub Actions workflow and taken an exciting step toward becoming an automation expert!

Write:

> The build passed, and GitHub saved the published application as an artifact.

An error message says what happened, why it happened if that's known, and what the learner can do next. Don't invent a cause you don't know.

Instead of:

> Oops! Something went wrong. Don't worry, we've got you covered!

Write:

> The lesson couldn't be loaded. Refresh the page. If the problem continues, use the lesson list to open it again.

## Text in the dashboard

Page titles, headings, lesson cards, buttons, labels, and navigation in `src/WorkshopDashboard` follow the same rules as the lessons.

- Name buttons and links after what they do or where they go, such as **Start workshop**.
- Don't let the interface suggest it does more than it does. The progress checkbox only stores a note in the browser, and the text next to it says so.

## Code comments

Comments that learners read, in workflow files, solutions, scripts, and `src/WorkshopCore`, follow the same rules. Explain why a line is there when the code doesn't make that obvious. Don't narrate every line, and don't repeat what the lesson already explains next to the snippet.

## Don't fake personality or experience

Don't add jokes, personal stories, or casual slang to make the writing sound human. Don't claim you performed an action or saw a result when that didn't happen. Avoid fictional first-person claims such as "When I first used GitHub Actions, I was amazed by..."

A natural technical voice comes from clarity, experience, judgment, and specificity, not from a personality added on top.

## Writing a lesson

A lesson has to give learners enough to finish it without an instructor. Use the parts below as a model, not a rigid template. Leave out a part that a lesson doesn't need, except "Check your result" and "Next step", which every lesson has.

### Title

Use a task-oriented title. "Run tests when someone opens a pull request" says what the learner will do. "Continuous integration fundamentals and automated validation concepts" doesn't. Lesson titles here also start with the lab number, as in "Lab 2: Run tests on pull requests."

### Opening paragraph

Say what the learner is about to do, and keep it short. For example: "Your repository already has a few unit tests. You'll add a workflow that runs them whenever someone opens or updates a pull request."

### Prerequisites

List only real requirements. If a lab needs an Azure subscription, a Copilot entitlement, a CLI, Docker, or a particular GitHub plan, say so near the top, and put a short version in the `requires` field so it shows on the lesson card. Don't spring a prerequisite on learners halfway through the steps.

### Steps

Use numbered steps, and have each one tell the learner exactly what to do. Include the real file names, UI labels, and commands. Keep steps short enough to follow while switching between the browser and the workshop.

### Code examples

Give complete examples that learners can copy, with a language on every code block so it's highlighted. Avoid unexplained placeholders: when a value needs replacing, make that obvious and say what to replace it with.

Explain what the important lines do, especially in Labs 0 to 2. Once learners have seen basic YAML syntax, don't explain it again.

### What happened

When a concept deserves it, add a short section that explains why the workflow behaved the way it did. Don't repeat the numbered steps.

### Check your result

Near the end of the lesson, add a section with the heading `## Check your result` that describes something the learner can see, such as:

- The workflow run shows a green check mark.
- The `test` job fails after you change the calculator method.
- The run summary has an artifact you can download.
- The Azure website shows the new commit SHA.

Use that exact heading, once. The dashboard shows this section in a box, so learners can find it when they want to confirm they're done.

### If something fails

Cover realistic problems, and say which error or symptom each suggestion addresses. Avoid generic advice like "check your configuration."

Write each problem as its own paragraph that starts with the symptom, then gives the fix. Keep the symptom in plain text, not bold:

```markdown excerpt=lessons/foundation/01-build-application.md
If the run stays queued for several minutes, check [GitHub Status](https://www.githubstatus.com) for an incident affecting GitHub Actions.
```

### Next step

End the lesson with a `## Next step` section that links to the next lesson, for Labs 0 and 1, or back to the lesson list. To link the lesson list, link the root `README.md`, such as `[the lesson list](../../README.md)` from a lab in `lessons/explore`: GitHub opens the README, which lists every lab, and the dashboard opens its own lesson list. Don't add a motivational paragraph.

## Lesson files

The dashboard reads lessons from `lessons/foundation` (Labs 0 to 2) and `lessons/explore` (the rest). It shows the foundation labs first, as a required group done in order, then the explore labs, which learners do in any order. Each group is sorted by `order`. The folder decides the group, so don't call an individual lab required or optional in its text. Every explore lab's `## Before you start` section opens with the same paragraph, `FoundationLine` in `tests/WorkshopDashboard.Tests/Content/ContentRules.cs`:

```markdown
Finish the foundation labs, [Lab 0](../foundation/00-create-repository.md) to [Lab 2](../foundation/02-test-pull-request.md), first. They cover the workflow basics this lab builds on.
```

People also read the same files on GitHub, so write them to work in both places. People also read the same files on GitHub, so write them to work in both places.

### File names

Use lowercase letters, numbers, and hyphens. The file name sets the lesson's address: `01-build-application.md` is served at `/lessons/build-application`. The numeric prefix only keeps the files in order on GitHub. The dashboard orders lessons by their `order` field.

### Front matter

Every lesson starts with a front matter block. The top of a lesson looks like this:

```markdown excerpt=lessons/foundation/02-test-pull-request.md
---
title: "Lab 2: Run tests on pull requests"
summary: "Add a workflow that runs the unit tests whenever someone opens or updates a pull request."
duration: 20
difficulty: Beginner
order: 2
---

# Lab 2: Run tests on pull requests
```

| Field | Required | Value |
| --- | --- | --- |
| `title` | Yes | The lab number and a task-oriented title. |
| `summary` | Yes | One sentence for the lesson card. |
| `duration` | Yes | Whole minutes, from 1 to 240. |
| `difficulty` | Yes | `Beginner`, `Intermediate`, or `Advanced`. |
| `order` | Yes | The lab number. It must be a whole number from 0 to 999 that no other lesson uses. |
| `requires` | No | What the learner needs besides a GitHub account. The lesson card shows it as "Requires:" followed by the value. |

Wrap `title`, `summary`, and `requires` in double quotes. An unquoted value breaks when it contains a colon followed by a space, and quoting every time avoids the problem. Inside double quotes, write `\"` for a quotation mark and `\\` for a backslash.

Each field goes on its own line, and the dashboard rejects any field that isn't in the table. When a lesson's front matter is invalid, the dashboard skips that lesson and logs the reason.

### First heading

Start the body with an H1 that matches `title`, so the file has a title when someone reads it on GitHub. The dashboard shows the title from the front matter and drops the H1. Use H2 and H3 headings for the rest of the lesson.

### Code blocks

Give every fenced code block a language, such as `yaml`, `bash`, `csharp`, or `text`.

When a block shows a whole file from this repository, add the file's path after the language on the opening line: `` ```yaml source=.github/workflows/build.yml ``. When it shows part of a file, use `excerpt=` instead. Paths start at the repository root. GitHub only uses the first word for highlighting, so readers don't see the path.

The content tests compare each of these blocks with its file. A `source=` block has to match the whole file. An `excerpt=` block has to match a run of consecutive lines in the file, ignoring indentation that all of its lines share. When someone changes a workflow, the tests fail until the lesson matches again.

### Hints

Put a hint in a `<details>` block, so learners can try the step before they see it:

```markdown
<details>
<summary>Hint: I can't find the Run workflow button</summary>

Select **Build application** in the list of workflows first. The **Run workflow** button only appears for a workflow that has a `workflow_dispatch` trigger.

</details>
```

- Put `<details>`, the `<summary>` line, and `</details>` on lines of their own, with a blank line after the summary and before `</details>`.
- Write the summary as plain text. Markdown such as backticks or bold isn't rendered inside it.
- These are the only HTML tags the dashboard renders. It shows any other HTML as plain text.

### Alerts

Use GitHub's alert syntax for information that changes what the learner does:

```markdown
> [!WARNING]
> Deleting the repository also deletes its workflow runs and artifacts.
```

The dashboard supports the same five types as GitHub: `NOTE`, `TIP`, `IMPORTANT`, `WARNING`, and `CAUTION`. If the learner could safely skip the information, it doesn't need an alert.

### Links

Link to other lessons and repository files with relative paths, such as `[Lab 1](01-build-application.md)` from another lab in `lessons/foundation`. Those links work on GitHub. The dashboard turns a link to a lesson into a link to its lesson page, a link to the root `README.md` into a link to the lesson list, and a link to any other file into a link to that file on GitHub.

Link text has to say where the link goes. Write `see [the workflow syntax reference](URL)`, not `[click here](URL)`. Screen reader users often jump from link to link, and "click here" tells them nothing.

Link to the official GitHub and Microsoft documentation for details the lesson doesn't need to cover.

### Images

Don't add images or screenshots. GitHub's interface changes often, and a screenshot goes out of date without anyone noticing. Describe the UI with its exact labels instead.

## What the tests check

The content tests in `tests/WorkshopDashboard.Tests` catch mechanical problems in the lessons, documentation, and workflows. They read the files Git would commit, so they skip anything `.gitignore` excludes and every dot folder except `.github`. Each problem is reported as `path:line: message`, and the message says how to fix it. The tests look for:

- Em dashes, en dashes, other long dashes, and emojis, including dashes written as HTML entities. Markdown files are checked outside code. Source, workflow, and configuration files are checked everywhere, except the vendored files in `wwwroot/lib`, the license, and the `.lock.yml` workflows that `gh aw compile` generates.
- Indented code blocks, fences that aren't closed, and fences without a language. In lessons, the language has to be one the dashboard recognizes.
- Anything after the language other than `source=` or `excerpt=`, and `source=` or `excerpt=` blocks that no longer match their files.
- Images, and link text that doesn't say where the link goes, such as "click here" or "read more."
- Relative links to files that aren't in the repository, and section links to headings that don't exist in the same file or the linked one. Capitalization has to match, because file paths on GitHub are case-sensitive.
- Links the dashboard blocks, such as `javascript:` links and links that start with `//`.
- HTML other than comments and hints, and hints that don't close in the same list item or quote where they open.
- Alert types GitHub doesn't support, text on the same line as the alert type, and alerts inside a list or another quote, where GitHub shows them as plain quotes.
- Bold text that ends with a period, question mark, exclamation mark, or colon, which means a whole sentence or a run-in heading is in bold. An ellipsis is fine, because some UI labels end with one.
- Lesson front matter the dashboard would reject, two lessons with the same `order`, titles that don't start with "Lab N: " for their `order`, a body that doesn't start with an H1 matching the title, a second H1, and lesson files outside `lessons/foundation` and `lessons/explore`.
- Lessons without exactly one `## Check your result` section and one `## Next step` section. The heading has to match exactly, so `## Check the result` or `### Check your result` doesn't count.
- Explore labs whose `## Before you start` section doesn't open with the foundation paragraph.
- Actions and reusable workflows that aren't pinned to a full commit SHA with the release in a comment, such as `# v7.0.1`, and Docker images without a digest. This covers every YAML file, and the YAML blocks and front matter in Markdown files.
- An action pinned to different commits or release comments in different places. Actions from one repository, such as `github/codeql-action/init` and `github/codeql-action/analyze`, count as one action.
- Copies that no longer match the file they copy. The list is `ExactCopies` in `tests/WorkshopDashboard.Tests/Content/ContentRules.cs`. For example, `solutions/migration/migrated-ci.yml` has to stay an exact copy of `migration/azure-devops/expected-workflow.yml`.

They don't check whether the writing is good, and they deliberately don't reject words from the list above, because some of those words are the right technical term. A spell checker or an AI-detection tool doesn't prove quality either. Read the actual prose, and use [the editorial checklist](editorial-checklist.md) before you finish.
