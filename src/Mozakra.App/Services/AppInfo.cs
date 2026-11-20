using System.Reflection;

namespace Mozakra.App.Services;

/// <summary>Build-time facts about the app (from Directory.Build.props).</summary>
public static class AppInfo
{
    private static readonly Assembly Assembly = typeof(AppInfo).Assembly;

    /// <summary>GitHub repository URL (<c>RepositoryUrl</c> MSBuild property).</summary>
    public static string RepositoryUrl { get; } = Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "RepositoryUrl")?.Value ?? "";

    /// <summary>True once the placeholder "OWNER" URL has been replaced with the real repository.</summary>
    public static bool HasRealRepository =>
        Uri.TryCreate(RepositoryUrl, UriKind.Absolute, out _) && !RepositoryUrl.Contains("/OWNER/");

    public static string Version { get; } = Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? Assembly.GetName().Version?.ToString() ?? "0.0.0";
}
