using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis.Addressing;

namespace safe_visual_tests;

/// <summary>
/// The terminal editor's own field keys: a route says WHICH value it is about, and the window decides which of
/// its controls that is.
///
/// <para>The vocabularies stay apart on purpose. The SDK knows attribute names, the window knows controls, and
/// the coordinator is the single place they meet — so no control identity ever travels through an SDK contract,
/// and no attribute name is spelled inside a view.</para>
/// </summary>
public class PinDialogFocusKeyTests : AvaloniaTestBase
{
    private static PinPropertiesInput Input(PinDialogField? focus, bool isOutput = true) =>
        new("Udgang 'Tryk'", isOutput, DataLine: 1, Terminal: 2, CableColour: "Grøn", Note: "note",
            InitialValueOn: false, InUseTerminals: new List<DatalineAddress>(), Name: "Tryk",
            SaveOnPowerFailure: false, Focus: focus);

    /// <summary>Populates a window with this input and shows it, so the on-open focus has run.</summary>
    private static PinPropertiesWindow Opened(PinPropertiesInput input)
    {
        PinPropertiesWindow window = new();
        window.Populate(input);
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Control Named(Window window, string name) =>
        window.FindControl<Control>(name)!;

    /// <summary>Every key lands on its own control — one case per key, so a map entry cannot be silently absent.</summary>
    [AvaloniaTest]
    public void EveryFieldKeyFocusesItsOwnControl()
    {
        Dictionary<PinDialogField, string> expected = new()
        {
            [PinDialogField.Address] = "DataLineList",
            [PinDialogField.CableColour] = "CableColourBox",
            [PinDialogField.Note] = "NoteBox",
            [PinDialogField.InitialValue] = "InitialValueCombo",
            [PinDialogField.Backup] = "SaveValueCheck",
        };

        Assert.Multiple(() =>
        {
            Assert.That(expected.Keys, Is.EquivalentTo(System.Enum.GetValues<PinDialogField>()),
                "every declared key is exercised — one added without a map entry would slip through otherwise");

            foreach ((PinDialogField field, string control) in expected)
            {
                PinPropertiesWindow window = Opened(Input(field));
                Assert.That(Named(window, control).IsFocused, Is.True, field.ToString());
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }

    /// <summary>
    /// An INPUT has no initial-value or power-failure control — those panels are hidden — so a route asking for
    /// one lands nowhere rather than on a control the installer cannot see.
    /// </summary>
    [AvaloniaTest]
    public void AKeyWhoseControlIsHiddenOnThisPinFocusesNothing()
    {
        PinPropertiesWindow window = Opened(Input(PinDialogField.InitialValue, isOutput: false));

        Assert.Multiple(() =>
        {
            Assert.That(Named(window, "InitialValuePanel").IsVisible, Is.False,
                "precondition: an input really does hide it");
            Assert.That(Named(window, "InitialValueCombo").IsFocused, Is.False);
        });

        window.Close();
    }

    /// <summary>No key asked for, nothing focused — the dialog opens as it always did.</summary>
    [AvaloniaTest]
    public void WithNoKeyTheDialogFocusesNoField()
    {
        PinPropertiesWindow window = Opened(Input(null));

        Assert.That(
            new[] { "DataLineList", "CableColourBox", "NoteBox", "InitialValueCombo", "SaveValueCheck" }
                .Select(n => Named(window, n).IsFocused),
            Has.None.True);

        window.Close();
    }

    /// <summary>
    /// The coordinator's translation: an SDK attribute name becomes a key, and an attribute this dialog does not
    /// render becomes NO key rather than a guess at a nearby field.
    /// </summary>
    [Test]
    public void TheCoordinatorTranslatesAttributeNamesIntoKeys()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PropertiesDialogCoordinator.PinFieldFor("address_dataline"),
                Is.EqualTo(PinDialogField.Address));
            Assert.That(PropertiesDialogCoordinator.PinFieldFor("cable_colour"),
                Is.EqualTo(PinDialogField.CableColour));
            Assert.That(PropertiesDialogCoordinator.PinFieldFor("note"), Is.EqualTo(PinDialogField.Note));
            Assert.That(PropertiesDialogCoordinator.PinFieldFor("inivalue"),
                Is.EqualTo(PinDialogField.InitialValue));
            Assert.That(PropertiesDialogCoordinator.PinFieldFor("backup"), Is.EqualTo(PinDialogField.Backup));

            Assert.That(PropertiesDialogCoordinator.PinFieldFor("product_identifier"), Is.Null,
                "an attribute this dialog does not render has no key, and inventing one would focus the "
                + "wrong field with every appearance of working");
            Assert.That(PropertiesDialogCoordinator.PinFieldFor(null), Is.Null);
        });
    }
}
