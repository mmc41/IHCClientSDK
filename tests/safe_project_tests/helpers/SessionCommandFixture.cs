using System.Threading.Tasks;

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
        protected static ProjectAppService App => new(TestSetup.Settings);

        protected static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

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
