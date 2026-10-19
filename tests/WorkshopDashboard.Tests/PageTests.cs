using System.Net;
using System.Text.Json;
using WorkshopDashboard.Services;
using WorkshopDashboard.Tests.TestSupport;
using Xunit;

namespace WorkshopDashboard.Tests;

public sealed class PageTests : IDisposable
{
    private readonly TempLessons _lessons = new();

    public void Dispose() => _lessons.Dispose();

    private void WriteSampleLessons()
    {
        _lessons.WriteLesson("foundation", "00-welcome.md", "Lab 0: Welcome", 0);
        _lessons.WriteLesson(
            "foundation",
            "01-first-workflow.md",
            "Lab 1: First workflow",
            1,
            "## Run it\n\nSelect **Run workflow**.\n\n## Check your result\n\nThe run has a green check mark.");
        _lessons.Write(
            "explore",
            "03-triggers.md",
            TempLessons.LessonText("Lab 3: Triggers", 3, difficulty: "Intermediate", extraFields: "requires: \"Lab 1\""));
    }

    private static async Task<(HttpResponseMessage Response, string Body)> GetAsync(HttpClient client, string url)
    {
        HttpResponseMessage response = await client.GetAsync(url, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, body);
    }

    [Fact]
    public async Task The_home_page_groups_foundation_labs_before_explore_labs()
    {
        WriteSampleLessons();
        await using var factory = new DashboardFactory(_lessons.Root);

        (HttpResponseMessage response, string body) = await GetAsync(factory.CreateClient(), "/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Learn GitHub Actions by using GitHub Actions", body);
        Assert.Contains("<a class=\"button\" href=\"/lessons/welcome\">Start workshop</a>", body);
        Assert.Contains("<a href=\"/lessons/first-workflow\">Lab 1: First workflow</a>", body);
        Assert.Contains("<a href=\"/lessons/triggers\">Lab 3: Triggers</a>", body);
        Assert.Contains("Requires: Lab 1", body);
        Assert.Contains("<h2 id=\"lessons-title\">Labs</h2>", body);
        Assert.Contains("<h3 id=\"foundation-title\">Foundation labs (required)</h3>", body);
        Assert.Contains("<h3 id=\"explore-title\">Explore labs</h3>", body);
        Assert.Equal(2, body.Split("class=\"lesson-list\"").Length - 1);

        int foundation = body.IndexOf("foundation-title\">", StringComparison.Ordinal);
        int explore = body.IndexOf("explore-title\">", StringComparison.Ordinal);
        int welcome = body.IndexOf("Lab 0: Welcome", foundation, StringComparison.Ordinal);
        int firstWorkflow = body.IndexOf("Lab 1: First workflow", foundation, StringComparison.Ordinal);
        int triggers = body.IndexOf("Lab 3: Triggers", foundation, StringComparison.Ordinal);
        Assert.True(foundation < welcome && welcome < firstWorkflow && firstWorkflow < explore, "The foundation labs should be listed first, in order.");
        Assert.True(explore < triggers, "Lab 3 should be in the explore group.");
        Assert.DoesNotContain("The lessons couldn't be loaded", body);
    }

    [Fact]
    public async Task The_home_page_hides_an_empty_group()
    {
        _lessons.WriteLesson("foundation", "00-welcome.md", "Lab 0: Welcome", 0);
        await using var factory = new DashboardFactory(_lessons.Root);

        (_, string body) = await GetAsync(factory.CreateClient(), "/");

        Assert.Contains("Foundation labs (required)", body);
        Assert.DoesNotContain("Explore labs", body);
    }

    [Fact]
    public async Task Only_foundation_lesson_pages_show_the_required_label()
    {
        WriteSampleLessons();
        await using var factory = new DashboardFactory(_lessons.Root);
        HttpClient client = factory.CreateClient();

        (_, string foundation) = await GetAsync(client, "/lessons/first-workflow");
        (_, string explore) = await GetAsync(client, "/lessons/triggers");

        Assert.Contains("<span class=\"lesson-foundation\">Foundation lab, required</span>", foundation);
        Assert.DoesNotContain("Foundation lab, required", explore);
    }

    [Fact]
    public async Task A_lesson_page_shows_the_lesson_and_links_to_its_neighbors()
    {
        WriteSampleLessons();
        await using var factory = new DashboardFactory(_lessons.Root);

        (HttpResponseMessage response, string body) = await GetAsync(factory.CreateClient(), "/lessons/first-workflow");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1>Lab 1: First workflow</h1>", body);
        Assert.Single(body.Split("<h1").Skip(1));
        Assert.Contains("<h2 id=\"run-it\">Run it</h2>", body);
        Assert.Contains("<strong>Run workflow</strong>", body);
        Assert.Contains("<section class=\"lesson-check\">\n<h2 id=\"check-your-result\">Check your result</h2>\n<p>The run has a green check mark.</p>\n</section>", body);
        Assert.Contains("href=\"/lessons/welcome\" rel=\"prev\"", body);
        Assert.Contains("href=\"/lessons/triggers\" rel=\"next\"", body);
    }

