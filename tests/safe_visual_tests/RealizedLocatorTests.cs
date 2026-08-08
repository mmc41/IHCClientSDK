using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis;

namespace safe_visual_tests;

/// <summary>
/// UX review SPEC-01 / USE-01: the controls a UIA client meets that are NOT authored one-by-one in markup — the
/// containers realized from an <c>ItemsSource</c>. <see cref="AutomationCoverageTests"/> deliberately exempts them
/// from its "every control carries an id" audit because their identity is the bound datum; this fixture is the other
/// half of that bargain — the datum must actually supply one.
/// <list type="bullet">
/// <item>The Recent-projects submenu generated four items that carried NO id at all, so the only way to invoke one
/// was its display text — a file name, i.e. user data.</item>
/// <item>The module map's two lists rendered 24 rows of loose <c>TextBlock</c>s. Avalonia's Windows bridge exposes
/// no Grid/Table pattern, so a client cannot ask for a cell by row and column; what it CAN do is read a row as one
/// labelled element — provided the row publishes a name and an id, which it did not.</item>
/// </list>
/// </summary>
public class RealizedLocatorTests : AvaloniaTestBase
{
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task RecentProjectMenuItems_ExposeDistinctAutomationIds()
    {
        using var harness = ShellHarness.Create();
        harness.Recent.Add(harness.TempPath("alpha.vis"));
        harness.Recent.Add(harness.TempPath("beta.vis"));

        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var declared = window.GetLogicalDescendants().OfType<MenuItem>().ToList();
        declared.First(item => AutomationProperties.GetAutomationId(item) == "MenuFile").Open();
        Dispatcher.UIThread.RunJobs();
        MenuItem recentMenu = declared.First(item => item.Name == "RecentProjectsMenu");
        recentMenu.Open();
        Dispatcher.UIThread.RunJobs();

        var ids = recentMenu.Items.OfType<RecentProjectViewModel>()
            .Select(entry => recentMenu.ContainerFromItem(entry))
            .OfType<MenuItem>()
            .Select(AutomationProperties.GetAutomationId)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(ids, Has.Count.EqualTo(2), "precondition: both recent entries materialized");
            Assert.That(ids, Has.All.Not.Null.And.All.Not.Empty,
                "a generated menu item is addressable by id, not only by the file name it displays");
            Assert.That(ids, Is.Unique);
        });
    }

    /// <summary>
    /// The general form of the two cases below, over the whole shell: wherever a bound datum OFFERS an
    /// <c>AutomationId</c>, the container realized for it must publish that id, and ids must be unique among
    /// siblings. Both halves are needed and neither is a spot check:
    /// <list type="bullet">
    /// <item>The id reaches the container through one <c>Setter</c> per item-container theme (three in the shell's
    /// markup). A new templated surface — another tree, another generated submenu — is a fourth, and forgetting it
    /// costs nothing visible: the rows render, the datum's property is still there, and only the automation tree is
    /// blank. The <see cref="AutomationCoverageTests"/> audit deliberately exempts data-realized containers, so
    /// nothing else would report it.</item>
    /// <item>Uniqueness is the other half, and the one that actually bit: the tree row's id was the node KIND, so
    /// ten sibling localities published the same id and a driver asking for one got ten elements back. An id that
    /// is not unique among its siblings is not an address.</item>
    /// </list>
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task EveryRealizedContainer_PublishesTheUniqueIdItsDatumOffers()
    {
        using var harness = ShellHarness.Create();
        // Two recent entries: real data that offers an id, used first by the shell itself and then — as the armed
        // probe below — by a plain ItemsControl that binds nothing.
        harness.Recent.Add(harness.TempPath("alpha.vis"));
        harness.Recent.Add(harness.TempPath("beta.vis"));
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        // Expand both trees so nested containers are realized too — an unexpanded node has no container, and a rule
        // that only ever sees root rows would miss precisely the sibling collisions it exists to catch.
        ExpandAll(vm.InstallationNodes);
        ExpandAll(vm.FunctionNodes);
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();

        List<string> failures = AuditDataContainers(window, out int inspected);

        Assert.Multiple(() =>
        {
            Assert.That(inspected, Is.GreaterThan(5),
                "sanity: the shell realizes containers whose data offer ids — otherwise this rule inspects nothing");
            Assert.That(failures, Is.Empty,
                "a container realized from a datum that offers an AutomationId publishes it, uniquely among its "
                + $"siblings ({failures.Count} gap(s)):\n  " + string.Join("\n  ", failures));

            // ARMED, over the exact shape the rule is for: two data that offer ids, realized by a plain ItemsControl
            // whose container theme binds none of them. Without this, a green result could equally mean the walk
            // found no containers to judge — which is how a coverage net rots after the markup it watches is
            // restructured.
            var unbound = new ItemsControl { ItemsSource = vm.RecentProjects.ToList() };
            var probe = new Window { Content = unbound };
            probe.Show();
            Dispatcher.UIThread.RunJobs();
            probe.CaptureRenderedFrame();

            Assert.That(AuditDataContainers(probe, out int probed), Has.Count.EqualTo(2),
                "armed: a container that does not publish the id its datum offers must be reported");
            Assert.That(probed, Is.EqualTo(2), "armed: and both seeded containers must have been inspected");
            probe.Close();
        });
    }

    /// <summary>Every realized container under <paramref name="root"/> whose datum offers an id it does not publish,
    /// plus every id shared by siblings. <paramref name="inspected"/> reports how many containers were judged, so a
    /// caller can tell "nothing wrong" apart from "nothing looked at".</summary>
    private static List<string> AuditDataContainers(Visual root, out int inspected)
    {
        var failures = new List<string>();
        int judged = 0;
        foreach (ItemsControl items in root.GetVisualDescendants().OfType<ItemsControl>())
        {
            var published = new List<string>();
            foreach (Control container in items.GetRealizedContainers())
            {
                if (DatumAutomationId(container.DataContext) is not { } offered)
                    continue;

                judged++;
                string? actual = AutomationProperties.GetAutomationId(container);
                if (actual == offered)
                    published.Add(actual);
                else
                    failures.Add($"{container.GetType().Name} for '{offered}' publishes '{actual}' — the datum's id is not bound");
            }

            failures.AddRange(published
                .GroupBy(id => id)
                .Where(group => group.Count() > 1)
                .Select(group => $"{items.GetType().Name}: id '{group.Key}' is shared by {group.Count()} siblings"));
        }

        inspected = judged;
        return failures;
    }

    /// <summary>The id a bound datum offers, by convention: a public <c>string AutomationId</c> property (the shape
    /// <c>TreeNodeViewModel</c>, <c>RecentProjectViewModel</c> and <c>ProductMenuItemViewModel</c> all publish).
    /// Read by name rather than through an interface so a NEW datum type is covered the moment it adopts the
    /// convention — the point being to catch surfaces nobody remembered to add to a test.</summary>
    private static string? DatumAutomationId(object? datum) =>
        datum?.GetType().GetProperty("AutomationId", typeof(string))?.GetValue(datum) as string;

    private static void ExpandAll(IEnumerable<TreeNodeViewModel> nodes)
    {
        foreach (TreeNodeViewModel node in nodes)
        {
            node.IsExpanded = true;
            ExpandAll(node.Children);
        }
    }

    // One row of the module map, read as a UIA client reads it: a single element with a name that carries every
    // column, and an id that identifies which data line it is.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void ModuleMapRows_PublishALabelledSummaryAndAStableId()
    {
        var map = new DatalineModuleMap(
            [new DatalineModule(1, "Udgangsmodul", "Køkken", "Loftlampe"), new DatalineModule(2, "", "", "")],
            ImmutableArray<DatalineModule>.Empty);
        var window = new ModuleMapWindow { DataContext = map };
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var rows = window.GetLogicalDescendants().OfType<ItemsControl>()
            .First(c => c.Name == "InputModulesList")
            .GetLogicalDescendants().OfType<Grid>()
            .Where(g => AutomationProperties.GetAutomationId(g) is { Length: > 0 })
            .ToList();

        Assert.That(rows, Has.Count.EqualTo(2), "both data lines realize as addressable rows");
        Assert.Multiple(() =>
        {
            Assert.That(rows.Select(AutomationProperties.GetAutomationId),
                Is.EqualTo(new[] { "inputModule.1", "inputModule.2" }));
            string occupied = ControlAutomationPeer.CreatePeerForElement(rows[0]).GetName();
            string free = ControlAutomationPeer.CreatePeerForElement(rows[1]).GetName();
            Assert.That(occupied, Is.EqualTo("Datalinie 1, Udgangsmodul, Køkken, Loftlampe"),
                "an occupied line reads as one sentence — the columns a sighted reader gets from the header row");
            Assert.That(free, Is.EqualTo("Datalinie 2, ikke i brug"),
                "an unused line says so instead of trailing three empty columns");
        });
    }
}
