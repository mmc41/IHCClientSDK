using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Time.Testing;

using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The arrange every session-command fixture opens with: the facade, an oracle loaded through it, and a
    /// document session opened over that project — so a command test reads as the gesture it applies and the
    /// outcome it expects rather than as the plumbing that gets there.
    /// </summary>
    /// <remarks>
    /// A base class rather than a <c>using static</c> helper because two of these names are also namespaces —
    /// <c>Ihc.App</c> and <c>Ihc.Vis.Session</c> — and a fixture sits inside <c>Ihc.Vis.Tests</c>, where an
    /// enclosing namespace wins a simple name over an imported static member (CS0118). An inherited member is
    /// found before either. A fixture needing a different load declares it under its own name
    /// (<c>LoadOracle</c>, <c>LoadFixture</c>) rather than hiding <see cref="Load(string)"/>.
    /// </remarks>
    public abstract class SessionCommandFixture
    {
        /// <summary>
        /// The clock every service in this family is built on. PINNED rather than <see cref="TimeProvider.System"/>,
        /// because a default save re-stamps the root <c>id2</c> and <c>&lt;modified&gt;</c> from it: on the system
        /// clock two saves either side of a second boundary write different bytes, so a byte-fidelity assertion over
        /// them fails at a rate set by how fast the machine is rather than by the code under test. Pinning also
        /// removes the agent's time zone from the stamp, which <c>GetLocalNow</c> would otherwise fold in.
        /// </summary>
        protected static DateTimeOffset SaveClock { get; } = new(2026, 6, 27, 16, 5, 51, TimeSpan.Zero);

        protected static ProjectAppService App =>
            new(TestSetup.Settings, new BuiltInCatalog(), new FakeTimeProvider(SaveClock));

        protected static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        /// <summary>Serializes <paramref name="project"/> through <see cref="App"/> — the family's save-to-bytes.</summary>
        protected static async Task<byte[]> Bytes(Project project)
        {
            using var ms = new MemoryStream();
            await App.Save(project, ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Opens a session on <paramref name="project"/>. The opened snapshot is the session's save point, so a
        /// fixture that measures dirtiness starts clean.
        /// </summary>
        protected static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
        }
    }
}
