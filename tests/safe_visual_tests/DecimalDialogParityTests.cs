using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Session;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-41 (final slice) and F-44: the seven DECIMAL-valued resource types get their initial-value editor.
///
/// <para>Measured 2026-08-11 by driving the reference application's own dialogs and reading the bytes it saved.
/// The family is exactly the set whose DTD default is <c>inivalue CDATA "0.00"</c> — kW, kWh, W, Wh,
/// <c>resource_floating_point</c> (Kommatal), <c>resource_humidity_level</c> (Fugtighed) and
/// <c>resource_temperature</c> — and it is NOT the set the dialog's appearance suggests: <b>W and Wh show a whole
/// number on screen yet serialise through the same decimal writer</b> (typing <c>42,7</c> gave a row reading
/// <c>43W</c> and saved bytes reading <c>inivalue="43.00"</c>). That is F-44, and it corrects turn 32, which put
/// the two in the integer kind on the strength of the field's appearance alone.</para>
///
/// <para>The genuinely integer types — Tal, Tæller, Lys, Lysniveau — declare <c>"0"</c> and were confirmed at byte
/// level in the same session (<c>inivalue="17"</c>, <c>"42"</c>), so turn 32's other half stands.</para>
///
/// <para>The editor's precision is per type and was measured field by field: kW/kWh <c>0,000</c>, Kommatal
/// <c>0,00</c>, Fugtighed/Temperatur <c>0,0</c>, W/Wh <c>0</c>. It happens to equal each type's ROW precision, but
/// the two were measured separately and are kept separate — the unit, for one, appears only in the row.</para>
/// </summary>
public class DecimalDialogParityTests : AvaloniaTestBase
{
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, ElementId variable)> WithVariableAsync(string tag)
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.EnterProgrammingModeOnNewBlockAsync();
        ElementId section = vm.InstallationNodes[0].Children
            .Single(n => n.NodeKind == "section:internalsettings").ElementId!.Value;
        ElementId variable = (await harness.Session.AddVariableAsync(section, tag, "Måler"))!.Value;
        return (harness, vm, variable);
    }

    [TestCase("kW")]
    [TestCase("kWh")]
    [TestCase("W")]
    [TestCase("Wh")]
    [TestCase("resource_floating_point")]
    [TestCase("resource_humidity_level")]
    [TestCase("resource_temperature")]
    public async Task Dialog_OffersADecimalInitialValue(string tag)
    {
        var (harness, vm, _) = await WithVariableAsync(tag);
        using var _1 = harness;

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Måler")!);

        Assert.That(harness.Dialogs.LastVariablePropertiesInput?.Current.Kind,
            Is.EqualTo(ResourceValueKind.Decimal),
            $"{tag}: the reference application stores this type as a two-decimal number");
    }

    /// <summary>The stored text is period-separated, so the read side must parse it invariantly — a Danish machine
    /// reading <c>-12.50</c> as a culture-formatted number would see either nothing or -1250.</summary>
    [TestCase("resource_temperature", -12.5)]
    [TestCase("resource_humidity_level", 55.5)]
    [TestCase("kW", 1.55)]
    [TestCase("W", 43)]
    public async Task AStoredValue_ReadsBack(string tag, double value)
    {
        var (harness, vm, variable) = await WithVariableAsync(tag);
        using var _1 = harness;
        await harness.Session.ApplyAsync(new SetResourceInitialValue(variable, ResourceInitialValue.OfDecimal(value)));

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Måler")!);

        Assert.That(harness.Dialogs.LastVariablePropertiesInput?.Current.Decimal, Is.EqualTo(value).Within(0.0001),
            $"{tag}: the period-separated text the writer stored must parse back invariantly");
    }

    /// <summary>An ABSENT inivalue is the DTD default 0.00, not "no value" — the omit-if-default rule makes an
    /// unedited variable's normal on-disk state.</summary>
    [Test]
    public async Task AnAbsentValue_ReadsAsZero()
    {
        var (harness, vm, _) = await WithVariableAsync("kW");
        using var _1 = harness;

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Måler")!);

        Assert.That(harness.Dialogs.LastVariablePropertiesInput?.Current.Decimal, Is.EqualTo(0));
    }

    [TestCase("resource_temperature", 21.5, "21.50")]
    [TestCase("kWh", 2.25, "2.25")]
    [TestCase("Wh", 7, "7.00")]
    public async Task CommittingAValue_PersistsIt(string tag, double value, string expected)
    {
        var (harness, vm, variable) = await WithVariableAsync(tag);
        using var _1 = harness;
        harness.Dialogs.VariablePropertiesResult = new VariablePropertiesResult(
            "Måler", string.Empty, ResourceInitialValue.OfDecimal(value), string.Empty);

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Måler")!);

        Assert.That(harness.Session.Current!.FindById(variable)!.GetAttribute("inivalue"), Is.EqualTo(expected));
    }

    /// <summary>The row must FOLLOW the value — the F-43 lesson, applied per type as each is added. Every
    /// expectation below is the reference application's own row, read after committing that value.</summary>
    [TestCase("kW", 1.55, "1,550kW")]
    [TestCase("kWh", 2.25, "2,250kWh")]
    [TestCase("W", 43, "43W")]
    [TestCase("Wh", 7, "7Wh")]
    [TestCase("resource_floating_point", 3.75, "3,75")]
    [TestCase("resource_humidity_level", 55.5, "55,5% RH")]
    [TestCase("resource_temperature", -12.5, "-12,5 °C")]
    public async Task TheTreeRowFollowsTheStoredValue(string tag, double value, string expected)
    {
        var (harness, vm, variable) = await WithVariableAsync(tag);
        using var _1 = harness;

        await harness.Session.ApplyAsync(new SetResourceInitialValue(variable, ResourceInitialValue.OfDecimal(value)));

        Assert.That(TreeNodes.FindPin(vm.InstallationNodes, "Måler")!.DisplayName, Does.Contain(expected),
            $"{tag}: the row renders the stored value at its own precision, with its own unit");
    }

    /// <summary>The editor shows the value at the type's own precision, with the Danish comma the reference
    /// application's field uses.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void TheEditorShowsTheValueAtTheTypesPrecision()
    {
        // One test rather than four cases: [AvaloniaTest] builds a single test and does not compose with
        // [TestCase], and each case here needs its own window on the same UI thread anyway.
        (int Places, double Value, string Shown)[] cases =
        [
            (3, 1.5, "1,500"),      // kW, kWh
            (2, 3.75, "3,75"),      // Kommatal
            (1, -12.5, "-12,5"),    // Fugtighed, Temperatur
            (0, 43, "43"),          // W, Wh
        ];

        Assert.Multiple(() =>
        {
            foreach ((int places, double value, string shown) in cases)
            {
                var window = new VariablePropertiesWindow();
                CurrentTestWindow = window;
                window.Populate(new VariablePropertiesInput("Rediger Måler egenskaber", "Måler", "",
                    ResourceInitialValue.OfDecimal(value), DecimalPlaces: places));

                Assert.That(window.FindControl<StackPanel>("DecimalPanel")!.IsVisible, Is.True);
                Assert.That(window.FindControl<TextBox>("DecimalBox")!.Text, Is.EqualTo(shown),
                    $"a field of {places} decimals showing {value}");
            }
        });
    }

    /// <summary>A typed value is rounded to the field's own precision on commit, which is how the reference
    /// application turned <c>42,7</c> in the W field into a row reading <c>43W</c> and bytes reading
    /// <c>43.00</c>. Without it a W would keep a fraction the type never shows.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void AWholeNumberField_RoundsWhatIsTyped()
    {
        var window = new VariablePropertiesWindow();
        CurrentTestWindow = window;
        window.Populate(new VariablePropertiesInput("Rediger W egenskaber", "W", "",
            ResourceInitialValue.OfDecimal(0), DecimalPlaces: 0));

        window.FindControl<TextBox>("DecimalBox")!.Text = "42,7";

        Assert.That(window.ResultForTest().Decimal, Is.EqualTo(43));
    }

    /// <summary>The field is read in DANISH, matching what it displays: a comma is the decimal separator, and a
    /// period must not silently multiply the value by a hundred.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void TheFieldIsReadWithTheDanishSeparator()
    {
        var window = new VariablePropertiesWindow();
        CurrentTestWindow = window;
        window.Populate(new VariablePropertiesInput("Rediger Temperatur egenskaber", "Temperatur", "",
            ResourceInitialValue.OfDecimal(0), DecimalPlaces: 1));

        window.FindControl<TextBox>("DecimalBox")!.Text = "-12,5";

        Assert.That(window.ResultForTest().Decimal, Is.EqualTo(-12.5).Within(0.0001));
    }
}
