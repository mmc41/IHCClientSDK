using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using ihc_openvisual.Configuration;
using ihc_openvisual.Views;

namespace safe_visual_tests;

/// <summary>
/// The ids a driver targets, pinned to the controls that publish them.
///
/// <para><see cref="AutomationIds"/> exists so the application and the end-to-end suite name an element through
/// ONE declaration instead of two string literals that drift apart silently. A constant is only worth that if it
/// still resolves, so this fixture opens each owning window and asserts the id is published there — exactly once,
/// because an ambiguous id is as useless to a driver as a missing one.</para>
///
/// <para><b>It reads the ATTACHED PROPERTY, not the peer.</b> Avalonia's peer falls back to <c>Owner.Name</c>, so
/// a control identified only by <c>x:Name</c> answers a peer query and looks fine — while x:Name is a private
/// detail a rename tool changes without a word, and nothing tells the driver. Reading
/// <see cref="AutomationProperties.GetAutomationId(StyledElement)"/> is what makes the id an explicit, authored
/// contract rather than a coincidence.</para>
/// </summary>
public class AutomationIdConstantsTests : AvaloniaTestBase
{
    /// <summary>The ids the shell publishes.</summary>
    private static readonly string[] ShellIds =
    [
        AutomationIds.MainWindow,
        AutomationIds.InstallationTree,
        AutomationIds.FunctionsTree,
        AutomationIds.MenuBar,
        AutomationIds.MenuEdit,
        AutomationIds.MenuView,
        AutomationIds.MenuDocumentation,
        AutomationIds.ProblemsPanel,
        AutomationIds.ProblemsList,
        AutomationIds.ProblemsStateText,
        AutomationIds.ProblemsSpinner,
    ];

    /// <summary>The ids each dialog publishes, by the window that owns them.</summary>
    private static readonly (Type Window, string[] Ids)[] DialogIds =
    [
        (typeof(ProjectInfoWindow), [AutomationIds.ProjectInfoWindow, AutomationIds.ProjNumberBox]),
        (typeof(PinPropertiesWindow),
            [
                AutomationIds.PinPropertiesWindow,
                AutomationIds.CableColourBox,
                AutomationIds.TerminalList,
                AutomationIds.OkButton,
                AutomationIds.CancelButton,
            ]),
        (typeof(ProductDialogWindow), [AutomationIds.ProductDialogWindow]),
    ];

    /// <summary>
    /// A prefix rather than an id: the tier count ids are composed per tier, so it is checked against the
    /// view-model that composes them instead of against a control.
    /// </summary>
    private static readonly string[] NotElementIds = [AutomationIds.ProblemsCountPrefix];

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task EveryConstantIsPublishedByExactlyOneControlInItsWindow()
    {
        List<string> failures = [];

        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var shell = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = shell;
        shell.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        failures.AddRange(Unresolved(shell, ShellIds));

        foreach ((Type type, string[] ids) in DialogIds)
        {
            var window = (Window)Activator.CreateInstance(type)!;
            CurrentTestWindow = window;
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            failures.AddRange(Unresolved(window, ids));
            window.Close();
        }

        Assert.That(failures, Is.Empty,
            "every AutomationIds constant must be published by exactly one control in its window, as an EXPLICIT "
            + "AutomationProperties.AutomationId. A count of 0 usually means the control carries only x:Name — "
            + "which the automation peer falls back to, so a driver finds it today and stops finding it the day "
            + "someone renames the field:"
            + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", failures));
    }

    /// <summary>
    /// The tier count ids compose from the prefix rather than repeating it, so the vocabulary a driver types
    /// follows the tier set instead of being a second hand-kept list.
    /// </summary>
    [AvaloniaTest]
    public async Task EveryTierComposesItsCountIdFromThePrefix()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();

        var offending = viewModel.Problems.Tiers
            .Where(tier => !tier.CountAutomationId.StartsWith(AutomationIds.ProblemsCountPrefix, StringComparison.Ordinal))
            .Select(tier => $"{tier.Tier} publishes '{tier.CountAutomationId}'")
            .ToList();

        Assert.That(offending, Is.Empty,
            "a tier's count id must be composed from AutomationIds.ProblemsCountPrefix, so the constant and the "
            + "ids the panel actually publishes cannot drift: " + string.Join("; ", offending));
    }

    /// <summary>
    /// The tables above are hand-maintained, so a constant added later would simply never be looked at. This
    /// closes that loop: a new constant has to be routed to the window that owns it, or declared as one of the
    /// deliberate non-element values.
    /// </summary>
    [Test]
    public void EveryDeclaredConstantIsCheckedBySomething()
    {
        var declared = typeof(AutomationIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();
        Assert.That(declared, Is.Not.Empty, "sanity: AutomationIds declares string constants");

        var checkedHere = ShellIds
            .Concat(DialogIds.SelectMany(entry => entry.Ids))
            .Concat(NotElementIds)
            .ToList();

        Assert.That(declared.Except(checkedHere), Is.Empty,
            "an AutomationIds constant that no table here names is a constant nothing verifies — add it to the "
            + "window that publishes it, or to NotElementIds with the reason it is not one");
    }

    /// <summary>
    /// The ids in <paramref name="ids"/> that are NOT published exactly once by <paramref name="window"/> or one
    /// of its logical descendants, each reported with the count actually found.
    /// </summary>
    private static IEnumerable<string> Unresolved(Window window, IEnumerable<string> ids)
    {
        var published = window.GetLogicalDescendants().OfType<StyledElement>()
            .Prepend(window)
            .Select(AutomationProperties.GetAutomationId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        foreach (string id in ids)
        {
            int count = published.Count(candidate => string.Equals(candidate, id, StringComparison.Ordinal));
            if (count != 1)
                yield return $"{window.GetType().Name} publishes '{id}' {count} times, expected once";
        }
    }
}
