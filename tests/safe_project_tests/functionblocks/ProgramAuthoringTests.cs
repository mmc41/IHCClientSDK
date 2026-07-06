using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// M3 / 3.5 — <see cref="ProgramBuilder"/>: authoring a custom program the way IHC Visual does when a user
    /// fills an empty function block's <c>program_simple</c> by hand (project2's "Custom blok", counters 204–247).
    /// Exercises each grammar primitive from the action script — <c>event_power</c>, <c>event</c> (link1/method,
    /// optional link2), the nested <c>program_sub</c> 4-id skeleton (program_sub + conditions + true/false actions),
    /// <c>condition</c>, <c>action</c>, and a <c>resource_enum</c> operand embedded inside a condition. Verified
    /// structurally (tags, nesting, id-allocation order, wiring attributes); the full byte compare is V4 (step 3.7).
    /// Catalog-free: authors into Project1's first <c>program_simple</c> (which owns real <c>events</c>/<c>actions</c>
    /// containers) and references resources it adds to that block — so the allocator carries a realistic high-water.
    /// </summary>
    public class ProgramAuthoringTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> Load() => new ProjectAppService(Settings).Load("testdata/projects/Project1-SimpelWired.vis");

        private static long HexCounter(string? token) =>
            long.Parse(token!.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        private static string Token(long counter) => "_0x" + counter.ToString("x", CultureInfo.InvariantCulture);

        /// <summary>The first <c>program_simple</c> in document order — the hand-authoring target.</summary>
        private static ElementId FirstProgram(Project p) =>
            p.Root.Descendants().First(e => e.Tag == "program_simple").Id!.Value;

        /// <summary>The (room, block) names of the first function block, discovered from the read model (no hard-coding).</summary>
        private static (string Room, string Block) FirstBlock(Project p)
        {
            foreach (ProjectElement g in p.Groups)
            {
                if (g.Children.IsDefaultOrEmpty)
                {
                    continue;
                }
                foreach (ProjectElement c in g.Children)
                {
                    if (c.Tag == "functionblock")
                    {
                        return (g.GetAttribute("name")!, c.GetAttribute("name")!);
                    }
                }
            }
            throw new InvalidOperationException("Project1 has no function block.");
        }

        /// <summary>Fetches the element carrying the given allocation counter (reorder-immune identity).</summary>
        private static ProjectElement ByCounter(Project p, long counter) =>
            p.Root.Descendants().First(e => e.Id is { } id && id.Counter == counter);

        [Test]
        public async Task AddPowerEvent_AppendsEventPowerToEvents_AllocatesOneId()
        {
            Project project = await Load();
            long seed = HexCounter(project.LastUniqueId);
            ElementId progId = FirstProgram(project);
            ProjectEditor editor = project.Edit();

            editor.Program(progId).AddPowerEvent("Powerup");
            Project after = editor.ToProject();

            ProjectElement power = ByCounter(after, seed + 1);
            Assert.Multiple(() =>
            {
                Assert.That(power.Tag, Is.EqualTo("event_power"));
                Assert.That(power.GetAttribute("name"), Is.EqualTo("Powerup"));
                Assert.That(power.GetAttribute("icon"), Is.EqualTo("_0xc"));
                Assert.That(power.GetAttribute("link1"), Is.Null, "a power-up trigger wires no resource");
                Assert.That(after.FindParent(power.Id!.Value)!.Tag, Is.EqualTo("events"), "appended under events");
                Assert.That(after.LastUniqueId, Is.EqualTo(Token(seed + 1)));
            });
        }

        [Test]
        public async Task AddEvent_WiresLink1Link2AndMethod_AppendsEventsToEventsContainer()
        {
            Project project = await Load();
            ElementId progId = FirstProgram(project);
            (string room, string block) = FirstBlock(project);
            ProjectEditor editor = project.Edit();
            FunctionBlockRef fb = editor.Group(room).FunctionBlock(block);
            ResourceRef p = fb.AddInput("__pgm_p");
            ResourceRef s = fb.AddInput("__pgm_s");
            long baseCtr = s.Id!.Value.Counter;

            editor.Program(progId)
                .AddEvent("%P -> ON", p, method: "_0xa")
                .AddEvent("%P -> %S", p, method: "_0x1e", link2: s);
            Project after = editor.ToProject();

            ProjectElement e1 = ByCounter(after, baseCtr + 1);
            ProjectElement e2 = ByCounter(after, baseCtr + 2);
            Assert.Multiple(() =>
            {
                Assert.That(e1.Tag, Is.EqualTo("event"));
                Assert.That(e1.GetAttribute("name"), Is.EqualTo("%P -> ON"));
                Assert.That(e1.GetAttribute("icon"), Is.EqualTo("_0xc"));
                Assert.That(e1.GetAttribute("link1"), Is.EqualTo(p.Id!.Value.ToToken()));
                Assert.That(e1.GetAttribute("link2"), Is.Null, "single-operand event has no link2");
                Assert.That(e1.GetAttribute("method"), Is.EqualTo("_0xa"));
                Assert.That(after.FindParent(e1.Id!.Value)!.Tag, Is.EqualTo("events"));

                Assert.That(e2.GetAttribute("link1"), Is.EqualTo(p.Id!.Value.ToToken()));
                Assert.That(e2.GetAttribute("link2"), Is.EqualTo(s.Id!.Value.ToToken()), "two-operand event wires link2");
                Assert.That(e2.GetAttribute("method"), Is.EqualTo("_0x1e"));
            });
        }

        [Test]
        public async Task AddSubProgram_AllocatesFourConsecutiveIds_ConditionsPlusTwoActionBranches()
        {
            Project project = await Load();
            long seed = HexCounter(project.LastUniqueId);
            ElementId progId = FirstProgram(project);
            ProjectEditor editor = project.Edit();

            editor.Program(progId).AddSubProgram();
            Project after = editor.ToProject();

            ProjectElement sub = ByCounter(after, seed + 1);
            Assert.Multiple(() =>
            {
                Assert.That(sub.Tag, Is.EqualTo("program_sub"));
                Assert.That(sub.GetAttribute("name"), Is.EqualTo("Under program"));
                Assert.That(sub.GetAttribute("icon"), Is.EqualTo("_0x7"));
                Assert.That(after.FindParent(sub.Id!.Value)!.Tag, Is.EqualTo("actions"), "under the program's root actions");
                Assert.That(sub.Children.Select(c => c.Tag),
                    Is.EqualTo(new[] { "conditions", "actions", "actions" }),
                    "auto-children: conditions + true/false action branches");
                Assert.That(sub.Children.Select(c => c.Id!.Value.Counter),
                    Is.EqualTo(new[] { seed + 2, seed + 3, seed + 4 }),
                    "4 consecutive ids allocated in document order (R1)");
                Assert.That(sub.Children[0].GetAttribute("name"), Is.EqualTo("Betingelser"));
                Assert.That(sub.Children[0].GetAttribute("icon"), Is.EqualTo("_0x16"));
                Assert.That(sub.Children[1].GetAttribute("type"), Is.EqualTo("_0x1"), "true-branch actions carry type=_0x1");
                Assert.That(sub.Children[2].GetAttribute("type"), Is.Null, "false-branch actions omit type (DTD default _0x0)");
                Assert.That(after.LastUniqueId, Is.EqualTo(Token(seed + 4)));
            });
        }

        [Test]
        public async Task AddCondition_AppendsToSubProgramConditions_WiresLink1Method()
        {
            Project project = await Load();
            ElementId progId = FirstProgram(project);
            (string room, string block) = FirstBlock(project);
            ProjectEditor editor = project.Edit();
            FunctionBlockRef fb = editor.Group(room).FunctionBlock(block);
            ResourceRef p = fb.AddInput("__pgm_p");
            long baseCtr = p.Id!.Value.Counter;

            SubProgramRef sub = editor.Program(progId).AddSubProgram();   // baseCtr+1..+4
            sub.AddCondition("%P = OFF", p, method: "_0x14");             // baseCtr+5
            Project after = editor.ToProject();

            ProjectElement cond = ByCounter(after, baseCtr + 5);
            Assert.Multiple(() =>
            {
                Assert.That(cond.Tag, Is.EqualTo("condition"));
                Assert.That(cond.GetAttribute("name"), Is.EqualTo("%P = OFF"));
                Assert.That(cond.GetAttribute("icon"), Is.EqualTo("_0x1a"));
                Assert.That(cond.GetAttribute("link1"), Is.EqualTo(p.Id!.Value.ToToken()));
                Assert.That(cond.GetAttribute("method"), Is.EqualTo("_0x14"));
                Assert.That(after.FindParent(cond.Id!.Value)!.Tag, Is.EqualTo("conditions"));
                Assert.That(after.FindParent(cond.Id!.Value)!.Id!.Value.Counter, Is.EqualTo(baseCtr + 2),
                    "into the sub-program's own conditions container");
            });
        }

        [Test]
        public async Task AddAction_IntoTrueAndFalseBranches_WiresLink1Method()
        {
            Project project = await Load();
            ElementId progId = FirstProgram(project);
            (string room, string block) = FirstBlock(project);
            ProjectEditor editor = project.Edit();
            FunctionBlockRef fb = editor.Group(room).FunctionBlock(block);
            ResourceRef p = fb.AddInput("__pgm_p");
            long baseCtr = p.Id!.Value.Counter;

            SubProgramRef sub = editor.Program(progId).AddSubProgram();   // baseCtr+1..+4
            sub.WhenTrue.AddAction("%P = ON", p, method: "_0xa");         // baseCtr+5 (into sande = baseCtr+3)
            sub.WhenFalse.AddAction("%P = OFF", p, method: "_0x14");      // baseCtr+6 (into falske = baseCtr+4)
            Project after = editor.ToProject();

            ProjectElement onAct = ByCounter(after, baseCtr + 5);
            ProjectElement offAct = ByCounter(after, baseCtr + 6);
            Assert.Multiple(() =>
            {
                Assert.That(onAct.Tag, Is.EqualTo("action"));
                Assert.That(onAct.GetAttribute("name"), Is.EqualTo("%P = ON"));
                Assert.That(onAct.GetAttribute("icon"), Is.EqualTo("_0x9"));
                Assert.That(onAct.GetAttribute("method"), Is.EqualTo("_0xa"));
                Assert.That(after.FindParent(onAct.Id!.Value)!.Id!.Value.Counter, Is.EqualTo(baseCtr + 3),
                    "true-branch action lands in the sande actions container");
                Assert.That(offAct.GetAttribute("name"), Is.EqualTo("%P = OFF"));
                Assert.That(after.FindParent(offAct.Id!.Value)!.Id!.Value.Counter, Is.EqualTo(baseCtr + 4),
                    "false-branch action lands in the falske actions container");
            });
        }

        [Test]
        public async Task AddEnumOperand_AllocatesResourceEnumInsideCondition_WiresConditionLink2()
        {
            Project project = await Load();
            ElementId progId = FirstProgram(project);
            (string room, string block) = FirstBlock(project);
            ProjectEditor editor = project.Edit();
            FunctionBlockRef fb = editor.Group(room).FunctionBlock(block);
            ResourceRef p = fb.AddInput("__pgm_p");
            EnumDefinitionRef def = editor.AddEnumDefinition("NyType", "Værdi1", "Værdi2");
            ElementId.TryParse(def.InitialValue("Værdi2"), out ElementId lastValue);
            long baseCtr = lastValue.Counter;   // high-water = the def's last value (id token → counter)

            SubProgramRef sub = editor.Program(progId).AddSubProgram();          // baseCtr+1..+4
            ConditionRef cond = sub.AddCondition("%P <> %S", p, method: "_0x28"); // baseCtr+5
            ResourceRef operand = cond.AddEnumOperand("Enumerator", def, "Værdi2"); // baseCtr+6
            Project after = editor.ToProject();

            ProjectElement condEl = ByCounter(after, baseCtr + 5);
            ProjectElement enumEl = ByCounter(after, baseCtr + 6);
            Assert.Multiple(() =>
            {
                Assert.That(enumEl.Tag, Is.EqualTo("resource_enum"));
                Assert.That(enumEl.GetAttribute("name"), Is.EqualTo("Enumerator"));
                Assert.That(enumEl.GetAttribute("typedef"), Is.EqualTo(def.Typedef));
                Assert.That(enumEl.GetAttribute("inivalue"), Is.EqualTo(def.InitialValue("Værdi2")));
                Assert.That(enumEl.GetAttribute("icon"), Is.EqualTo("_0x22"));
                Assert.That(operand.Id!.Value.Counter, Is.EqualTo(baseCtr + 6));
                Assert.That(after.FindParent(enumEl.Id!.Value)!.Id!.Value, Is.EqualTo(condEl.Id!.Value),
                    "the enum operand is a child of the condition");
                Assert.That(condEl.GetAttribute("link2"), Is.EqualTo(enumEl.GetAttribute("id")),
                    "the condition's link2 points at its embedded enum operand");
                Assert.That(condEl.GetAttribute("link1"), Is.EqualTo(p.Id!.Value.ToToken()));
            });
        }
    }
}
