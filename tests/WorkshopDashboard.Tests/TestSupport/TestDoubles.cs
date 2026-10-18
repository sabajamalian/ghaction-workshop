using Microsoft.Extensions.FileProviders;

namespace WorkshopDashboard.Tests.TestSupport;

// Records log entries so tests can check what the app reported.
internal sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception)));
    }
}

internal sealed class FakeHostEnvironment(string environmentName, string contentRootPath) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;

    public string ApplicationName { get; set; } = "WorkshopDashboard";

    public string ContentRootPath { get; set; } = contentRootPath;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
