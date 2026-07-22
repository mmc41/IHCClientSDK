using System.Linq;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T016 / US-027 — a resource variable's typed INITIAL VALUE edits through <see cref="SetResourceInitialValue"/> and
    /// round-trips byte-faithfully: a bool writes <c>inivalue</c> on/off, a number a decimal <c>inivalue</c>, a timer
    /// hour/minute/second/millisecond (a resource_time omits millisecond). A locked block refuses the edit (T003). The
    /// unlocked "Custom blok" of project2 carries one variable of each type.
    /// </summary>
    public class InitialValueTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        private static Project Load() => ProjectReader.Read(TestData.ReadBytes("projects/project2-CustomBlock.vis"));

        // Scoped to the UNLOCKED "Custom blok" — the same variable name also appears in the locked "AutoProof".
        private static ElementId VarId(Project p, string tag, string name) =>
            p.Root.Descendants().First(e => e.Tag == "functionblock" && e.GetAttribute("name") == "Custom blok")
                .Descendants().First(e => e.Tag == tag && e.GetAttribute("name") == name).Id!.Value;

        // Applies the edit, asserts it commits, then asserts the edited project round-trips byte-identically
        // (serialise → reload → serialise is idempotent) and returns the reload for value checks.
        private static Project ApplyAndReload(Project project, ElementId id, ResourceInitialValue value)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            EditOutcome outcome = session.Apply(App.Commands.SetResourceInitialValue(session.Current!, id, value));
            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed), "the initial-value edit commits on an unlocked variable");

            byte[] serialized = ProjectSerializer.Serialize(session.Current!);
            Project reloaded = ProjectReader.Read(serialized);
            Assert.That(ProjectSerializer.Serialize(reloaded), Is.EqualTo(serialized), "the edited project round-trips byte-identically");
            return reloaded;
        }

        [Test]
        public void InitialValue_Bool_RoundTripsAsInivalueOnOff()
        {
            Project project = Load();
            ElementId id = VarId(project, "resource_flag", "Flag");

            Project reloaded = ApplyAndReload(project, id, ResourceInitialValue.OfBool(true));

            Assert.That(reloaded.FindById(id)!.GetAttribute("inivalue"), Is.EqualTo("on"));
        }

        [Test]
        public void InitialValue_Number_RoundTripsAsDecimalInivalue()
        {
            Project project = Load();
            ElementId id = VarId(project, "resource_counter", "Tæller");

            Project reloaded = ApplyAndReload(project, id, ResourceInitialValue.OfNumber(42));

            Assert.That(reloaded.FindById(id)!.GetAttribute("inivalue"), Is.EqualTo("42"));
        }

        [Test]
        public void InitialValue_Timer_RoundTripsAsHourMinuteSecondMillisecond()
        {
            Project project = Load();
            ElementId id = VarId(project, "resource_timer", "Timer");

            ProjectElement timer = ApplyAndReload(project, id, ResourceInitialValue.OfTime(1, 2, 3, 4)).FindById(id)!;

            Assert.Multiple(() =>
            {
                Assert.That(timer.GetAttribute("hour"), Is.EqualTo("1"));
                Assert.That(timer.GetAttribute("minute"), Is.EqualTo("2"));
                Assert.That(timer.GetAttribute("second"), Is.EqualTo("3"));
                Assert.That(timer.GetAttribute("millisecond"), Is.EqualTo("4"));
            });
        }

        [Test]
        public void InitialValue_Time_OmitsMillisecond()
        {
            Project project = Load();
            ElementId id = VarId(project, "resource_time", "Tidspunkt");

            ProjectElement time = ApplyAndReload(project, id, ResourceInitialValue.OfTime(5, 6, 7, 0)).FindById(id)!;

            Assert.Multiple(() =>
            {
                Assert.That(time.GetAttribute("hour"), Is.EqualTo("5"));
                Assert.That(time.GetAttribute("second"), Is.EqualTo("7"));
                Assert.That(time.GetAttribute("millisecond"), Is.Null, "a resource_time carries no millisecond");
            });
        }

        [Test]
        public void InitialValue_OnVariableInsideLockedBlock_IsRefused()
        {
            Project project = Load();
            ElementId id = project.Root.Descendants().First(e => e.Tag == "functionblock" && e.GetAttribute("name") == "AutoProof")
                .FindChild("outputs")!.ChildrenOrEmpty().First(e => e.Tag == "resource_output").Id!.Value;
            var session = new ProjectDocumentSession();
            session.Open(project);

            EditOutcome outcome = session.Apply(App.Commands.SetResourceInitialValue(session.Current!, id, ResourceInitialValue.OfBool(true)));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
        }
    }
}
