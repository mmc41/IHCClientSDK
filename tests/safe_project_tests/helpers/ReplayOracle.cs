using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The shared load→edit→restamp→save→compare pipeline of the vendor-oracle replay fixtures: loads the
    /// original, reproduces the vendor's one-time load-time enum re-hoist (<see cref="ProjectEditor.NormalizeCatalogEnums"/>
    /// — "Action 0"), applies the test's recorded gesture sequence, restamps to the oracle's clock and asserts
    /// byte-identity against the oracle. Each replay test keeps only its gestures, clock and oracle names; the
    /// save/restamp mechanics live here once.
    /// </summary>
    internal static class ReplayOracle
    {
        // Parsed projects cached per file name — Project is immutable and Edit() opens a fresh session over it,
        // so every test replays over the same parsed instance safely (the 236 KB project3 alone is loaded by
        // dozens of tests; one parse serves them all).
        private static readonly ConcurrentDictionary<string, Task<Project>> Projects =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The parsed project for <c>testdata/projects/&lt;name&gt;</c>, cached across the suite.</summary>
        public static Task<Project> LoadProject(string name) =>
            Projects.GetOrAdd(name, n => new ProjectAppService(TestSetup.Settings).Load(TestData.PathOf("projects", n)));

        /// <summary>
        /// Replays <paramref name="replay"/> (after Action 0) over <paramref name="originalName"/> and asserts the
        /// restamped save is byte-identical to <paramref name="oracleName"/>. <paramref name="restampClock"/> is the
        /// oracle's capture moment — including the id2 seconds that the minute-precision <c>modified</c> element
        /// does not carry.
        /// </summary>
        public static async Task AssertReplaysByteIdentical(string originalName, string oracleName,
            DateTimeOffset restampClock, Action<ProjectEditor> replay)
        {
            byte[] expected = TestData.ReadBytes("projects/" + oracleName);
            Project original = await LoadProject(originalName);

            ProjectEditor editor = original.Edit();
            editor.NormalizeCatalogEnums();
            replay(editor);

            Project stamped = MetadataStamper.Restamp(editor.ToProject(), restampClock);
            using var ms = new MemoryStream();
            await new ProjectAppService(TestSetup.Settings).Save(stamped, ms, ProjectSaveOptions.PreserveExistingMetadata);

            TestData.AssertBytesIdentical(expected, ms.ToArray(), "replay → " + oracleName);
        }
    }
}
