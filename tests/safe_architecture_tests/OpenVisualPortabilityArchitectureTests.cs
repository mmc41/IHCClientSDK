using System.Collections.Generic;
using System.Diagnostics;
using static Ihc.Tests.ArchRuleHelpers;

namespace Ihc.Tests
{
    public partial class OpenVisualArchitectureTests
    {
        // Launching something outside the app is the whole of Process.Start's use here, so the member name is the
        // rule: Start is the only way to start a process, and no other Process member is worth banning.
        private static readonly IReadOnlyCollection<string> ProcessStartMemberName = new[] { nameof(Process.Start) };

        /// <summary>
        /// The cross-platform desktop app opens URLs and generated report files through Avalonia's
        /// <c>ILauncher</c> (<c>TopLevel.Launcher</c>), never <c>Process.Start</c>.
        /// <para><c>Process.Start(UseShellExecute: true)</c> is a Windows shell concept that the runtime emulates
        /// elsewhere by shelling out to whatever it can find, and its failure mode is a launch that silently does
        /// nothing on one desktop while working on the developer's. The framework already resolves this per
        /// platform, and the owner window — which the launcher needs and the shell verb cannot use — is in hand at
        /// every call site in this app.</para>
        /// <para>Armed by <see cref="ProcessStartScan_IsArmed"/>: zero calls is the intended steady state, so
        /// without a positive control a green result would be indistinguishable from a scan that sees nothing.</para>
        /// </summary>
        [Test]
        public void Gui_DoesNotStartProcesses() =>
            AssertDoesNotCallMembers(Gui, GuiRoot, typeof(Process).FullName!,
                ProcessStartMemberName, "Process.Start",
                "the GUI opens files and URLs through Avalonia's ILauncher, which resolves the per-platform handler that Process.Start(UseShellExecute) only emulates");

        // Seeded violator for the positive control: an unexempted caller that really does start a process.
        private static class SeededProcessStartCaller
        {
            public static void Call() => Process.Start(new ProcessStartInfo { FileName = "x", UseShellExecute = true });
        }

        /// <summary>Run against this test assembly — where <see cref="SeededProcessStartCaller"/> calls it — the
        /// same ban must report, proving it detects a real <c>Process.Start</c> edge.</summary>
        [Test]
        public void ProcessStartScan_IsArmed() =>
            Assert.That(
                () => AssertDoesNotCallMembers(OwnTestAssembly.Value, typeof(OpenVisualArchitectureTests).Namespace!,
                    typeof(Process).FullName!, ProcessStartMemberName, "seeded probe", "seeded probe",
                    exemptCallSites: null),
                Throws.InstanceOf<AssertionException>(),
                "the Process.Start scan must report the seeded call");
    }
}
