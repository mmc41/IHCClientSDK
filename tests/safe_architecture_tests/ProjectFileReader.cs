using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Ihc.Tests
{
    /// <summary>
    /// What a project file declares about its edges: the projects and packages it references, and the source
    /// files it links in from elsewhere. Pure functions over an <see cref="XDocument"/>, so a rule built on them can
    /// be fed a seeded document. For a rule no real project violates, a seed is the only way to show it can fail.
    /// </summary>
    /// <remarks>
    /// Paths are returned as written, with separators normalised to <c>/</c>, so a rule compares them the same way
    /// on every platform. Nothing here resolves a path against the disk.
    /// </remarks>
    internal static class ProjectFileReader
    {
        /// <summary>Every <c>ProjectReference</c> include, as written.</summary>
        internal static IReadOnlyList<string> ProjectReferences(XDocument project) => Includes(project, "ProjectReference");

        /// <summary>Every <c>PackageReference</c> id.</summary>
        internal static IReadOnlyList<string> PackageReferences(XDocument project) => Includes(project, "PackageReference");

        /// <summary>True when <paramref name="project"/> builds a program, console or windowed, rather than a library.</summary>
        internal static bool IsExecutable(XDocument project) =>
            project.Descendants()
                .Where(element => element.Name.LocalName == "OutputType")
                .Select(element => element.Value.Trim())
                .Any(type => type.Equals("Exe", StringComparison.OrdinalIgnoreCase)
                             || type.Equals("WinExe", StringComparison.OrdinalIgnoreCase));

        /// <summary>True when <paramref name="project"/> sets the property <paramref name="propertyName"/> to <c>true</c>.</summary>
        internal static bool SetsTrue(XDocument project, string propertyName) =>
            project.Descendants()
                .Where(element => element.Name.LocalName == propertyName && element.Parent?.Name.LocalName == "PropertyGroup")
                .Any(element => element.Value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));

        /// <summary>Every <c>Compile Include</c>, which for a test project is how a shared helper is linked in.</summary>
        internal static IReadOnlyList<string> CompileIncludes(XDocument project) => Includes(project, "Compile");

        /// <summary>
        /// True when <paramref name="project"/> compiles the file <paramref name="fileName"/> from the shared test
        /// folder, which is where every linked helper lives: a same-named file of the suite's own does not count,
        /// because it is a copy, and a copy is how two suites drift.
        /// </summary>
        internal static bool LinksShared(XDocument project, string fileName) =>
            CompileIncludes(project).Any(include => IsShared(include, fileName));

        /// <summary>The include under which <paramref name="project"/> links the shared <paramref name="fileName"/>,
        /// or null when it does not.</summary>
        internal static string? SharedInclude(XDocument project, string fileName) =>
            CompileIncludes(project).FirstOrDefault(include => IsShared(include, fileName));

        private static bool IsShared(string include, string fileName) =>
            include.EndsWith("/shared/" + fileName, StringComparison.OrdinalIgnoreCase);

        private static IReadOnlyList<string> Includes(XDocument project, string itemName) =>
            [.. project.Descendants()
                .Where(element => element.Name.LocalName == itemName)
                .Select(element => (string?)element.Attribute("Include"))
                .OfType<string>()
                .Select(include => include.Replace('\\', '/'))];

        /// <summary>Loads <paramref name="projectPath"/> for the functions above.</summary>
        internal static XDocument Load(string projectPath) => XDocument.Load(projectPath);

        /// <summary>The full path an include names, read relative to the project file that wrote it.</summary>
        internal static string Resolve(string projectPath, string include) =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, include));
    }
}
