# Editorial checklist

Use this list when you review a change to anything a person reads: lessons, documentation, dashboard text, or code comments that learners see. The [editorial guide](editorial-guide.md) explains each rule. To track a review in a pull request, copy the list into the description, where you can check the boxes.

Review in a separate pass after the content is written. Read each page as a developer seeing it for the first time, and rewrite the parts that fail. Passing a spelling or grammar check doesn't make the writing good, and neither does passing an AI-detection tool.

## First read

- [ ] The first paragraph helps the reader act or understand the task.
- [ ] Every sentence is necessary, and a developer would actually say it.
- [ ] Technical terms are explained where the reader first needs them.
- [ ] The page doesn't repeat itself, and no paragraph could be replaced by one clear sentence.
- [ ] The instructions are specific enough to follow without guessing.
- [ ] Every heading is useful.
- [ ] Nothing was added just to make the page longer.

## Voice

- [ ] There's no generic introduction or conclusion, and no motivational paragraph at the end.
- [ ] There's no marketing language. Words from the guide's list appear only where they're the correct technical term.
- [ ] There's no manufactured enthusiasm: no exclamation marks for excitement, no compliments to the learner, and no praise for ordinary actions.
- [ ] Sentence openings vary, with no run of "This allows you to..." or "This ensures that..." sentences.
- [ ] Rhetorical questions and transitions are there because they help, not as filler.
- [ ] Obvious actions aren't explained, and there are no disclaimers that don't change what the learner does.
- [ ] Lists, tables, bold text, and alerts are used only where they help.
- [ ] Nothing claims a first-person experience that didn't happen.

## Rhythm

- [ ] Sentence lengths vary, and paragraphs don't all have the same number of sentences.
- [ ] Each step gets the explanation it needs. Some steps need a sentence, and others need more.
- [ ] The lesson is as long as its topic needs, not as long as the other lessons.

## Accuracy

- [ ] Every UI label matches what GitHub, Azure, or the dashboard shows today.
- [ ] Every command was run, and every snippet matches the file it comes from.
- [ ] Every link goes where its text says it goes.
- [ ] Technical claims were checked against the implementation or the official GitHub and Microsoft documentation.
- [ ] Prerequisites appear near the top of the lesson and in its `requires` field.
- [ ] "Check your result" describes something the learner can see.
- [ ] Each troubleshooting item names the error or symptom it addresses and is based on real behavior.
- [ ] Anything that couldn't be tested is named in the pull request description, with the reason, rather than presented as working.

## Dashboard text

- [ ] Buttons and links say what happens or where they go.
- [ ] Completion messages say what the learner did.
- [ ] Error messages say what happened, why if that's known, and what to do next.
- [ ] The interface doesn't suggest it does more than it does.

## Workflows and code

- [ ] Workflows in `.github/workflows/` use only `workflow_dispatch`, and a maintainer-only workflow checks `github.repository` first.
- [ ] Every action is pinned to a full commit SHA, with the version in a comment.
- [ ] `permissions` grants only what each job needs.
- [ ] Values that someone else controls reach `run` scripts through `env`, not through `${{ }}` expressions.
- [ ] `actionlint` reports no problems.
- [ ] Comments that learners read explain why a line is there when the code doesn't make that obvious.

## Before you approve

- [ ] `dotnet test` passes, including the content tests.
- [ ] You'd send these instructions to a colleague.
- [ ] That colleague could finish the task without asking what you meant.

The last two questions both matter. If you wouldn't send the page, rewrite it. If your colleague would have to ask what you meant, add the missing details. Don't trade technical accuracy for a conversational tone.
