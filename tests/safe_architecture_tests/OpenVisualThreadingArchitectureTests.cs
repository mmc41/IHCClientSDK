using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Ihc.Tests.ArchRuleHelpers;

namespace Ihc.Tests
{
    public partial class OpenVisualArchitectureTests
    {
        /// <summary>
        /// The GUI threading contract: no <c>ConfigureAwait</c> anywhere in the app. A single
        /// <c>ConfigureAwait(false)</c> upstream of a document mutation resumes the continuation on a pool thread,
        /// and the resulting Avalonia failure can be silent, such as an <c>ObservableCollection</c> update not
        /// reaching the UI. That is why this is a blanket call ban rather than an operand check:
        /// <c>ConfigureAwait(true)</c> is pointless in GUI code, so banning the call outright is both simpler and
        /// stricter, and nothing else automated watches future GUI code for this.
        /// The ban is by member NAME on any declaring type, since <c>ConfigureAwait</c> is declared separately on
        /// <c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c>, <c>ValueTask&lt;T&gt;</c> and the async-enumerable
        /// extensions. Armed by <see cref="ConfigureAwaitScan_IsArmed"/>.
        /// <para>The allowlist that used to exempt the auto-backup writer's read-only path went with that feature:
        /// the ban is now absolute, so a new exemption is a decision to take deliberately, not a slot to fill.</para>
        /// </summary>
        [Test]
        public void Gui_DoesNotCallConfigureAwait() =>
            AssertDoesNotCallMembers(Gui, GuiRoot, targetTypeFullName: null,
                ConfigureAwaitMemberName, "ConfigureAwait",
                "GUI continuations must stay on the UI thread — ConfigureAwait(false) upstream of a document mutation can silently lose an Avalonia update",
                exemptCallSites: null);

        private static readonly IReadOnlyCollection<string> ConfigureAwaitMemberName = new[] { "ConfigureAwait" };

        // Seeded violator for the ConfigureAwait ban's positive control: an unexempted type that genuinely calls it,
        // on both a Task and a ValueTask, so the name-based match is proven against more than one declaring type.
        private static class SeededConfigureAwaitCaller
        {
            public static async Task Call(Task work, ValueTask valueWork)
            {
                await work.ConfigureAwait(false);
                await valueWork.ConfigureAwait(false);
            }
        }

        /// <summary>The positive control for <see cref="Gui_DoesNotCallConfigureAwait"/>: run against this test
        /// assembly — where <see cref="SeededConfigureAwaitCaller"/> calls it on two different declaring types — the
        /// ban must report. Proves the scan detects real ConfigureAwait edges rather than passing because it sees
        /// nothing at all — the seeded caller awaits through the same compiler-generated async state machine an
        /// authored GUI method would, so a call model that lost those edges would fail here.</summary>
        [Test]
        public void ConfigureAwaitScan_IsArmed() =>
            Assert.That(
                () => AssertDoesNotCallMembers(OwnTestAssembly.Value, typeof(OpenVisualArchitectureTests).Namespace!,
                    targetTypeFullName: null, ConfigureAwaitMemberName, "seeded probe", "seeded probe",
                    exemptCallSites: null),
                Throws.InstanceOf<AssertionException>(),
                "the blanket ConfigureAwait scan must report the seeded Task and ValueTask calls");

    }
}
