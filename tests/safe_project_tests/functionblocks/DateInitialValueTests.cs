using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Alignment F-41 (IHCReverseEnginneredInfo tmp/align-campaign-2026-08-10.md): a <c>resource_date</c>'s
    /// initial value is a DAY and a MONTH.
    ///
    /// <para>Measured 2026-08-11: the reference application's <c>Rediger Dato egenskaber</c> shows a picker
    /// reading <c>01 January</c> — day and month, no year — and its tree row renders <c>Dato = 01:01</c>
    /// (<c>dd:MM</c>). The DTD stores <c>year</c>, <c>month</c> and <c>day</c>, all <c>#REQUIRED</c>, with the
    /// template default <c>year="2000" month="1" day="1"</c>.</para>
    ///
    /// <para><b>The year is stored but never edited</b>, so a write must leave it exactly as it found it — a
    /// date edit that silently rewrote the year would change a byte of the project the installer never touched.</para>
    /// </summary>
    public class DateInitialValueTests
    {
        private static IhcSettings Settings => TestSetup.Settings;
        private static ProjectAppService App => new(Settings);

        private static async Task<(ProjectDocumentSession session, ElementId variable)> WithDate()
        {
            Project project = await App.Load("testdata/projects/project3-KompleksWired.vis");
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId block = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("locked") != "yes").Id!.Value;
            session.Apply(new AddVariable(block, "settings", "resource_date", "Mærkedag"));
            ElementId variable = session.Current!.FindById(block)!.Descendants()
                .First(e => e.Tag == "resource_date" && e.GetAttribute("name") == "Mærkedag").Id!.Value;
            return (session, variable);
        }

        [Test]
        public async Task Date_WritesDayAndMonth()
        {
            (ProjectDocumentSession session, ElementId variable) = await WithDate();

            session.Apply(new SetResourceInitialValue(variable, ResourceInitialValue.OfDate(24, 12)));

            ProjectElement written = session.Current!.FindById(variable)!;
            Assert.Multiple(() =>
            {
                Assert.That(written.GetAttribute("day"), Is.EqualTo("24"));
                Assert.That(written.GetAttribute("month"), Is.EqualTo("12"));
            });
        }

        /// <summary>The year is not the installer's to change through this editor, so it must survive untouched.</summary>
        [Test]
        public async Task Date_LeavesTheYearAlone()
        {
            (ProjectDocumentSession session, ElementId variable) = await WithDate();
            string? before = session.Current!.FindById(variable)!.GetAttribute("year");

            session.Apply(new SetResourceInitialValue(variable, ResourceInitialValue.OfDate(24, 12)));

            Assert.That(session.Current!.FindById(variable)!.GetAttribute("year"), Is.EqualTo(before),
                "the dialog never offers the year, so an edit must not rewrite it");
        }

        /// <summary>A date carries no time fields — the flat payload must not leak one kind's data into another.</summary>
        [Test]
        public async Task Date_WritesNoTimeFields()
        {
            (ProjectDocumentSession session, ElementId variable) = await WithDate();

            session.Apply(new SetResourceInitialValue(variable, ResourceInitialValue.OfDate(1, 6)));

            ProjectElement written = session.Current!.FindById(variable)!;
            Assert.Multiple(() =>
            {
                Assert.That(written.GetAttribute("hour"), Is.Null);
                Assert.That(written.GetAttribute("inivalue"), Is.Null);
            });
        }
    }
}
