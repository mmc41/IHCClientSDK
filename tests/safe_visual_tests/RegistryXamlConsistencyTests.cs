using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// crudarch T023 (proposal §3.3 review F6): the TWO-WAY registry↔XAML consistency net. Direction 1 — every
/// XAML item whose Command binds a registry-materialized command sits on a surface the row's Placement
/// includes. Direction 2 — every surface in a row's Placement has at least one bound XAML item. The checker is
/// a pure function over (xaml text, rows, bridge map), so the armed sub-asserts seed a mismatch in EACH
/// direction and prove it reports, without touching the real inputs. XAML regions are landmark-split
/// (NodeContextMenu flyout / the Menu element / the Toolbar comment block); the markup is the SUBJECT, copied
/// to the test output by the csproj link.
/// </summary>
public class RegistryXamlConsistencyTests : AvaloniaTestBase
{
    // One parameterized item command intentionally backs the three Theme radio items. It is presentation-only,
    // has no SDK edit verdict or per-surface availability policy, and therefore is not a CommandSpec row.
    private static readonly IReadOnlySet<string> ParameterizedItemCommandExceptions = new HashSet<string>
    {
        nameof(MainWindowViewModel.SetThemeCommand),
        // Same shape and same reason as SetThemeCommand: one parameterized item command backs the four
        // Tekststørrelse radio items. Presentation-only — no SDK edit verdict, no per-surface availability
        // policy — so it is not a CommandSpec row.
        nameof(MainWindowViewModel.SetTextScaleCommand),
    };

    private static readonly Regex CommandBinding = new(@"Command=""\{Binding (?<prop>[A-Za-z0-9_]+)\}""", RegexOptions.Compiled);

