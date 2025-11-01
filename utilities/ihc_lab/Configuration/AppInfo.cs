using System;
using System.Reflection;

namespace IhcLab
{
    /// <summary>
    /// Get version of the App stored in the project file.
    /// </summary>
    public static class VersionInfo
    {
        public static string GetAppVersionStr()
        {
            Assembly? assembly = Assembly.GetEntryAssembly();
            var fileVersion = assembly?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            return fileVersion ?? assembly?.GetName().Version?.ToString() ?? "Unknown";
        }

    }
}