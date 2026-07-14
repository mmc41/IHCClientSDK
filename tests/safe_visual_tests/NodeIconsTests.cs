using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;

namespace safe_visual_tests;

/// <summary>US-046: the icon language — every node category is rendered with a distinct, consistent glyph so a
/// node's type is identifiable without reading its label.</summary>
public class NodeIconsTests
{
    [Test]
    public void EveryVariableType_HasADistinctIcon()
    {
        string[] variableTags =
        {
            "resource_flag", "resource_date", "resource_weekday", "resource_time", "resource_counter",
            "resource_integer", "resource_floating_point", "resource_timer", "resource_timertime",
            "resource_enum", "resource_light", "resource_light_level", "resource_temperature",
            "resource_holiday", "resource_humidity_level",
        };

        var icons = variableTags.Select(t => NodeIcons.For(t, null)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(icons.Distinct().Count(), Is.EqualTo(variableTags.Length), "each variable type maps to its own glyph");
            Assert.That(icons, Has.None.EqualTo(NodeIcons.Locality), "no variable type falls back to the neutral glyph");
            // The gaps closed this turn:
            Assert.That(NodeIcons.For("resource_humidity_level", null), Is.EqualTo("/Assets/var-humidity.svg"));
            Assert.That(NodeIcons.For("resource_light_level", null), Is.EqualTo("/Assets/var-light-level.svg"));
        });
    }

    [Test]
    public void ProgramElements_PinsAndLogicOperators_AreDistinct()
    {
        var byCategory = new Dictionary<string, string>
        {
            ["program"] = NodeIcons.For("program_simple", null),
            ["subprogram"] = NodeIcons.For("program_sub", null),
            ["eventGroup"] = NodeIcons.For("events", null),
            ["event"] = NodeIcons.For("event", null),
            ["conditionsAnd"] = NodeIcons.For("conditions", null),
            ["conditionsOr"] = NodeIcons.For("conditions-or", null),
            ["condition"] = NodeIcons.For("condition", null),
            ["commandGroup"] = NodeIcons.For("actions", null),
            ["command"] = NodeIcons.For("action", null),
            ["pinIn"] = NodeIcons.For("resource_input", null),
            ["pinOut"] = NodeIcons.For("resource_output", null),
            ["scenario"] = NodeIcons.For("resource_scene", null),
        };

        Assert.Multiple(() =>
        {
            Assert.That(byCategory.Values.Distinct().Count(), Is.EqualTo(byCategory.Count), "each program/pin/scenario category is distinct");
            Assert.That(byCategory["conditionsAnd"], Is.Not.EqualTo(byCategory["conditionsOr"]), "AND and OR logic groups differ");
            Assert.That(byCategory["pinIn"], Is.Not.EqualTo(byCategory["pinOut"]), "input and output pins differ");
            Assert.That(byCategory.Values, Has.None.EqualTo(NodeIcons.Locality), "no program element falls back to the neutral glyph");
        });
    }

    // US-046: the library function block and the editable function block render with two different icons.
    [Test]
    public async Task LibraryBlock_AndEditableBlock_ShowDifferentIcons()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var library = harness.Session.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        await harness.Session.AddFunctionBlockAsync(loc, library.MasterType);   // a locked library block
        await harness.Session.AddEmptyFunctionBlockAsync(loc);                   // an editable block

        var blocks = vm.FunctionNodes[0].Children[0].Children.ToList();
        var libraryIcon = blocks[0].IconAsset;
        var editableIcon = blocks[1].IconAsset;

        Assert.Multiple(() =>
        {
            Assert.That(libraryIcon, Is.EqualTo("/Assets/fb-lk.svg"), "the library block shows the library badge icon");
            Assert.That(editableIcon, Is.EqualTo("/Assets/fb-editable.svg"), "the editable block shows the editable icon");
            Assert.That(libraryIcon, Is.Not.EqualTo(editableIcon));
        });
    }
}
