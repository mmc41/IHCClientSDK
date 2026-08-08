using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Ihc.Tests.ArchRuleHelpers;

namespace Ihc.Tests
{
    public partial class OpenVisualArchitectureTests
    {
        // The exact sanctioned call site: AutoBackupScheduler.WriteAsync only reads document.Current and performs
        // the serialized backup write. The internal type is name-anchored through its public Services sibling; the
        // control below proves both the type and authored-method mapping stay live.
        private static readonly string AutoBackupSchedulerName =
            typeof(global::ihc_openvisual.Services.ProjectWorkflow).Namespace + ".AutoBackupScheduler";

        private static IReadOnlyCollection<MethodCallExemption> ConfigureAwaitAllowlist => new[]
        {
            new MethodCallExemption(AutoBackupSchedulerName, "WriteAsync"),
        };

        /// <summary>
        /// The GUI threading contract: no <c>ConfigureAwait</c> anywhere in the app except
        /// <c>AutoBackupScheduler.WriteAsync</c>. A single <c>ConfigureAwait(false)</c> upstream of a
        /// document mutation resumes the continuation on a pool thread, and the resulting Avalonia failure can be
        /// silent, such as an <c>ObservableCollection</c> update not reaching the UI. That is why this is a blanket call ban
        /// rather than an operand check: <c>ConfigureAwait(true)</c> is pointless in GUI code, so banning the call
        /// outright is both simpler and stricter, and nothing else automated watches future GUI code for this.
        /// The ban is by member NAME on any declaring type, since <c>ConfigureAwait</c> is declared separately on
        /// <c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c>, <c>ValueTask&lt;T&gt;</c> and the async-enumerable
        /// extensions. Armed by <see cref="ConfigureAwaitScan_IsArmed"/>.
        /// </summary>
        [Test]
        public void Gui_DoesNotCallConfigureAwait() =>
            AssertDoesNotCallMembers(Gui, GuiRoot, targetTypeFullName: null,
                ConfigureAwaitMemberName, "ConfigureAwait outside the auto-backup writer",
                "GUI continuations must stay on the UI thread — ConfigureAwait(false) upstream of a document mutation can silently lose an Avalonia update",
                ConfigureAwaitAllowlist);

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
        /// assembly — where <see cref="SeededConfigureAwaitCaller"/> calls it on two different declaring types and is
        /// NOT allowlisted — the ban must report. Proves the scan detects real ConfigureAwait edges rather than
        /// passing because the GUI happens to make none outside the allowlist.</summary>
        [Test]
        public void ConfigureAwaitScan_IsArmed()
        {
            Assert.That(
                () => AssertDoesNotCallMembers(OwnTestAssembly.Value, typeof(OpenVisualArchitectureTests).Namespace!,
                    targetTypeFullName: null, ConfigureAwaitMemberName, "seeded probe", "seeded probe",
                    exemptCallSites: null),
                Throws.InstanceOf<AssertionException>(),
                "the blanket ConfigureAwait scan must report the seeded Task and ValueTask calls");

            Assert.That(typeof(global::ihc_openvisual.App).Assembly.GetType(AutoBackupSchedulerName), Is.Not.Null,
                $"the allowlisted '{AutoBackupSchedulerName}' must still exist — a rename would leave a dead allowlist entry that exempts nothing while reading as if it does");

            var realConfigureAwaitSites = MethodCallEdges(Gui, GuiRoot)
                .Where(edge => ConfigureAwaitMemberName.Contains(edge.Member))
                .Select(edge => new MethodCallExemption(OutermostTypeName(edge.Origin), edge.OriginMember))
                .Distinct()
                .ToList();
            Assert.That(realConfigureAwaitSites, Is.EquivalentTo(ConfigureAwaitAllowlist),
                "the call model must map AutoBackupScheduler's async state-machine edges back to the exact authored WriteAsync method");

                // The strongest control available: the same ban over the REAL GUI with an empty allowlist must fail.
                // That proves in one shot that the scan sees the GUI's genuine ConfigureAwait call (not merely the
                // seeded one in this test assembly) and that the allowlist — not an empty result set — is what makes
                // the rule green. If the auto-backup writer ever stops calling ConfigureAwait, delete the allowlist
                // and this assertion together; do not weaken the ban.
            Assert.That(
                () => AssertDoesNotCallMembers(Gui, GuiRoot, targetTypeFullName: null,
                    ConfigureAwaitMemberName, "allowlist probe", "allowlist probe",
                    exemptCallSites: null),
                Throws.InstanceOf<AssertionException>(),
                "the GUI's one sanctioned ConfigureAwait call must be visible to the scan — otherwise the ban is green because it detects nothing");
        }

    }
}
