using System.Text.Json.Serialization;

namespace WorkshopDashboard.Services;

public sealed record HealthReport(
    string Status,
    string Version,
    string Commit,
    string BuildTimestamp,
    string Environment,
    int Lessons)
{
    public static HealthReport Create(SiteInfo site, int lessonCount)
    {
        return new HealthReport(
            lessonCount > 0 ? "healthy" : "unhealthy",
            site.Version,
            site.Commit,
            site.BuildTimestamp,
            site.EnvironmentName,
            lessonCount);
    }

    [JsonIgnore]
    public bool IsHealthy => Lessons > 0;
}
