using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Alignment F-41: a <c>resource_weekday</c>'s
    /// initial value is one of seven TOKENS, so the value payload needs a token representation.
    ///
    /// <para>Measured 2026-08-11: the reference application's <c>Rediger Ugedag egenskaber</c> offers a combo of
    /// <c>Mandag, Tirsdag, Onsdag, Torsdag, Fredag, Lørdag, Søndag</c>. The DTD stores the choice as
    /// <c>inivalue (monday | tuesday | wednesday | thursday | friday | saturday | sunday) "monday"</c> — an
    /// enumerated token, not a number and not a bool, which is why none of the existing kinds could carry it and
    /// the type fell through to "no editable initial value".</para>
    ///
    /// <para>The engine keeps the TOKEN; the Danish display names are the application's, so the file never
    /// depends on how a label is spelled.</para>
    /// </summary>
    public class WeekdayInitialValueTests
    {
        private static IhcSettings Settings => TestSetup.Settings;
        private static ProjectAppService App => new(Settings);

        private static async Task<(ProjectDocumentSession session, ElementId variable)> WithWeekday()
        {
            Project project = await App.Load("testdata/projects/project3-KompleksWired.vis");
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId block = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("locked") != "yes").Id!.Value;
            session.Apply(new AddVariable(block, "settings", "resource_weekday", "Dag"));
            ElementId variable = session.Current!.FindById(block)!.Descendants()
                .First(e => e.Tag == "resource_weekday" && e.GetAttribute("name") == "Dag").Id!.Value;
            return (session, variable);
        }

        [TestCase("sunday")]
        [TestCase("friday")]
        public async Task Choice_WritesTheTokenAsInivalue(string token)
        {
            (ProjectDocumentSession session, ElementId variable) = await WithWeekday();

            session.Apply(new SetResourceInitialValue(variable, ResourceInitialValue.OfChoice(token)));

            Assert.That(session.Current!.FindById(variable)!.GetAttribute("inivalue"), Is.EqualTo(token),
                "the DTD's own token is what the file stores");
        }

        /// <summary>The DEFAULT token is omitted, not written — the format's omit-if-default rule, which keeps a
        /// project byte-identical to what the reference application would write. Pinned because it dictates the
        /// READ side: an absent <c>inivalue</c> on a weekday means <c>monday</c>, not "no value", and a reader
        /// that treats missing as empty would show the wrong day.</summary>
        [Test]
        public async Task Choice_OmitsTheDefaultToken()
        {
            (ProjectDocumentSession session, ElementId variable) = await WithWeekday();

            session.Apply(new SetResourceInitialValue(variable, ResourceInitialValue.OfChoice("monday")));

            Assert.That(session.Current!.FindById(variable)!.GetAttribute("inivalue"), Is.Null,
                "monday is the DTD default for resource_weekday, so the attribute is dropped");
        }

        /// <summary>A token kind must not disturb the others: the payload is one flat record and a new kind that
        /// leaked into another one's write path would corrupt unrelated variables.</summary>
        [Test]
        public async Task Choice_WritesNothingElse()
        {
            (ProjectDocumentSession session, ElementId variable) = await WithWeekday();

            session.Apply(new SetResourceInitialValue(variable, ResourceInitialValue.OfChoice("friday")));

            ProjectElement written = session.Current!.FindById(variable)!;
            Assert.Multiple(() =>
            {
                Assert.That(written.GetAttribute("hour"), Is.Null, "a weekday has no time fields");
                Assert.That(written.GetAttribute("millisecond"), Is.Null);
            });
        }
    }
}
