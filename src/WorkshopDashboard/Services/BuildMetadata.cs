using System.Reflection;

namespace WorkshopDashboard.Services;

// Values recorded when the app was compiled. See WorkshopDashboard.csproj for how GitHub Actions supplies them.
public sealed record BuildMetadata(
    string? InformationalVersion,
    string? CommitSha,
    string? BuildTimestamp,
    string? RepositoryUrl)
{
    public static BuildMetadata FromAssembly(Assembly assembly)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (AssemblyMetadataAttribute attribute in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (!string.IsNullOrWhiteSpace(attribute.Value))
            {
                metadata[attribute.Key] = attribute.Value;
            }
        }

        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return new BuildMetadata(
            informationalVersion,
            metadata.GetValueOrDefault("CommitSha"),
            metadata.GetValueOrDefault("BuildTimestamp"),
            metadata.GetValueOrDefault("RepositoryUrl"));
    }
}
