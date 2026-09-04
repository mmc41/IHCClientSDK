using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Ihc.Tests
{
    /// <summary>
    /// The project-file half of the Windows quarantine: which projects the solution holds, which of them are
    /// named for Windows, and what in a project file binds it to Windows before a line of its code runs.
    ///
    /// <para><b>Why a project file, when the suite reads IL everywhere else.</b> A package reference is a fact
    /// about the <c>.csproj</c>, and for the reference that matters most here it is the ONLY place the fact
    /// exists: <c>Microsoft.Windows.CsWin32</c> is a source generator, so nothing of it survives into an assembly —
    /// the P/Invokes it emits are the consumer's own. The same goes for a <c>-windows</c> target framework or a
    /// <c>UseWPF</c>. An IL scan sees the use; only the file shows the reference.</para>
    ///
    /// <para><b>Why the recogniser is a list.</b> A package id carries no platform metadata a test can read
    /// without restoring the package, so what counts as a Windows package is curated: the ids of the platform
    /// packages that do not say so in their name, and the name segments that do. That makes this half complete
    /// for the reference and <see cref="WindowsReachScan"/> complete for the use — a package the list does not
    /// name is still caught the moment a neutral assembly calls into it, because its members carry the platform
    /// attribute the IL scan reads. Extend the list when a generator or a build-only package arrives that leaves
    /// no such trace.</para>
    /// </summary>
    internal static partial class WindowsProjectFileScan
    {
        /// <summary>One project of the solution: its name, which is the project file's own, and where it is.</summary>
        internal sealed record SolutionProject(string Name, string Path)
        {
            /// <summary>Whether the name licenses the project to reach Windows.</summary>
            public bool IsWindowsProject => WindowsProjectFileScan.IsWindowsProject(Name);
        }

        /// <summary>The licence, read from the name alone: a project whose name says Windows may depend on it.
        /// The project file's name is the MSBuild project name and, by default, the assembly name, so the same
        /// test applies to a loaded assembly.</summary>
        internal static bool IsWindowsProject(string projectName) =>
            projectName.Contains("windows", StringComparison.OrdinalIgnoreCase);

        [GeneratedRegex("""^Project\("\{[^}]+\}"\)\s*=\s*"[^"]+",\s*"([^"]+\.csproj)",""", RegexOptions.Multiline)]
        private static partial Regex ProjectLine();

        /// <summary>Every C# project the solution file lists. Solution folders are not projects and carry no
        /// <c>.csproj</c> path, which is what leaves them out.</summary>
        internal static IReadOnlyList<SolutionProject> Projects(string solutionPath)
        {
            string root = System.IO.Path.GetDirectoryName(solutionPath)!;
            return [.. ProjectLine().Matches(File.ReadAllText(solutionPath))
                .Select(match => match.Groups[1].Value.Replace('\\', System.IO.Path.DirectorySeparatorChar))
                .Select(relative => new SolutionProject(
                    System.IO.Path.GetFileNameWithoutExtension(relative), System.IO.Path.Combine(root, relative)))
                .OrderBy(project => project.Name, StringComparer.Ordinal)];
        }

        /// <summary>Everything in the project file that binds the project to Windows, each named the way it
        /// appears there. Empty for a platform-neutral project.</summary>
        internal static IReadOnlyList<string> WindowsDependencies(string projectPath) =>
            WindowsDependencies(XDocument.Load(projectPath));

        internal static IReadOnlyList<string> WindowsDependencies(XDocument project)
        {
            var found = new List<string>();
            foreach (XElement element in project.Descendants())
            {
                string name = element.Name.LocalName;
                string value = element.Value.Trim();
                switch (name)
                {
                    case "PackageReference":
                        string? id = (string?)element.Attribute("Include") ?? (string?)element.Attribute("Update");
                        if (id is not null && IsWindowsPackage(id))
                        {
                            found.Add($"PackageReference {id}");
                        }
                        break;
                    case "TargetFramework":
                    case "TargetFrameworks":
                        foreach (string framework in value.Split(';',
                                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            if (framework.Contains("-windows", StringComparison.OrdinalIgnoreCase))
                            {
                                found.Add($"{name} {framework}");
                            }
                        }
                        break;
                    case "UseWPF":
                    case "UseWindowsForms":
                        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                        {
                            found.Add($"{name} true");
                        }
                        break;
                    case "RuntimeIdentifier":
                        if (value.StartsWith("win", StringComparison.OrdinalIgnoreCase))
                        {
                            found.Add($"{name} {value}");
                        }
                        break;
                }
            }
            return [.. found.Distinct().Order(StringComparer.Ordinal)];
        }

        // Platform packages whose id does not say so. Each is Windows-only at run time by the platform
        // attribute on its assembly, which is exactly what the IL half reads once one is used.
        private static readonly HashSet<string> WindowsPackageIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "System.Drawing.Common",
            "System.Management",
            "System.ServiceProcess.ServiceController",
            "System.Diagnostics.EventLog",
            "System.Diagnostics.PerformanceCounter",
            "System.Security.Cryptography.ProtectedData",
            "System.Security.Cryptography.Cng",
            "System.Security.AccessControl",
            "System.IO.FileSystem.AccessControl",
            "System.IO.Pipes.AccessControl",
            "System.Threading.AccessControl",
            "System.DirectoryServices",
            "System.DirectoryServices.AccountManagement",
            "System.DirectoryServices.Protocols",
            "Interop.UIAutomationClient",
            "Microsoft.Extensions.Logging.EventLog",
        };

        // A dotted segment that names the platform: Microsoft.Windows.CsWin32, Microsoft.Win32.Registry,
        // System.Security.Principal.Windows, Avalonia.Win32, PInvoke.User32, FlaUI.UIA3. Whole segments rather
        // than substrings, so a package that merely contains the letters is not caught by them.
        private static readonly HashSet<string> WindowsPackageSegments = new(StringComparer.OrdinalIgnoreCase)
        {
            "Windows", "WindowsDesktop", "WindowsAppSDK", "WindowsServices", "WindowsForms", "WindowsBase",
            "WindowsRuntime", "Win32", "WinForms", "WinUI", "WPF", "UWP", "FlaUI", "Vanara", "PInvoke",
        };

        internal static bool IsWindowsPackage(string packageId) =>
            WindowsPackageIds.Contains(packageId) || packageId.Split('.').Any(WindowsPackageSegments.Contains);
    }
}
