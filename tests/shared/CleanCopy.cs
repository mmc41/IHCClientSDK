using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ihc.Tests
{
    /// <summary>
    /// A copy of an executable's build output without the settings file staged there. An executable reads the
    /// <c>ihcsettings.json</c> beside it, and the one its build stages is the developer's, which names a real
    /// controller and carries its login; a test that starts the executable from a copy writes the file the copy
    /// runs with instead (O8).
    /// </summary>
    internal static class CleanCopy
    {
        /// <summary>
        /// Makes <paramref name="target"/> hold exactly the files of <paramref name="source"/> except its settings
        /// file: copies each file that is missing or differs in length or last-write time, and deletes each file the
        /// source does not have, a settings file left from an earlier run included. A file the last build did not
        /// change is not copied again, so refreshing a kept copy costs what that build changed, not the whole output.
        /// </summary>
        internal static void Mirror(DirectoryInfo source, DirectoryInfo target)
        {
            StringComparer names = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            target.Create();
            var mirrored = new HashSet<string>(names);
            foreach (FileInfo file in source.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source.FullName, file.FullName);
                if (names.Equals(relative, IhcConfiguration.SettingsFileName))
                {
                    continue;
                }
                mirrored.Add(relative);
                var copied = new FileInfo(Path.Combine(target.FullName, relative));
                if (copied.Exists && copied.Length == file.Length && copied.LastWriteTimeUtc == file.LastWriteTimeUtc)
                {
                    continue;
                }
                Directory.CreateDirectory(copied.DirectoryName!);
                file.CopyTo(copied.FullName, overwrite: true);
                // Stamped here rather than trusted to the copy, so an unchanged file is recognised on every platform.
                File.SetLastWriteTimeUtc(copied.FullName, file.LastWriteTimeUtc);
            }
            foreach (FileInfo stale in target.EnumerateFiles("*", SearchOption.AllDirectories)
                         .Where(file => !mirrored.Contains(Path.GetRelativePath(target.FullName, file.FullName)))
                         .ToList())
            {
                stale.Delete();
            }
        }
    }
}
