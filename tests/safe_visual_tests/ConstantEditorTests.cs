using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace safe_visual_tests;

/// <summary>
/// T040 — <i>Rediger konstant</i>, the editor behind a row of the sensors' <i>Indstillinger</i> grid.
///
/// <para>Measured on build 3.4.72.3: the vendor's window holds twelve controls of which two are
/// visible — one enabled edit box carrying the value, and <b>OK with no Annuller</b> — and it is reached two ways,
/// by double-clicking the row and by right-clicking it and choosing <i>Egenskaber</i>. Both halves are asserted
/// here, the missing Annuller included, because a cancel button would be an invention and a second route that
/// opened something else would be two features wearing one name.</para>
/// </summary>
public class ConstantEditorTests : AvaloniaTestBase
{
    private const string TemperatureSensor = "_0x2124";

    private static (ProductDialogViewModel Dialog, IReadOnlyList<ProductSetting> Settings) DialogWithSettings()
    {
        var app = new ProjectAppService(new IhcSettings());
        Project project = app.CreateNew(new ProjectDetails("P", "I", "DK"));
        ElementId locality = project.Groups.First().Id!.Value;
        var session = new ProjectDocumentSession();
        session.Open(project);
        ElementId placed = session.Apply(new AddProduct(locality,
            app.GetAvailableProducts().First(p => p.ProductIdentifier == TemperatureSensor))).Value;

        Project current = session.Current!;
        ProjectElement element = current.FindById(placed)!;
        IReadOnlyList<ProductSetting> settings = [.. PropertiesDialogCoordinator.BuildSettings(
            current, new ProductView(current, element))];
        return (new ProductDialogViewModel(app.GetProductDialog(current, placed), terminals: null,
            settings: settings), settings);
    }

    private static (ProductDialogWindow Window, List<ProductDialogWidgetAction> Stepped, ListBox List)
        OpenWithSettings(ProductDialogViewModel dialog)
    {
        var window = new ProductDialogWindow();
        List<ProductDialogWidgetAction> stepped = [];
        window.Populate(dialog, options: null, onStep: action =>
        {
            stepped.Add(action);
            return Task.FromResult<ProductDialogRefresh?>(null);
        });
        window.Show();
        Dispatcher.UIThread.RunJobs();
        ListBox list = window.GetVisualDescendants().OfType<ListBox>().First(l => l.Name == "SettingsList");
        return (window, stepped, list);
    }

    /// <summary>The row's container, for a gesture aimed at a real place on screen.</summary>
    private static Control RowContainer(ListBox list, int index) =>
        (Control)list.ContainerFromIndex(index)!;

    // Through the headless input pipeline, not a synthetic RaiseEvent: DoubleTapped is SYNTHESISED from the
    // pointer stream, so a hand-built event would test a path the installer's mouse never takes — and the same
    // press is what selects the row, which is half of what this route relies on.
    private static void DoubleClick(Window window, Visual target)
    {
        Point centre = target.TranslatePoint(
            new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        for (int i = 0; i < 2; i++)
        {
            window.MouseDown(centre, MouseButton.Left);
            window.MouseUp(centre, MouseButton.Left);
        }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void TheEditor_PreFillsTheValueAndOffersNoCancel()
    {
        ConstantEditorWindow window = ConstantEditorWindow.Create(
            new ConstantEditorInput(new ElementId(1, 0), "Kalibrering af rumføler", "0,0 °C"));
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var boxes = window.GetVisualDescendants().OfType<TextBox>().ToList();
        var buttons = window.GetVisualDescendants().OfType<Button>()
            .Where(b => b is not RepeatButton && b.IsVisible).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(window.Title, Is.EqualTo("Rediger konstant"), "the vendor's caption");
            Assert.That(boxes, Has.Count.EqualTo(1), "ONE value field");
            Assert.That(boxes[0].Text, Is.EqualTo("0,0 °C"), "pre-filled with what the grid shows");
            Assert.That(boxes[0].IsEnabled, Is.True, "and enabled — it is the thing being edited");
            Assert.That(buttons.Select(b => b.Content?.ToString()), Is.EqualTo(new[] { "OK" }).AsCollection,
                "OK, and no Annuller: the vendor's window has none, and inventing one would be a second "
                + "way out that the original does not offer");
            Assert.That(buttons.Any(b => b.IsCancel), Is.False,
                "nor a hidden one — an IsCancel button is an Annuller without a caption");
        });
    }

    /// <summary>OK returns the text as typed. The window does not interpret it; the writing command does.</summary>
    [AvaloniaTest]
    public void TheEditor_ReturnsWhatWasTyped()
    {
        ConstantEditorWindow window = ConstantEditorWindow.Create(
            new ConstantEditorInput(new ElementId(1, 0), "Kalibrering", "0,0"));
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        TextBox box = window.GetVisualDescendants().OfType<TextBox>().Single();
        box.Text = "-1,5";
        window.GetVisualDescendants().OfType<Button>().First(b => b.Content?.ToString() == "OK")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.That(window.AcceptedResult, Is.EqualTo("-1,5"),
            "verbatim — a negative offset is a value the command rules on, not a shape this window judges");
    }