    [Theory]
    [InlineData("/lessons/does-not-exist")]
    [InlineData("/no/such/page")]
    [InlineData("/status/500")]
    [InlineData("/Error")]
    public async Task Unknown_addresses_show_page_not_found(string url)
    {
        WriteSampleLessons();
        await using var factory = new DashboardFactory(_lessons.Root);

        (HttpResponseMessage response, string body) = await GetAsync(factory.CreateClient(), url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("<h1>Page not found</h1>", body);
    }

    [Fact]
    public async Task Lesson_addresses_redirect_to_lowercase()
    {
        WriteSampleLessons();
        await using var factory = new DashboardFactory(_lessons.Root);

        HttpResponseMessage response = await factory.CreateClientWithoutRedirects()
            .GetAsync("/lessons/First-Workflow", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/lessons/first-workflow", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Health_reports_the_build_and_lesson_count()
    {
        WriteSampleLessons();
        await using var factory = new DashboardFactory(_lessons.Root);

        (HttpResponseMessage response, string body) = await GetAsync(factory.CreateClient(), "/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore, "The health response must not be cached.");

        using JsonDocument json = JsonDocument.Parse(body);
        JsonElement root = json.RootElement;
        Assert.Equal("healthy", root.GetProperty("status").GetString());
        Assert.Equal(3, root.GetProperty("lessons").GetInt32());
        Assert.Equal("Production", root.GetProperty("environment").GetString());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("version").GetString()));
        Assert.False(string.IsNullOrEmpty(root.GetProperty("commit").GetString()));
        Assert.False(string.IsNullOrEmpty(root.GetProperty("buildTimestamp").GetString()));
    }

    [Fact]
    public async Task Without_lessons_health_fails_and_the_home_page_explains_why()
    {
        await using var factory = new DashboardFactory(_lessons.Root);
        HttpClient client = factory.CreateClient();

        (HttpResponseMessage health, string healthBody) = await GetAsync(client, "/health");
        (HttpResponseMessage home, string homeBody) = await GetAsync(client, "/");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
        Assert.Contains("\"status\":\"unhealthy\"", healthBody);
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("The lessons couldn't be loaded", homeBody);
        Assert.DoesNotContain("Start workshop", homeBody);
    }

    [Theory]
    [InlineData("/", HttpStatusCode.OK)]
    [InlineData("/lessons/welcome", HttpStatusCode.OK)]
    [InlineData("/css/site.css", HttpStatusCode.OK)]
    [InlineData("/health", HttpStatusCode.OK)]
    [InlineData("/no/such/page", HttpStatusCode.NotFound)]
    public async Task Responses_include_security_headers(string url, HttpStatusCode expectedStatus)
    {
        WriteSampleLessons();
        await using var factory = new DashboardFactory(_lessons.Root);

        (HttpResponseMessage response, _) = await GetAsync(factory.CreateClient(), url);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("strict-origin-when-cross-origin", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal(SecurityHeaders.PermissionsPolicy, Assert.Single(response.Headers.GetValues("Permissions-Policy")));
        Assert.Equal("same-origin", Assert.Single(response.Headers.GetValues("Cross-Origin-Opener-Policy")));
    }

    [Theory]
    [InlineData("/css/site.css", "text/css")]
    [InlineData("/js/site.js", "text/javascript")]
    [InlineData("/js/theme-init.js", "text/javascript")]
    [InlineData("/lib/highlight/highlight.min.js", "text/javascript")]
    [InlineData("/favicon.svg", "image/svg+xml")]
    public async Task Static_files_are_served(string url, string mediaType)
    {
        await using var factory = new DashboardFactory(_lessons.Root);

        (HttpResponseMessage response, string body) = await GetAsync(factory.CreateClient(), url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(body);
    }

    [Fact]
    public async Task Server_errors_hide_the_exception_details()
    {
        WriteSampleLessons();
        await using var factory = new DashboardFactory(_lessons.Root)
        {
            ConfigureServices = services => services.AddSingleton<LessonCatalog>(_ => throw new InvalidOperationException("secret detail")),
        };

        (HttpResponseMessage response, string body) = await GetAsync(factory.CreateClient(), "/");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("<h1>The page couldn't be loaded</h1>", body);
        Assert.Contains("request ID", body);
        Assert.DoesNotContain("secret detail", body);
        Assert.DoesNotContain("InvalidOperationException", body);
    }

    [Fact]
    public async Task Development_shows_lesson_edits_after_a_refresh()
    {
        _lessons.WriteLesson("foundation", "00-welcome.md", "Lab 0: Welcome", 0);
        await using var factory = new DashboardFactory(_lessons.Root, "Development");
        HttpClient client = factory.CreateClient();
        (_, string before) = await GetAsync(client, "/");

        _lessons.WriteLesson("foundation", "01-first-workflow.md", "Lab 1: First workflow", 1);
        (_, string after) = await GetAsync(client, "/");

        Assert.DoesNotContain("/lessons/first-workflow", before);
        Assert.Contains("<a href=\"/lessons/first-workflow\">Lab 1: First workflow</a>", after);
    }

    [Fact]
    public async Task Production_keeps_the_lessons_it_loaded_at_startup()
    {
        _lessons.WriteLesson("foundation", "00-welcome.md", "Lab 0: Welcome", 0);
        await using var factory = new DashboardFactory(_lessons.Root);
        HttpClient client = factory.CreateClient();
        await GetAsync(client, "/");

        _lessons.WriteLesson("foundation", "01-first-workflow.md", "Lab 1: First workflow", 1);
        (_, string after) = await GetAsync(client, "/");

        Assert.DoesNotContain("/lessons/first-workflow", after);
    }
}
