# How the workshop site works

The workshop site is one ASP.NET Core Razor Pages app on .NET 10. It has no database, no client-side framework, and no accounts. The lessons are Markdown files in `lessons/`, so the same files read well on GitHub and on the site. The site needs no Azure configuration and no secrets to run.

## Projects

| Project | What it contains |
| --- | --- |
| `src/WorkshopDashboard` | The Razor Pages site. |
| `src/WorkshopCore` | The `Calculator` class that learners test in Lab 2. The site uses it to total lesson durations. |
| `tests/WorkshopCore.Tests` | The xUnit tests that learners run in Lab 2. |
| `tests/WorkshopDashboard.Tests` | Tests for the site, plus the content checks for lessons, docs, and workflows. |

## From Markdown file to page

1. The build copies every `lessons/**/*.md` file next to the app, into a `lessons` folder in the build and publish output. The `None` item in `WorkshopDashboard.csproj` does this.
2. `LessonLoader` reads each file under `lessons/<track>/`, parses its front matter, and turns the file name into the lesson's address: `01-build-application.md` becomes `/lessons/build-application`. A file with invalid front matter is skipped and logged, so one bad lesson can't take the site down.
3. `MarkdownRenderer` renders the Markdown with Markdig: pipe tables, GitHub-style heading IDs, and GitHub alerts such as `> [!NOTE]`. It doesn't pass raw HTML through, except the `<details>` and `<summary>` lines that hints use. It also removes the H1, because the page layout shows the title, and sets the "Check your result" section apart.
4. `LinkResolver` rewrites relative links. A link to another lesson opens that lesson on the site, a link to the root `README.md` opens the lesson list, and a link to any other file opens it on GitHub.
5. `LessonCatalog` keeps the rendered lessons in memory. In the Development environment, it rereads the files on every request, so edits show up when you refresh the page.

The pages are `/` (the lesson list), `/lessons/{slug}`, and `/health`. Errors and unknown addresses show friendly pages from `/Error` and `/status/{code}`.

## In the browser

`wwwroot/js/site.js` adds the theme picker, code highlighting, copy buttons, and lesson progress. Highlighting uses highlight.js 11.12.0, stored in `wwwroot/lib/highlight` with its PowerShell language file. Progress is saved in the browser's local storage. It isn't shared between browsers and doesn't check that a workflow passed. The site works without JavaScript, and these features stay hidden until the script runs. `wwwroot/js/theme-init.js` loads first and applies the saved theme before the page is drawn.

## Build information

When the app is built in GitHub Actions, the build records the commit (`GITHUB_SHA`), the build time, and the repository's address in the assembly. A local build falls back to the commit the .NET SDK adds to the version when it builds inside a Git repository, and shows "local build" for the build time.

The footer shows the version, the commit, the build time, and the environment name. `/health` returns the same values as JSON, plus the number of lessons. It returns status 200 when at least one lesson loaded and 503 when none did, and it's never cached.

## Settings

The settings live in the `Workshop` section of `appsettings.json`. To set one with an environment variable, use two underscores, such as `Workshop__EnvironmentName`.

| Setting | When it's empty | What it does |
| --- | --- | --- |
| `LessonsPath` | The `lessons` folder next to the app. Development uses `../../lessons`. | The folder to read lessons from. |
| `EnvironmentName` | The ASP.NET Core environment name, such as `Production`. | The name shown in the footer and on `/health`. |
| `RepositoryUrl` | The repository recorded at build time. | Where links to files other than lessons point, and the footer's source link. |

## Security

- The Content Security Policy allows scripts, styles, images, and fonts only from the site itself, and no inline scripts or styles. That's why every script and style sheet is a file under `wwwroot`, and why the renderer removes the column alignment that Markdig writes into tables as `style` attributes.
- Every response also sets `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, and `Cross-Origin-Opener-Policy`. Outside Development, the app adds HSTS and hides exception details.
- Lessons can't add scripts or other raw HTML. The renderer escapes or drops it, so it shows up as text at most.

## Tests

`dotnet test` runs both test projects.

- `WorkshopCore.Tests` tests the `Calculator` class.
- `WorkshopDashboard.Tests` tests front matter parsing, lesson loading, link rewriting, rendering and HTML safety, build information, and the pages themselves through `WebApplicationFactory`, including status codes and security headers.
- `ContentTests` runs the rules in `tests/WorkshopDashboard.Tests/Content` against the repository: punctuation and emojis, code block languages and the `source=` and `excerpt=` blocks that must match their files, links, HTML, alerts, bold text, lesson front matter and titles, and pinned actions. The [editorial guide](editorial-guide.md#what-the-tests-check) lists every check.

To run only the content checks:

```bash
dotnet test tests/WorkshopDashboard.Tests --filter "FullyQualifiedName~ContentTests"
```

## Deployment

The **Deploy workshop site** workflow publishes the site to Azure App Service. [Deploy the workshop site](deployment.md) covers the setup, the permissions it needs, and the cleanup.