    /// <summary>Route one: double-clicking the row steps into the editor for THAT setting.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void DoubleClickingASettingsRow_StepsIntoTheEditorForThatSetting()
    {
        (ProductDialogViewModel dialog, IReadOnlyList<ProductSetting> settings) = DialogWithSettings();
        (ProductDialogWindow window, List<ProductDialogWidgetAction> stepped, ListBox list) =
            OpenWithSettings(dialog);
        CurrentTestWindow = window;

        DoubleClick(window, RowContainer(list, 1));

        Assert.Multiple(() =>
        {
            Assert.That(stepped, Has.Count.EqualTo(1), "one gesture, one step");
            Assert.That(stepped[0].Kind, Is.EqualTo(DialogWidgetKind.SettingsGrid));
            Assert.That(stepped[0].Target, Is.EqualTo(settings[1].Id),
                "the row that was clicked, not the first one");
        });
    }

    /// <summary>
    /// Route two: right-click, then <i>Egenskaber</i> — the SAME step. The right-click must also move the
    /// selection, or the menu item would edit whichever row happened to be selected before.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void EgenskaberOnASettingsRow_StepsIntoTheSameEditor()
    {
        (ProductDialogViewModel dialog, IReadOnlyList<ProductSetting> settings) = DialogWithSettings();
        (ProductDialogWindow window, List<ProductDialogWidgetAction> stepped, ListBox list) =
            OpenWithSettings(dialog);
        CurrentTestWindow = window;
        ProductDialogSettingsGridViewModel grid = dialog.Groups.SelectMany(g => g.SettingsSection).Single();
        grid.SelectedRow = settings[0];   // a stale selection the right-click has to displace

        Control row = RowContainer(list, 1);
        row.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();

        Assert.That(grid.SelectedRow?.Id, Is.EqualTo(settings[1].Id),
            "the right-click selected the row it was made on");

        var flyout = (MenuFlyout)list.ContextFlyout!;
        var egenskaber = flyout.Items.OfType<MenuItem>().Single();
        egenskaber.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(egenskaber.Header, Is.EqualTo("Egenskaber"), "the vendor's one-item popup");
            Assert.That(stepped, Has.Count.EqualTo(1));
            Assert.That(stepped[0].Kind, Is.EqualTo(DialogWidgetKind.SettingsGrid));
            Assert.That(stepped[0].Target, Is.EqualTo(settings[1].Id),
                "the same setting the double-click route reaches — one editor, two doors");
        });
    }

    /// <summary>
    /// And the step is carried out: the coordinator opens the editor on the row's CURRENT value, so the
    /// installer overtypes what they were looking at.
    /// </summary>
    [Test]
    public async Task TheVisitOpensTheEditorPreFilledWithTheRowsValue()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ProductDefinition sensor = harness.ProjectService.GetAvailableProducts()
            .First(p => p.ProductIdentifier == TemperatureSensor);
        ElementId product = (await harness.Session.AddProductAsync(
            vm.InstallationNodes[0].Children[0].ElementId!.Value, sensor.ProductIdentifier))!.Value;
        ProjectElement setting = new ProductView(harness.Session.Current!,
            harness.Session.Current!.FindById(product)!).SettingElements.First();

        harness.Dialogs.RespondWithWidget(DialogWidgetKind.SettingsGrid, setting.Id);
        harness.Dialogs.ConstantResult = null;   // read and dismissed; nothing is written

        await vm.PropertiesCommand.ExecuteAsync(
            vm.InstallationNodes[0].Children[0].Children.First(n => n.ElementId == product));

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditConstantCalls, Is.EqualTo(1), "the step opened the editor");
            Assert.That(harness.Dialogs.LastConstantInput?.Setting, Is.EqualTo(setting.Id));
            Assert.That(harness.Dialogs.LastConstantInput?.Value,
                Is.EqualTo(harness.Dialogs.LastProductDialogSettings!
                    .First(s => s.Id == setting.Id).Value),
                "pre-filled with exactly what the grid was showing for that row");
        });
    }

    /// <summary>
    /// And the accepted value REACHES the document, as part of the visit's one commit — the whole point of the
    /// two routes above. The text is turned back into a typed value on this side, so what the SDK writes is a
    /// number of the setting's own kind.
    /// </summary>
    [Test]
    public async Task AnAcceptedConstant_IsWrittenByTheVisitsSingleCommit()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ProductDefinition sensor = harness.ProjectService.GetAvailableProducts()
            .First(p => p.ProductIdentifier == TemperatureSensor);
        ElementId product = (await harness.Session.AddProductAsync(
            vm.InstallationNodes[0].Children[0].ElementId!.Value, sensor.ProductIdentifier))!.Value;
        ProjectElement setting = new ProductView(harness.Session.Current!,
            harness.Session.Current!.FindById(product)!).SettingElements.First();

        harness.Dialogs.RespondWithWidget(DialogWidgetKind.SettingsGrid, setting.Id);
        harness.Dialogs.ConstantResult = "-1,5 °C";   // typed in Danish, unit left in place

        await vm.PropertiesCommand.ExecuteAsync(
            vm.InstallationNodes[0].Children[0].Children.First(n => n.ElementId == product));

        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.Current!.FindById(setting.Id!.Value)!.GetAttribute("inivalue"),
                Is.EqualTo("-1.50"),
                "the Danish comma and the trailing unit are read back here, and the file gets the number");
            Assert.That(harness.Session.CanUndo, Is.True);
        });

        await harness.Session.UndoAsync();

        Assert.That(harness.Session.Current!.FindById(setting.Id!.Value)!.GetAttribute("inivalue"),
            Is.Null.Or.EqualTo("0.00"), "one visit, one undo entry — the constant goes back with it");
    }
}
