using System.Reflection;

namespace ihc_openvisual.Configuration;

/// <summary>
/// Version of the running application, read from the entry-assembly metadata set in the csproj
/// (<c>&lt;Version&gt;</c>/<c>&lt;FileVersion&gt;</c>). The SDK version is a separate concept read from
/// <see cref="Ihc.VersionInfo"/>; the About dialog shows both.
/// </summary>
public static class VersionInfo
{
    public static string GetAppVersionStr()
    {
        Assembly? assembly = Assembly.GetEntryAssembly();
        string? fileVersion = assembly?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        return fileVersion ?? assembly?.GetName().Version?.ToString() ?? "Unknown";
    }
}
