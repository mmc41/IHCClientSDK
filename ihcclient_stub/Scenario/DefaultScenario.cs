using Ihc.Vis;
using Ihc.Vis.Projects;

namespace Ihc.Stubbing
{
    /// <summary>
    /// The one coherent controller a new stub starts as: ready, with a ready SD card and a valid project the SDK
    /// itself built, so a download parses with no outside file.
    /// </summary>
    /// <remarks>
    /// The project is built through the SDK's public determinism seam with a clock the stub owns, fixed at the
    /// shape defaults' instant, so its creation and save stamps are the same in every process. The settings-only
    /// constructors would read the system clock.
    /// </remarks>
    internal static class DefaultScenario
    {
        private const string ProjectFileName = "stub.vis";

        private static readonly ProjectDetails Details = new("Stub Programmer", "Stub Installer", "DK")
        {
            ProjectNumber = "stub-1",
            CustomerName = "Stub Customer",
        };

        private static readonly Lazy<ProjectFile> BuiltProject = new(BuildProject);

        /// <summary>The project a new stub holds.</summary>
        internal static ProjectFile Project => BuiltProject.Value;

        /// <summary>A description of <see cref="Project"/>: the version a new project carries, and its details.</summary>
        internal static ProjectInfo ProjectInfo { get; } = new()
        {
            VisualMajorVersion = 4,
            VisualMinorVersion = 0,
            ProjectNumber = Details.ProjectNumber,
            CustomerName = Details.CustomerName,
            InstallerName = Details.InstallerName,
            Lastmodified = ShapeDefaults.Instant,
        };

        private static ProjectFile BuildProject()
        {
            // Off any caller's synchronization context: the build runs once, from whichever thread first asks.
            byte[] saved = Task.Run(SaveNewProject).GetAwaiter().GetResult();
            return new ProjectFile(ProjectFileName, ProjectFile.Encoding.GetString(saved));
        }

        private static async Task<byte[]> SaveNewProject()
        {
            var projects = new ProjectAppService(new IhcSettings(), new FixedClock(ShapeDefaults.Instant));
            using var bytes = new MemoryStream();
            await projects.Save(projects.CreateNew(Details), bytes).ConfigureAwait(false);
            return bytes.ToArray();
        }

        /// <summary>A clock stopped at one instant, in UTC, so a stamp taken from local time is the same on every
        /// machine.</summary>
        private sealed class FixedClock(DateTimeOffset instant) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => instant;

            public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }
    }
}
