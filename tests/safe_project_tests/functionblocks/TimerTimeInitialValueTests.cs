using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Alignment F-41 (IHCReverseEnginneredInfo tmp/align-campaign-2026-08-10.md): a <c>resource_timertime</c>
    /// carries MILLISECONDS, so an initial-value write must set them.
    ///
    /// <para>Measured 2026-08-11 on the reference application's own dialogs, all three time-family types side by
    /// side: <c>Tidspunkt</c> (<c>resource_time</c>) shows <c>00.00.00</c> — hours, minutes, seconds — while both
    /// <c>Timer</c> (<c>resource_timer</c>) and <c>Timertid</c> (<c>resource_timertime</c>) show
    /// <c>00:00:00,000</c>. The DTD agrees: <c>resource_timertime</c> declares
    /// <c>hour/minute/second/millisecond</c> all <c>#REQUIRED</c>, exactly as <c>resource_timer</c> does, and
    /// <c>resource_time</c> declares no millisecond at all.</para>
    ///
    /// <para>The writer keyed milliseconds to the tag <c>resource_timer</c> alone, so a Timertid edit would have
    /// silently dropped the millisecond the dialog collected — writing three of its four required fields.</para>
    /// </summary>
    public class TimerTimeInitialValueTests
    {
        private static IhcSettings Settings => TestSetup.Settings;
        private static ProjectAppService App => new(Settings);

        private static async Task<(ProjectDocumentSession session, ElementId variable)> WithVariable(string tag)
        {
            Project project = await App.Load("testdata/projects/project3-KompleksWired.vis");
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId block = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("locked") != "yes").Id!.Value;
            session.Apply(new AddVariable(block, "settings", tag, "Probe"));
            ElementId variable = session.Current!.FindById(block)!.Descendants()
                .First(e => e.Tag == tag && e.GetAttribute("name") == "Probe").Id!.Value;
            return (session, variable);
        }

        [TestCase("resource_timer")]
        [TestCase("resource_timertime")]
        public async Task Time_WritesMilliseconds_ForBothMillisecondCarryingTypes(string tag)
        {
            (ProjectDocumentSession session, ElementId variable) = await WithVariable(tag);

            session.Apply(new SetResourceInitialValue(variable,
                ResourceInitialValue.OfTime(1, 2, 3, millisecond: 456)));

            ProjectElement written = session.Current!.FindById(variable)!;
            Assert.Multiple(() =>
            {
                Assert.That(written.GetAttribute("hour"), Is.EqualTo("1"));
                Assert.That(written.GetAttribute("minute"), Is.EqualTo("2"));
                Assert.That(written.GetAttribute("second"), Is.EqualTo("3"));
                Assert.That(written.GetAttribute("millisecond"), Is.EqualTo("456"),
                    $"{tag} declares millisecond #REQUIRED and its dialog shows 00:00:00,000");
            });
        }

        /// <summary>The other half of the rule: a <c>resource_time</c> has no millisecond field, so the writer
        /// must not invent one. Asserted so widening the rule cannot become "write it everywhere".</summary>
        [Test]
        public async Task Time_WritesNoMillisecond_ForAPlainTime()
        {
            (ProjectDocumentSession session, ElementId variable) = await WithVariable("resource_time");

            session.Apply(new SetResourceInitialValue(variable,
                ResourceInitialValue.OfTime(1, 2, 3, millisecond: 456)));

            Assert.That(session.Current!.FindById(variable)!.GetAttribute("millisecond"), Is.Null,
                "resource_time declares no millisecond, and its dialog shows 00.00.00");
        }
    }
}