    [Test]
    public async Task RegistryAndXaml_AgreeInBothDirections()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        string xaml = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "appxaml", "MainWindow.axaml"));
        IReadOnlyDictionary<string, string> propToRow = BridgePropertyToRowId(vm);
        Assert.That(propToRow, Is.Not.Empty, "the bridge map must resolve (reflection over the VM's command properties)");

        var regions = SplitRegions(xaml);
        List<string> errors = Check(regions, vm.Registry.Rows, propToRow);
        Assert.That(errors, Is.Empty, "registry and XAML drifted:\n" + string.Join("\n", errors));

        // ARMED direction 1: a seeded XAML line binding a mapped command on a surface its row does NOT carry —
        // picked dynamically as any bridged row without Toolbar placement, so placement changes cannot unarm it.
        CommandSpec barOnly = vm.Registry.Rows.First(r =>
            !r.Placement.Contains(Surface.Toolbar) && propToRow.Values.Contains(r.Id));
        string bridgeOfBarOnly = propToRow.First(kv => kv.Value == barOnly.Id).Key;
        var seededRegions = new Dictionary<Surface, string>(regions)
        {
            [Surface.Toolbar] = regions[Surface.Toolbar] + $"\n<Button Command=\"{{Binding {bridgeOfBarOnly}}}\"/>",
        };
        Assert.That(Check(seededRegions, vm.Registry.Rows, propToRow), Is.Not.Empty,
            "armed(1): a bound item on an unplaced surface must be reported");

        var unknownBindingRegions = new Dictionary<Surface, string>(regions)
        {
            [Surface.Toolbar] = regions[Surface.Toolbar]
                + Environment.NewLine + """<Button Command="{Binding UnknownCommand}"/>""",
        };
        Assert.That(Check(unknownBindingRegions, vm.Registry.Rows, propToRow),
            Has.Some.Contains("UnknownCommand"),
            "armed(unknown): an unregistered XAML command binding must be reported");

        // ARMED direction 2: a seeded row whose Placement claims a surface no XAML item binds.
        var seededRows = vm.Registry.Rows.Append(new CommandSpec(
            "seeded.orphan", null, Surfaces.MenuBar,
            _ => Task.CompletedTask, _ => Ihc.Vis.Session.EditVerdict.Allow)).ToList();
        var seededMap = new Dictionary<string, string>(propToRow) { ["SeededOrphanCommand"] = "seeded.orphan" };
        Assert.That(Check(regions, seededRows, seededMap), Is.Not.Empty,
            "armed(2): a placed row with no bound XAML item must be reported");
    }

    // D06: every <Window.KeyBinding> must bind the BAR-gated Registry.GestureCommands[<row id>]. Binding a
    // CutCommand-style bridge instead binds the row's GATE, which would let the shortcut run what the menu bar
    // refuses — and nothing downstream can undo that, because Avalonia services a TopLevel's KeyBindings before
    // any instance KeyDown handler, tunnel included. (Delete/F2/F4 are not KeyBindings; the trees' own handler
    // services them, and routes them through the same GestureCommands.)
    [Test]
    public async Task KeyBindings_AllBindTheBarGatedGestureCommands()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        string xaml = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "appxaml", "MainWindow.axaml"));
        var rowIds = vm.Registry.Rows.Select(r => r.Id).ToHashSet();

        int start = xaml.IndexOf("<Window.KeyBindings>", StringComparison.Ordinal);
        int end = xaml.IndexOf("</Window.KeyBindings>", start + 1, StringComparison.Ordinal);
        Assert.That(end, Is.GreaterThan(start), "the KeyBindings block landmark moved — this check needs updating");
        string region = xaml.Substring(start, end - start);

        Assert.That(AnyCommandBinding.Matches(region), Is.Not.Empty, "precondition: the block binds commands");
        Assert.That(BridgeBound(region, rowIds), Is.Empty, "KeyBindings bypassing the bar gate:\n" + string.Join("\n", BridgeBound(region, rowIds)));

        // ARMED: a seeded KeyBinding on a bridge property must be reported.
        Assert.That(BridgeBound(region + "\n<KeyBinding Gesture=\"Ctrl+Q\" Command=\"{Binding CutCommand}\"/>", rowIds),
            Is.Not.Empty, "armed: a bridge-bound KeyBinding must be reported");
    }

    // Unlike CommandBinding (which reads the menus' simple bridge-property names), a KeyBinding's path is an
    // indexer expression, so this one accepts any binding body.
    private static readonly Regex AnyCommandBinding =
        new(@"Command=""\{Binding (?<path>[^}]+)\}""", RegexOptions.Compiled);

    private static readonly Regex GestureCommandBinding =
        new(@"^Registry\.GestureCommands\[(?<id>[^\]]+)\]$", RegexOptions.Compiled);

    // The pure check: which of the region's Command bindings are NOT a bar-gated GestureCommands[<known row>].
    private static List<string> BridgeBound(string region, IReadOnlySet<string> rowIds) =>
        AnyCommandBinding.Matches(region)
            .Select(m => m.Groups["path"].Value)
            .Where(path => GestureCommandBinding.Match(path) is not { Success: true } hit
                           || !rowIds.Contains(hit.Groups["id"].Value))
            .ToList();

    // Reflects the VM's public command-bridge properties and matches each VALUE (reference-identical) back to
    // its registry id — so the map cannot drift from the real bridges.
    private static IReadOnlyDictionary<string, string> BridgePropertyToRowId(MainWindowViewModel vm)
    {
        var byCommand = vm.Registry.Commands.ToDictionary(kv => (object)kv.Value, kv => kv.Key);
        return vm.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => typeof(IRelayCommand).IsAssignableFrom(p.PropertyType) && p.GetIndexParameters().Length == 0)
            .Select(p => (p.Name, Value: p.GetValue(vm)))
            .Where(x => x.Value is not null && byCommand.ContainsKey(x.Value))
            .ToDictionary(x => x.Name, x => byCommand[x.Value!]);
    }

    private static Dictionary<Surface, string> SplitRegions(string xaml)
    {
        string Between(string startMark, string endMark, string label)
        {
            int start = xaml.IndexOf(startMark, StringComparison.Ordinal);
            int end = xaml.IndexOf(endMark, start + 1, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"landmark '{startMark}' not found — the {label} region split needs updating");
            Assert.That(end, Is.GreaterThan(start), $"landmark '{endMark}' not found — the {label} region split needs updating");
            return xaml.Substring(start, end - start);
        }
        return new Dictionary<Surface, string>
        {
            [Surface.ContextMenu] = Between("x:Key=\"NodeContextMenu\"", "</MenuFlyout>", "context-menu"),
            [Surface.MenuBar] = Between("<controls:AccessibleMenu ", "</controls:AccessibleMenu>", "menu-bar"),
            [Surface.Toolbar] = Between("============ Toolbar", "============ Status bar", "toolbar"),
        };
    }

    // The pure two-way check: (surface regions, rows, bridge-property map) -> human-readable drift errors.
    private static List<string> Check(IReadOnlyDictionary<Surface, string> regions,
        IEnumerable<CommandSpec> rows, IReadOnlyDictionary<string, string> propToRow)
    {
        var rowById = rows.ToDictionary(r => r.Id);
        var propertiesPerSurface = regions.ToDictionary(
            kv => kv.Key,
            kv => CommandBinding.Matches(kv.Value)
                .Select(match => match.Groups["prop"].Value)
                .ToHashSet());
        var boundPerSurface = regions.ToDictionary(
            kv => kv.Key,
            kv => propertiesPerSurface[kv.Key]
                .Where(propToRow.ContainsKey)
                .Select(p => propToRow[p])
                .ToHashSet());

        var errors = new List<string>();
        foreach ((Surface surface, HashSet<string> properties) in propertiesPerSurface)
        {
            foreach (string property in properties.Where(property =>
                         !propToRow.ContainsKey(property)
                         && !ParameterizedItemCommandExceptions.Contains(property)))
                errors.Add($"XAML binds unknown command property '{property}' on {surface}; register it or document a narrow exception");
        }
        var allBoundProperties = propertiesPerSurface.Values.SelectMany(properties => properties).ToHashSet();
        foreach (string exception in ParameterizedItemCommandExceptions.Where(exception => !allBoundProperties.Contains(exception)))
            errors.Add($"parameterized-item exception '{exception}' is stale because XAML no longer binds it");

        foreach ((Surface surface, HashSet<string> ids) in boundPerSurface)
        {
            foreach (string id in ids.Where(id => !rowById[id].Placement.Contains(surface)))
            {
                errors.Add($"XAML binds '{id}' on {surface}, but the row's Placement does not include it");
            }
        }
        foreach (CommandSpec row in rowById.Values)
        {
            foreach (Surface surface in new[] { Surface.MenuBar, Surface.ContextMenu, Surface.Toolbar })
            {
                if (row.Placement.Contains(surface) && !boundPerSurface[surface].Contains(row.Id))
                {
                    errors.Add($"row '{row.Id}' claims {surface} in its Placement, but no XAML item there binds it");
                }
            }
        }
        return errors;
    }
}
