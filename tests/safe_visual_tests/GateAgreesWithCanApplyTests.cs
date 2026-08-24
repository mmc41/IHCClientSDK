using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The agreement a menu gate rests on, pinned in ONE direction: if a registry row's gate ENABLES a command, the
/// SDK must actually allow it.
///
/// <para><b>Why one direction and not two.</b> A gate is allowed to be stricter than the SDK — that is surface
/// policy, and it is GUI-owned by design (a command hidden on a transient flyout, a family the shell does not
/// offer on a given node). What a gate may never do is be LOOSER: an enabled command that refuses on click is the
/// frontend telling its user something the engine will contradict, and it is the failure mode a registry gate
/// exists to prevent.</para>
///
/// <para><b>Only rows mintable without user input.</b> A row whose <c>Execute</c> raises a dialog first
/// (insert-a-product, rename, the program-authoring family) has no command to compare against until the user has
/// answered, so it is out of scope here rather than approximated with a fabricated answer.</para>
///
/// <para><b>The cases are the ones the GUI already probes</b> — delete, paste/move, link — because those are the
/// families whose gates were written against SDK verdicts and are therefore the ones where a divergence is a real
/// defect rather than a difference of intent.</para>
/// </summary>
public class GateAgreesWithCanApplyTests : AvaloniaTestBase
{
    /// <summary>One comparison: the row, the context to evaluate its gate in, and the command its Execute mints.</summary>
    private sealed record Case(string Row, string What, ShellContext Context, ProjectCommand Command);

    private static NodeContext Node(ElementId? id, TreeNodeKind kind, bool canCut = false) =>
        new(id, kind, IsPin: false, IsProductTerminal: false, IsLinkRow: false, IsLinkTarget: false,
            IsLogMarkPin: false, IsOutputPin: false, IsEventsContainer: false, IsCommandsContainer: false,
            IsConditionsContainer: false, IsCaseNode: false, IsLockedBlock: false,
            CanCut: canCut, CanCopy: canCut, CanReorder: canCut);

    [Test]
    public async Task AGateThatEnablesACommandNeverEnablesOneTheSdkRefuses()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ProjectWorkflow session = harness.Session;
        Project project = session.Current!;

        ElementId first = project.Groups[0].Id!.Value;
        ElementId second = project.Groups[1].Id!.Value;
        // A new project ships localities and no devices, so the product every paste/delete case needs is placed
        // here rather than assumed.
        ElementId product = (await session.AddProductAsync(
            first, harness.ProjectService.GetAvailableProducts().First().ProductIdentifier))!.Value;
        project = session.Current!;
        ShellContext open = vm.Context with { ProjectOpen = true };

        List<Case> cases =
        [
            new("edit.delete", "a product in a locality",
                open with { Node = Node(product, TreeNodeKind.Product, canCut: true) },
                session.Commands.DeleteNode(project, product, cascade: false)),

            new("edit.paste", "moving a product into another locality",
                open with
                {
                    Node = Node(second, TreeNodeKind.Locality),
                    Clipboard = new ClipboardContext(product, IsCut: true),
                },
                session.Commands.MoveNode(project, product, second)),

            new("edit.paste", "copying a product into another locality",
                open with
                {
                    Node = Node(second, TreeNodeKind.Locality),
                    Clipboard = new ClipboardContext(product, IsCut: false),
                },
                session.Commands.CopyNode(project, product, second)),

            // The interesting one: the gate asks only "is the target a locality, and is the clipboard full?".
            // Moving a locality INTO a locality is a different question, and only the SDK answers it.
            new("edit.paste", "moving a LOCALITY into another locality",
                open with
                {
                    Node = Node(second, TreeNodeKind.Locality),
                    Clipboard = new ClipboardContext(first, IsCut: true),
                },
                session.Commands.MoveNode(project, first, second)),

            new("edit.paste", "moving a locality into ITSELF",
                open with
                {
                    Node = Node(first, TreeNodeKind.Locality),
                    Clipboard = new ClipboardContext(first, IsCut: true),
                },
                session.Commands.MoveNode(project, first, first)),

            new("edit.paste", "moving a product onto the locality it already sits in",
                open with
                {
                    Node = Node(first, TreeNodeKind.Locality),
                    Clipboard = new ClipboardContext(product, IsCut: true),
                },
                session.Commands.MoveNode(project, product, first)),
        ];

        Assert.Multiple(() =>
        {
            foreach (Case probe in cases)
            {
                CommandSpec row = vm.Registry.Rows.Single(r => r.Id == probe.Row);
                EditVerdict gate = row.Gate(probe.Context);
                if (!gate.Ok)
                {
                    continue;   // stricter than the SDK is allowed; only the loose direction is a defect
                }

                EditVerdict sdk = session.CanApply(probe.Command);
                Assert.That(sdk.Ok, Is.True,
                    $"{probe.Row} — {probe.What}: the gate ENABLED it, so the SDK must allow it. "
                    + $"The SDK refused with: {sdk.Reason}");
            }
        });
    }

    /// <summary>
    /// The armed control. This suite would pass unchanged if every gate refused everything, so at least one case
    /// must be shown to reach the comparison at all — otherwise "no disagreement found" would mean "nothing was
    /// compared".
    /// </summary>
    [Test]
    public async Task AtLeastOneGateActuallyEnablesSomething()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        Project project = harness.Session.Current!;
        ElementId product = (await harness.Session.AddProductAsync(
            project.Groups[0].Id!.Value,
            harness.ProjectService.GetAvailableProducts().First().ProductIdentifier))!.Value;

        CommandSpec delete = vm.Registry.Rows.Single(r => r.Id == "edit.delete");
        EditVerdict gate = delete.Gate(vm.Context with
        {
            ProjectOpen = true,
            Node = Node(product, TreeNodeKind.Product, canCut: true),
        });

        Assert.That(gate.Ok, Is.True,
            "if this stops enabling, the agreement test above compares nothing and passes vacuously");
    }
}
