using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WorkshopDashboard.Tests.TestSupport;

// Runs the dashboard in memory against a lessons folder chosen by the test.
internal sealed class DashboardFactory(string lessonsPath, string environment = "Production") : WebApplicationFactory<Program>
{
    public const string RepositoryUrl = "https://github.com/octo/workshop";

    public Action<IServiceCollection>? ConfigureServices { get; init; }

    public HttpClient CreateClientWithoutRedirects()
    {
        return CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("Workshop:LessonsPath", lessonsPath);
        builder.UseSetting("Workshop:RepositoryUrl", RepositoryUrl);
        builder.ConfigureLogging(logging => logging.ClearProviders());

        if (ConfigureServices is not null)
        {
            builder.ConfigureTestServices(ConfigureServices);
        }
    }
}
