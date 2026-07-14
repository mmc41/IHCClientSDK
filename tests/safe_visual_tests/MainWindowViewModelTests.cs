using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Catalog;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace safe_visual_tests;

/// <summary>Shell view-model behaviour (US-001/051): the title, the two locality tree panes, and the
/// toolbar/status-bar/theme view state. Pure logic — no Avalonia UI needed.</summary>
public class MainWindowViewModelTests
{
    [Test]
    public async Task Initialize_BuildsLocalitiesRootWithTenRooms_InBothPanes()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();

        await vm.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(vm.InstallationNodes, Has.Count.EqualTo(1));
            Assert.That(vm.InstallationNodes[0].DisplayName, Is.EqualTo("Localities"));
            Assert.That(vm.InstallationNodes[0].Children, Has.Count.EqualTo(10));
            Assert.That(vm.FunctionNodes[0].Children, Has.Count.EqualTo(10));
        });
    }

    // US-006: a new project OpenVisual authors starts from English default localities (English is the product
    // language), shown identically in both panes, in the fixed vendor order, with the root expanded and the
    // room labels bold.
    [Test]
    public async Task Initialize_DefaultLocalities_AreEnglishNamesInOrder_AndBold()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        string[] expected =
        {
            "Living room", "Hall", "Kitchen", "Bedroom", "Room",
            "Bathroom", "Utility room", "Garage", "Basement", "Outdoors",
        };
        var install = vm.InstallationNodes[0];
        var functions = vm.FunctionNodes[0];

        Assert.Multiple(() =>
        {
            Assert.That(install.DisplayName, Is.EqualTo("Localities"));
            Assert.That(install.IsExpanded, Is.True, "the Localities root is expanded by default");
            Assert.That(install.Children.Select(c => c.DisplayName), Is.EqualTo(expected));
            Assert.That(functions.Children.Select(c => c.DisplayName), Is.EqualTo(expected),
                "the same ten localities appear in the Functions pane");
            Assert.That(install.Children.All(c => c.IsBold), Is.True, "locality labels render bold (US-006)");
        });
    }

    // US-007: rename a locality via the Properties dialog — reflected in both panes, confirmed in the status
    // bar, dialog titled "Edit <current name> properties" and pre-filled with the current name.
    [Test]
    public async Task Properties_RenamesLocality_InBothPanes_AndConfirms()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        string newName = "Living room & Kitchen \"open\"";
        harness.Dialogs.PropertiesResult = new PropertiesResult(newName, "a note");

        var node = vm.InstallationNodes[0].Children[0];   // "Living room"
        await vm.PropertiesCommand.ExecuteAsync(node);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastPropertiesTitle, Is.EqualTo("Edit Living room properties"));
            Assert.That(harness.Dialogs.LastPropertiesName, Is.EqualTo("Living room"));
            Assert.That(vm.InstallationNodes[0].Children[0].DisplayName, Is.EqualTo(newName));
            Assert.That(vm.FunctionNodes[0].Children[0].DisplayName, Is.EqualTo(newName),
                "the rename shows in the Functions pane too");
            Assert.That(vm.StatusText, Does.Contain(newName), "the status bar confirms the change");
            Assert.That(harness.Session.IsDirty, Is.True);
        });
    }

    [Test]
    public async Task Properties_Cancel_KeepsOriginalNameAndClean()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        harness.Dialogs.PropertiesResult = null;   // Cancel

        var node = vm.InstallationNodes[0].Children[0];
        await vm.PropertiesCommand.ExecuteAsync(node);

        Assert.Multiple(() =>
        {
            Assert.That(vm.InstallationNodes[0].Children[0].DisplayName, Is.EqualTo("Living room"));
            Assert.That(harness.Session.IsDirty, Is.False);
        });
    }

    [Test]
    public async Task Properties_RenamedNote_IsPreFilledOnReopen()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        var node = vm.InstallationNodes[0].Children[0];
        harness.Dialogs.PropertiesResult = new PropertiesResult("Kitchen", "on the ground floor");
        await vm.PropertiesCommand.ExecuteAsync(node);

        harness.Dialogs.PropertiesResult = null;   // reopen and cancel — just capture the pre-fill
        await vm.PropertiesCommand.ExecuteAsync(vm.InstallationNodes[0].Children[0]);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastPropertiesTitle, Is.EqualTo("Edit Kitchen properties"));
            Assert.That(harness.Dialogs.LastPropertiesNote, Is.EqualTo("on the ground floor"));
        });
    }

    // US-008: insert a new locality under Localities — appended last, named "Locality", selected, shown in both
    // panes, with the exact status-bar confirmation.
    [Test]
    public async Task InsertLocality_AppendsNamedLocality_InBothPanes_SelectedAndConfirmed()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        int before = vm.InstallationNodes[0].Children.Count;   // 10
        await vm.InsertLocalityCommand.ExecuteAsync(null);

        var install = vm.InstallationNodes[0].Children;
        var functions = vm.FunctionNodes[0].Children;
        Assert.Multiple(() =>
        {
            Assert.That(install, Has.Count.EqualTo(before + 1));
            Assert.That(install[^1].DisplayName, Is.EqualTo("Locality"), "the new locality is appended at the bottom");
            Assert.That(install[^1].ElementId, Is.Not.Null, "the new node is a real, addressable locality");
            Assert.That(install[^1].IsBold, Is.True);
            Assert.That(functions[^1].DisplayName, Is.EqualTo("Locality"), "it appears in the Functions pane too");
            Assert.That(vm.StatusText, Is.EqualTo("Locality was inserted under Localities"));
            Assert.That(harness.Session.IsDirty, Is.True);
            Assert.That(vm.SelectedNode?.DisplayName, Is.EqualTo("Locality"), "the new locality is selected");
        });
    }

    // The inserted locality is immediately renamable (US-007 flow over the US-008 result).
    [Test]
    public async Task InsertLocality_ThenRename_UpdatesTheNewNode()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await vm.InsertLocalityCommand.ExecuteAsync(null);

        harness.Dialogs.PropertiesResult = new PropertiesResult("Workshop", "");
        await vm.PropertiesCommand.ExecuteAsync(vm.InstallationNodes[0].Children[^1]);

        Assert.That(vm.InstallationNodes[0].Children[^1].DisplayName, Is.EqualTo("Workshop"));
    }

    // US-009: delete an empty locality — removed from both panes, no confirmation needed.
    [Test]
    public async Task Delete_EmptyLocality_RemovesFromBothPanes()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        // ConfirmResult stays false: an empty locality must delete WITHOUT asking, so a false confirm cannot block it.

        int before = vm.InstallationNodes[0].Children.Count;
        var node = vm.InstallationNodes[0].Children[0];   // "Living room", empty
        await vm.DeleteCommand.ExecuteAsync(node);

        Assert.Multiple(() =>
        {
            Assert.That(vm.InstallationNodes[0].Children, Has.Count.EqualTo(before - 1));
            Assert.That(vm.InstallationNodes[0].Children.Any(c => c.DisplayName == "Living room"), Is.False);
            Assert.That(vm.FunctionNodes[0].Children.Any(c => c.DisplayName == "Living room"), Is.False);
            Assert.That(harness.Session.IsDirty, Is.True);
        });
    }

    // US-009: deleting a locality that still holds a function block asks first; declining keeps everything.
    [Test]
    public async Task Delete_NonEmptyLocality_Declined_KeepsIt()
    {
        using var harness = await BuildHarnessWithNonEmptyLivingRoomAsync();
        var vm = harness.CreateViewModel();
        harness.Dialogs.ConfirmResult = false;   // decline the confirmation

        var node = vm.InstallationNodes[0].Children.First(c => c.DisplayName == "Living room");
        await vm.DeleteCommand.ExecuteAsync(node);

        Assert.Multiple(() =>
        {
            Assert.That(vm.InstallationNodes[0].Children.Any(c => c.DisplayName == "Living room"), Is.True,
                "declining the confirmation deletes nothing");
            Assert.That(harness.Session.IsDirty, Is.False);
        });
    }

    [Test]
    public async Task Delete_NonEmptyLocality_Confirmed_RemovesIt()
    {
        using var harness = await BuildHarnessWithNonEmptyLivingRoomAsync();
        var vm = harness.CreateViewModel();
        harness.Dialogs.ConfirmResult = true;   // accept the confirmation

        var node = vm.InstallationNodes[0].Children.First(c => c.DisplayName == "Living room");
        await vm.DeleteCommand.ExecuteAsync(node);

        Assert.That(vm.InstallationNodes[0].Children.Any(c => c.DisplayName == "Living room"), Is.False);
    }

    // Builds a session whose "Living room" holds an empty function block (via the built-in catalog — no controller),
    // so the delete-confirmation gate can be exercised without the not-yet-built product/FB insertion UI.
    private static async Task<ShellHarness> BuildHarnessWithNonEmptyLivingRoomAsync()
    {
        var harness = ShellHarness.Create();
        var service = new ProjectAppService(new IhcSettings());
        Project project = service.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));
        project = DefaultLocalities.ApplyEnglish(project);
        var catalog = new BuiltInCatalog();
        ProjectEditor editor = project.Edit();
        editor.Group("Living room").AddEmptyFunctionBlock(catalog.EmptyFunctionBlockTemplate, new DateOnly(2024, 1, 1));
        string path = harness.TempPath("nonempty.vis");
        await service.Save(editor.ToProject(), path);
        await harness.Session.OpenAsync(path);
        return harness;
    }

    // US-010: inserting a wired product under a locality nests it (with pins) in the Installation pane only.
    [Test]
    public async Task InsertProduct_UnderLocality_NestsProductWithPins_InstallationOnly()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        var product = harness.Session.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("Datalinie") && p.Resources.Count > 0);
        var localityId = vm.InstallationNodes[0].Children[0].ElementId!.Value;   // "Living room"

        var newId = await harness.Session.AddProductAsync(localityId, product.ProductIdentifier);

        var installLocality = vm.InstallationNodes[0].Children[0];
        var functionsLocality = vm.FunctionNodes[0].Children[0];
        Assert.Multiple(() =>
        {
            Assert.That(newId, Is.Not.Null);
            Assert.That(installLocality.Children, Has.Count.EqualTo(1), "the product nests under the locality");
            Assert.That(installLocality.Children[0].DisplayName, Is.EqualTo(product.DisplayName));
            Assert.That(installLocality.Children[0].Children, Is.Not.Empty, "the product exposes its pins");
            Assert.That(installLocality.IsExpanded, Is.True, "a locality with a product opens by default");
            Assert.That(functionsLocality.Children, Is.Empty, "a wired product is not shown in the Functions pane");
            Assert.That(harness.Session.IsDirty, Is.True);
        });
    }

    // US-010: the menu leaf inserts under the selected locality and confirms by product + locality name.
    [Test]
    public async Task InsertProduct_ViaMenuLeaf_TargetsSelectedLocality_AndConfirms()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        vm.SelectNode(vm.InstallationNodes[0].Children[2]);   // select "Kitchen"

        var leaf = FirstLeaf(vm.WiredProductsMenu);
        await ((IAsyncRelayCommand)leaf.Command!).ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.StatusText, Is.EqualTo($"Product '{leaf.Header}' inserted under Kitchen"));
            Assert.That(vm.InstallationNodes[0].Children[2].Children, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task InsertProduct_WithNoLocalitySelected_HintsToSelectOne()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        vm.SelectedNode = null;

        var leaf = FirstLeaf(vm.WiredProductsMenu);
        await ((IAsyncRelayCommand)leaf.Command!).ExecuteAsync(null);

        Assert.That(vm.StatusText, Does.Contain("Select a locality"));
    }

    // US-010: the shared node context menu offers "Insert product" only in the Installation pane (the Functions
    // pane hosts function blocks, not products), and only on a node that addresses a locality — not the Localities
    // root. CanInsertProduct is the gate the shared MenuFlyout binds that item's visibility to.
    [Test]
    public async Task CanInsertProduct_OnlyOnAddressableInstallationNode()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        Assert.That(vm.CanInsertProduct, Is.False, "nothing selected yet");

        vm.SelectedInstallationNode = vm.InstallationNodes[0];               // Localities root (no element id)
        Assert.That(vm.CanInsertProduct, Is.False, "the root hosts localities, not products");

        vm.SelectedInstallationNode = vm.InstallationNodes[0].Children[0];   // Living room (Installation pane)
        Assert.That(vm.CanInsertProduct, Is.True);

        vm.SelectedFunctionsNode = vm.FunctionNodes[0].Children[0];          // switch active pane to Functions
        Assert.That(vm.CanInsertProduct, Is.False, "products are not inserted through the Functions pane");

        vm.SelectedInstallationNode = vm.InstallationNodes[0].Children[2];   // back to an Installation node (Kitchen)
        Assert.That(vm.CanInsertProduct, Is.True);
    }

    // US-011: applying product documentation writes the mapped attributes on the product element.
    [Test]
    public async Task UpdateProduct_WritesDocumentationAttributes()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var product = harness.Session.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("Datalinie") && p.Resources.Count > 0);
        var localityId = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var pid = (await harness.Session.AddProductAsync(localityId, product.ProductIdentifier))!.Value;

        var ok = await harness.Session.UpdateProductAsync(pid,
            new ProductPropertiesResult("My push button", localityId.ToToken(), "hallway", "LK 4x0.5", "K7", "ID-42", "LG-3"));

        var el = harness.Session.Current!.FindById(pid)!;
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(el.GetAttribute("name"), Is.EqualTo("My push button"));
            Assert.That(el.GetAttribute("note"), Is.EqualTo("hallway"));
            Assert.That(el.GetAttribute("cabletype"), Is.EqualTo("LK 4x0.5"));
            Assert.That(el.GetAttribute("cablenumber"), Is.EqualTo("K7"));
            Assert.That(el.GetAttribute("documentation_tag"), Is.EqualTo("ID-42"));
            Assert.That(el.GetAttribute("power_group"), Is.EqualTo("LG-3"));
            Assert.That(harness.Session.IsDirty, Is.True);
        });
    }

    // US-011: setting Location to another locality re-parents the product (ids preserved).
    [Test]
    public async Task UpdateProduct_LocationChange_ReParentsProduct()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var product = harness.Session.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("Datalinie") && p.Resources.Count > 0);
        var livingRoomId = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var kitchenId = vm.InstallationNodes[0].Children[2].ElementId!.Value;
        var pid = (await harness.Session.AddProductAsync(livingRoomId, product.ProductIdentifier))!.Value;

        await harness.Session.UpdateProductAsync(pid,
            new ProductPropertiesResult(product.DisplayName, kitchenId.ToToken(), "", "", "", "", ""));

        Assert.Multiple(() =>
        {
            Assert.That(vm.InstallationNodes[0].Children[0].Children, Is.Empty, "product left Living room");
            Assert.That(vm.InstallationNodes[0].Children[2].Children.Select(c => c.ElementId),
                Does.Contain((Ihc.Vis.Model.ElementId?)pid), "product now under Kitchen");
        });
    }

    // US-011: inserting a product opens its properties dialog automatically, pre-filled.
    [Test]
    public async Task InsertProduct_AutoOpensProductProperties_PreFilled()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        vm.SelectNode(vm.InstallationNodes[0].Children[0]);   // "Living room"
        harness.Dialogs.ProductPropertiesResult = null;       // dismiss the auto-opened dialog

        var leaf = FirstLeaf(vm.WiredProductsMenu);
        await ((IAsyncRelayCommand)leaf.Command!).ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditProductPropertiesCalls, Is.EqualTo(1), "the dialog opens automatically on insert");
            Assert.That(harness.Dialogs.LastProductPropertiesInput!.Title, Is.EqualTo("Product properties"));
            Assert.That(harness.Dialogs.LastProductPropertiesInput!.Name, Is.EqualTo(leaf.Header), "Name is pre-filled");
            Assert.That(harness.Dialogs.LastProductPropertiesInput!.Localities.Select(l => l.Name),
                Does.Contain("Living room"), "Location offers the localities");
        });
    }

    // US-011: reopening via the Properties route on a product node edits its documentation (not the locality dialog).
    [Test]
    public async Task ProductNode_Properties_OpensProductDialog_AndApplies()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var product = harness.Session.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("Datalinie") && p.Resources.Count > 0);
        await harness.Session.AddProductAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value, product.ProductIdentifier);
        var productNode = vm.InstallationNodes[0].Children[0].Children[0];

        harness.Dialogs.ProductPropertiesResponder =
            i => new ProductPropertiesResult("Renamed button", i.CurrentLocalityId, "", "", "", "", "");
        await vm.PropertiesCommand.ExecuteAsync(productNode);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditProductPropertiesCalls, Is.EqualTo(1), "the product dialog opened");
            Assert.That(harness.Dialogs.EditPropertiesCalls, Is.EqualTo(0), "not the locality rename dialog");
            Assert.That(vm.InstallationNodes[0].Children[0].Children[0].DisplayName, Is.EqualTo("Renamed button"));
        });
    }

    // US-012: addressing an input pin encodes address_dataline and writes cable colour + note.
    [Test]
    public async Task UpdatePin_Input_EncodesAddress_AndWritesCableAndNote()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var product = harness.Session.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("Datalinie") && p.Resources.Count > 0);
        await harness.Session.AddProductAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value, product.ProductIdentifier);
        var pinId = vm.InstallationNodes[0].Children[0].Children[0].Children[0].ElementId!.Value;   // product's first pin

        var ok = await harness.Session.UpdatePinAsync(pinId, new PinPropertiesResult(2, 3, "brown", "left button", false));

        var el = harness.Session.Current!.FindById(pinId)!;
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(el.GetAttribute("address_dataline"), Is.EqualTo("_0x13"), "(2-1)*16 + 3 = 0x13");
            Assert.That(el.GetAttribute("cable_colour"), Is.EqualTo("brown"));
            Assert.That(el.GetAttribute("note"), Is.EqualTo("left button"));
            Assert.That(harness.Session.IsDirty, Is.True);
        });
    }

    // US-012: an output's Initial value is stored as inivalue (on = normally-closed), and outputs use 8 terminals/line.
    [Test]
    public async Task UpdatePin_Output_SetsInitialValue_AndEncodesWith8PerLine()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var product = harness.Session.GetAvailableProducts().First(p => p.CategoryPath.Contains("02#Output"));
        var pid = (await harness.Session.AddProductAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value, product.ProductIdentifier))!.Value;
        var outputPin = harness.Session.Current!.FindById(pid)!.ChildrenOrEmpty().First(c => c.Tag == "dataline_output");

        await harness.Session.UpdatePinAsync(outputPin.Id!.Value, new PinPropertiesResult(1, 2, "", "", true));

        var el = harness.Session.Current!.FindById(outputPin.Id!.Value)!;
        Assert.Multiple(() =>
        {
            Assert.That(el.GetAttribute("inivalue"), Is.EqualTo("on"), "ON = normally-closed");
            Assert.That(el.GetAttribute("address_dataline"), Is.EqualTo("_0x2"), "(1-1)*8 + 2 = 0x2");
        });
    }

    // US-012: right-click a pin > Properties opens the addressing dialog (not the product/locality one), and it
    // reports terminals already in use.
    [Test]
    public async Task PinNode_Properties_OpensPinDialog_WithInUseTerminals()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var product = harness.Session.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("Datalinie") && p.Resources.Count > 1);
        await harness.Session.AddProductAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value, product.ProductIdentifier);

        // Address the first pin to 1.1.
        harness.Dialogs.PinPropertiesResult = new PinPropertiesResult(1, 1, "", "", false);
        await vm.PropertiesCommand.ExecuteAsync(vm.InstallationNodes[0].Children[0].Children[0].Children[0]);

        // Open the second pin: its dialog should list 1.1 as in use.
        harness.Dialogs.PinPropertiesResult = null;
        await vm.PropertiesCommand.ExecuteAsync(vm.InstallationNodes[0].Children[0].Children[0].Children[1]);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditPinPropertiesCalls, Is.EqualTo(2));
            Assert.That(harness.Dialogs.EditProductPropertiesCalls, Is.EqualTo(0), "not the product dialog");
            Assert.That(harness.Dialogs.EditPropertiesCalls, Is.EqualTo(0), "not the locality dialog");
            Assert.That(harness.Dialogs.LastPinPropertiesInput!.IsOutput, Is.False);
            Assert.That(harness.Dialogs.LastPinPropertiesInput!.InUseTerminals, Does.Contain("1.1"));
        });
    }

    // US-013: a project may contain at most one modem; the second insertion is blocked.
    [Test]
    public async Task Modem_AtMostOneAllowed()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var modem = harness.Session.GetAvailableProducts().First(p => ProductKinds.IsModem(p.Body.Tag));
        var loc0 = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var loc1 = vm.InstallationNodes[0].Children[1].ElementId!.Value;

        var first = await harness.Session.AddProductAsync(loc0, modem.ProductIdentifier);
        var second = await harness.Session.AddProductAsync(loc1, modem.ProductIdentifier);

        int modemCount = harness.Session.Current!.Root.DescendantsAndSelf().Count(e => ProductKinds.IsModem(e.Tag));
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.Null, "the first modem inserts");
            Assert.That(second, Is.Null, "the second modem is blocked");
            Assert.That(modemCount, Is.EqualTo(1));
            Assert.That(harness.Dialogs.LastMessage, Does.Contain("one modem"));
        });
    }

    // US-013: editing modem properties writes documentation, the four cabling colours, the PIN and a phone number.
    [Test]
    public async Task UpdateModem_WritesDocumentationCablingPinAndPhone()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var modem = harness.Session.GetAvailableProducts().First(p => ProductKinds.IsModem(p.Body.Tag));
        var loc = vm.InstallationNodes[0].Children[7].ElementId!.Value;   // Garage
        var mid = (await harness.Session.AddProductAsync(loc, modem.ProductIdentifier))!.Value;

        var ok = await harness.Session.UpdateModemAsync(mid, new ModemPropertiesResult(
            "Alarm modem", loc.ToToken(), "roof", "ID-9",
            "black", "red", "blue", "white", "4321", new List<string> { "+4512345678", "", "", "" }));

        var el = harness.Session.Current!.FindById(mid)!;
        var pin = el.DescendantsAndSelf().First(e => e.Tag == "sms_modem_pincode");
        var phone1 = el.DescendantsAndSelf().First(e => e.Tag == "sms_modem_phonenumber" && e.GetAttribute("address") == "1");
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(el.GetAttribute("name"), Is.EqualTo("Alarm modem"));
            Assert.That(el.GetAttribute("documentation_tag"), Is.EqualTo("ID-9"));
            Assert.That(el.GetAttribute("cablecolour_0V"), Is.EqualTo("black"));
            Assert.That(el.GetAttribute("cablecolour_RS485Plus"), Is.EqualTo("white"));
            Assert.That(pin.GetAttribute("value"), Is.EqualTo("4321"));
            Assert.That(phone1.GetAttribute("phonenumber"), Is.EqualTo("+4512345678"));
        });
    }

    // US-013: inserting a modem opens the modem dialog (not the generic product dialog).
    [Test]
    public async Task InsertModem_AutoOpensModemDialog()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        vm.SelectNode(vm.InstallationNodes[0].Children[0]);
        harness.Dialogs.ModemPropertiesResult = null;

        var modemLeaf = vm.SpecialProductsMenu[0];
        await ((IAsyncRelayCommand)modemLeaf.Command!).ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditModemPropertiesCalls, Is.EqualTo(1), "the modem dialog opened");
            Assert.That(harness.Dialogs.EditProductPropertiesCalls, Is.EqualTo(0), "not the generic product dialog");
            Assert.That(harness.Dialogs.LastModemPropertiesInput!.Title, Is.EqualTo("SMS modem properties"));
        });
    }

    [Test]
    public void SpecialProductsMenu_ContainsTheModem()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        Assert.That(vm.SpecialProductsMenu.Select(m => m.Header), Does.Contain("SMS Modem"));
    }

    // US-014: an inserted wireless product nests under its locality and shows the unlinked marker.
    [Test]
    public async Task InsertWireless_NestsProduct_WithUnlinkedMarker()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var wireless = harness.Session.GetAvailableProducts().First(p => p.CategoryPath.StartsWith("LK IHC Wireless"));
        var loc = vm.InstallationNodes[0].Children[3].ElementId!.Value;   // Bedroom

        var pid = await harness.Session.AddProductAsync(loc, wireless.ProductIdentifier);

        var node = vm.InstallationNodes[0].Children[3].Children[0];
        Assert.Multiple(() =>
        {
            Assert.That(pid, Is.Not.Null);
            Assert.That(node.DisplayName, Is.EqualTo(wireless.DisplayName));
            Assert.That(node.IsUnlinked, Is.True, "a freshly inserted wireless product is not yet linked");
            Assert.That(harness.Session.IsDirty, Is.True);
        });
    }

    // US-014: the wireless product dialog has no cabling — updating one never touches (or requires) cable attributes.
    [Test]
    public async Task UpdateWireless_WritesDocumentation_WithoutCabling()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var wireless = harness.Session.GetAvailableProducts().First(p => p.CategoryPath.StartsWith("LK IHC Wireless"));
        var loc = vm.InstallationNodes[0].Children[3].ElementId!.Value;
        var pid = (await harness.Session.AddProductAsync(loc, wireless.ProductIdentifier))!.Value;

        var ok = await harness.Session.UpdateProductAsync(pid,
            new ProductPropertiesResult("Sender", loc.ToToken(), "note", "IGNORED", "IGNORED", "ID-7", "LG-2"));

        var el = harness.Session.Current!.FindById(pid)!;
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True, "wireless cabling fields are skipped, not rejected by the schema");
            Assert.That(el.GetAttribute("name"), Is.EqualTo("Sender"));
            Assert.That(el.GetAttribute("documentation_tag"), Is.EqualTo("ID-7"));
            Assert.That(el.GetAttribute("power_group"), Is.EqualTo("LG-2"));
            Assert.That(el.GetAttribute("cabletype"), Is.Null, "a wireless product has no cabletype attribute");
        });
    }

    // US-014: inserting a wireless product opens the product dialog flagged as wireless (cabling hidden).
    [Test]
    public async Task InsertWireless_AutoOpensProductDialog_MarkedWireless()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        vm.SelectNode(vm.InstallationNodes[0].Children[0]);
        harness.Dialogs.ProductPropertiesResult = null;

        var leaf = FirstLeaf(vm.WirelessProductsMenu);
        await ((IAsyncRelayCommand)leaf.Command!).ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditProductPropertiesCalls, Is.EqualTo(1));
            Assert.That(harness.Dialogs.LastProductPropertiesInput!.IsWireless, Is.True, "the dialog is flagged wireless");
        });
    }

    [Test]
    public void WirelessProductsMenu_HasCategoriesFromCatalog()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        Assert.Multiple(() =>
        {
            Assert.That(vm.WirelessProductsMenu, Is.Not.Empty);
            Assert.That(vm.WirelessProductsMenu.All(m => !m.Header.Contains('#')), Is.True, "NN# prefixes stripped");
        });
    }

    // US-015: applying advanced dimmer settings writes the dimmer_setting_* values.
    [Test]
    public async Task UpdateDimmerSettings_WritesValues()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var dimmer = harness.Session.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("LK IHC Wireless") && p.CategoryPath.Contains("Dimmer"));
        var pid = (await harness.Session.AddProductAsync(vm.InstallationNodes[0].Children[3].ElementId!.Value, dimmer.ProductIdentifier))!.Value;

        var ok = await harness.Session.UpdateDimmerSettingsAsync(pid, new AdvancedDimmerResult(700, 800, 5, 30, 90, "rl"));

        var el = harness.Session.Current!.FindById(pid)!;
        string Val(string tag) => el.DescendantsAndSelf().First(e => e.Tag == tag).GetAttribute("value")!;
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(Val("dimmer_setting_fade_rate_up"), Is.EqualTo("700"));
            Assert.That(Val("dimmer_setting_fade_rate_down"), Is.EqualTo("800"));
            Assert.That(Val("dimmer_setting_dimming_rate"), Is.EqualTo("5"));
            Assert.That(Val("dimmer_setting_minimum_value"), Is.EqualTo("30"));
            Assert.That(Val("dimmer_setting_maximum_value"), Is.EqualTo("90"));
            Assert.That(Val("dimmer_setting_load_mode"), Is.EqualTo("rl"), "Inductive maps to the rl load mode");
        });
    }

    // US-015: a wireless dimmer's Properties dialog offers Advanced, which opens the advanced dialog and applies.
    [Test]
    public async Task WirelessDimmer_PropertiesAdvanced_OpensAdvancedDialog_AndApplies()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var dimmer = harness.Session.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("LK IHC Wireless") && p.CategoryPath.Contains("Dimmer"));
        var pid = (await harness.Session.AddProductAsync(vm.InstallationNodes[0].Children[3].ElementId!.Value, dimmer.ProductIdentifier))!.Value;
        var node = vm.InstallationNodes[0].Children[3].Children[0];

        harness.Dialogs.ProductPropertiesResponder = i =>
            new ProductPropertiesResult(i.Name, i.CurrentLocalityId, i.Note, "", "", i.IdentificationCode, i.LightGroup, OpenAdvanced: true);
        harness.Dialogs.AdvancedDimmerResult = new AdvancedDimmerResult(700, 700, 3, 20, 90, "rc");
        await vm.PropertiesCommand.ExecuteAsync(node);

        var el = harness.Session.Current!.FindById(pid)!;
        string Val(string tag) => el.DescendantsAndSelf().First(e => e.Tag == tag).GetAttribute("value")!;
        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastProductPropertiesInput!.IsWirelessDimmer, Is.True, "Advanced is offered for a dimmer");
            Assert.That(harness.Dialogs.EditAdvancedDimmerCalls, Is.EqualTo(1), "Advanced opened the dimmer dialog");
            Assert.That(Val("dimmer_setting_maximum_value"), Is.EqualTo("90"));
            Assert.That(Val("dimmer_setting_load_mode"), Is.EqualTo("rc"), "Capacitive maps to the rc load mode");
        });
    }

    // US-015: a non-dimmer wireless product does not offer the Advanced dimmer dialog.
    [Test]
    public async Task WirelessNonDimmer_HasNoAdvanced()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var wireless = harness.Session.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("LK IHC Wireless") && !p.CategoryPath.Contains("Dimmer"));
        await harness.Session.AddProductAsync(vm.InstallationNodes[0].Children[3].ElementId!.Value, wireless.ProductIdentifier);

        harness.Dialogs.ProductPropertiesResult = null;
        await vm.PropertiesCommand.ExecuteAsync(vm.InstallationNodes[0].Children[3].Children[0]);

        Assert.That(harness.Dialogs.LastProductPropertiesInput!.IsWirelessDimmer, Is.False);
    }

    // US-018: inserting a library function block nests it in the Functions pane with its variable sections and pins.
    [Test]
    public async Task InsertFunctionBlock_NestsInFunctionsPane_WithSections()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;   // Living room

        var fbId = await harness.Session.AddFunctionBlockAsync(loc, block.MasterType);

        var fbNode = vm.FunctionNodes[0].Children[0].Children[0];   // Living room > the block (Functions pane)
        var sectionLabels = fbNode.Children.Select(c => c.DisplayName).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(fbId, Is.Not.Null);
            Assert.That(fbNode.DisplayName, Is.EqualTo(block.DisplayName));
            Assert.That(sectionLabels, Is.EqualTo(new[] { "Input", "Output", "Settings", "Internal variables" }));
            Assert.That(fbNode.Children[0].Children, Is.Not.Empty, "the Input section shows the block's pins");
            Assert.That(vm.InstallationNodes[0].Children[0].Children, Is.Empty, "a function block is not shown in the Installation pane");
            Assert.That(harness.Session.IsDirty, Is.True);
        });
    }

    // US-018: the menu leaf inserts under the selected locality and confirms with the FB status string.
    [Test]
    public async Task InsertFunctionBlock_ViaMenuLeaf_TargetsSelection_AndConfirms()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        vm.SelectNode(vm.InstallationNodes[0].Children[2]);   // Kitchen

        var leaf = FirstLeaf(vm.FunctionBlocksMenu);
        await ((IAsyncRelayCommand)leaf.Command!).ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.StatusText, Is.EqualTo($"Function block '{leaf.Header}' has been inserted under Kitchen"));
            Assert.That(vm.FunctionNodes[0].Children[2].Children, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void FunctionBlocksMenu_HasLibraryFoldersFromCatalog()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        Assert.Multiple(() =>
        {
            Assert.That(vm.FunctionBlocksMenu, Is.Not.Empty);
            // The first folder is "00. Foretrukne" (Favourites) — folders order by their numeric prefix.
            Assert.That(vm.FunctionBlocksMenu.Any(m => m.Children.Count > 0 || m.IsLeaf), Is.True);
        });
    }

    // US-019: an empty function block is named "Empty block" and shows the four variable sections.
    [Test]
    public async Task InsertEmptyFunctionBlock_ShowsFourSections_InFunctionsPane()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;

        var fbId = await harness.Session.AddEmptyFunctionBlockAsync(loc);

        var fbNode = vm.FunctionNodes[0].Children[0].Children[0];
        Assert.Multiple(() =>
        {
            Assert.That(fbId, Is.Not.Null);
            Assert.That(fbNode.DisplayName, Is.EqualTo("Empty block"));
            Assert.That(fbNode.Children.Select(c => c.DisplayName),
                Is.EqualTo(new[] { "Input", "Output", "Settings", "Internal variables" }));
            Assert.That(harness.Session.IsDirty, Is.True);
        });
    }

    // US-019: the command inserts under the selected locality with the exact status string.
    [Test]
    public async Task InsertEmptyFunctionBlockCommand_TargetsSelection_AndConfirms()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        vm.SelectNode(vm.InstallationNodes[0].Children[7]);   // Garage

        await vm.InsertEmptyFunctionBlockCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.StatusText, Is.EqualTo("Empty block was inserted under Garage"));
            Assert.That(vm.FunctionNodes[0].Children[7].Children[0].DisplayName, Is.EqualTo("Empty block"));
        });
    }

    // US-019: a function block is renamed through the Properties route (F2).
    [Test]
    public async Task FunctionBlockNode_Properties_RenamesTheBlock()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        var fbNode = vm.FunctionNodes[0].Children[0].Children[0];

        harness.Dialogs.PropertiesResult = new PropertiesResult("Stair light logic", "my note");
        await vm.PropertiesCommand.ExecuteAsync(fbNode);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastPropertiesTitle, Is.EqualTo("Edit Empty block properties"));
            Assert.That(vm.FunctionNodes[0].Children[0].Children[0].DisplayName, Is.EqualTo("Stair light logic"));
        });
    }

    // US-020: unlocking a library block clears its lock and switches it to the editable icon.
    [Test]
    public async Task UnlockLibraryFunctionBlock_ClearsLock_AndSwitchesIcon()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var fbId = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;

        var before = vm.FunctionNodes[0].Children[0].Children[0];
        Assert.That(before.IsLockedFunctionBlock, Is.True, "a library block starts locked");
        Assert.That(before.IconAsset, Is.EqualTo("/Assets/fb-lk.svg"));

        var ok = await harness.Session.UnlockFunctionBlockAsync(fbId);

        var after = vm.FunctionNodes[0].Children[0].Children[0];
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(harness.Session.Current!.FindById(fbId)!.GetAttribute("locked"), Is.Null, "locked cleared (no = DTD default, dropped)");
            Assert.That(after.IsLockedFunctionBlock, Is.False);
            Assert.That(after.IconAsset, Is.EqualTo("/Assets/fb-editable.svg"), "the icon switches to editable");
            Assert.That(harness.Session.IsDirty, Is.True);
        });
    }

    // US-020: the Unlock command confirms and an empty (already-editable) block is not offered Unlock.
    [Test]
    public async Task UnlockCommand_Confirms_AndEmptyBlockIsNotLocked()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        await harness.Session.AddFunctionBlockAsync(loc, block.MasterType);
        await harness.Session.AddEmptyFunctionBlockAsync(loc);

        var libraryNode = vm.FunctionNodes[0].Children[0].Children[0];
        var emptyNode = vm.FunctionNodes[0].Children[0].Children[1];
        await vm.UnlockCommand.ExecuteAsync(libraryNode);

        Assert.Multiple(() =>
        {
            Assert.That(vm.StatusText, Does.Contain("Unlocked"));
            Assert.That(emptyNode.IsLockedFunctionBlock, Is.False, "an empty block is already editable — no Unlock");
            Assert.That(vm.FunctionNodes[0].Children[0].Children[0].IconAsset, Is.EqualTo("/Assets/fb-editable.svg"));
        });
    }

    // US-021: saving a placed block writes a valid, re-importable .ifb function-block file.
    [Test]
    public async Task SaveFunctionBlock_WritesReadableIfb()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var fbId = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        string path = harness.TempPath("MyStairLight.ifb");

        var ok = await harness.Session.SaveFunctionBlockAsync(fbId, path, "My stair light", "reusable block");

        // Re-import through a fresh catalog proves the file is a valid, readable .ifb.
        var reimport = new ProjectAppService(new IhcSettings());
        reimport.ImportCatalogFile(path);
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(File.Exists(path) && new FileInfo(path).Length > 0, Is.True, "a non-empty .ifb was written");
            Assert.That(reimport.GetAvailableFunctionBlocks().Any(f => f.MasterName == "My stair light"), Is.True,
                "the saved block re-imports into the catalog");
        });
    }

    // US-021: the Save block command prompts for name/note, writes the picked file, and confirms.
    [Test]
    public async Task SaveFunctionBlockCommand_PromptsThenWrites_AndConfirms()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        await harness.Session.AddFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value, block.MasterType);
        var fbNode = vm.FunctionNodes[0].Children[0].Children[0];

        string path = harness.TempPath("saved.ifb");
        harness.Dialogs.PropertiesResult = new PropertiesResult("Reusable", "note");
        harness.Dialogs.SaveBlockPath = path;
        await vm.SaveFunctionBlockCommand.ExecuteAsync(fbNode);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastPropertiesTitle, Is.EqualTo("Save function block"));
            Assert.That(File.Exists(path), Is.True);
            Assert.That(vm.StatusText, Is.EqualTo("Saved function block 'Reusable'."));
        });
    }

    // US-022: linking a product input to a block input shows reciprocal rows naming each other's full path.
    [Test]
    public async Task LinkProductInputToBlockInput_ShowsReciprocalRows()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;   // Living room
        var product = harness.Session.GetAvailableProducts().First(p => p.CategoryPath.StartsWith("Datalinie") && p.Resources.Count > 0);
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        await harness.Session.AddProductAsync(loc, product.ProductIdentifier);
        await harness.Session.AddFunctionBlockAsync(loc, block.MasterType);

        var productNode = vm.InstallationNodes[0].Children[0].Children[0];
        var productInput = productNode.Children[0];
        var fbNode = vm.FunctionNodes[0].Children[0].Children[0];
        var blockInput = fbNode.Children[0].Children[0];   // FB > Input section > first pin

        var ok = await harness.Session.LinkPinsAsync(productInput.ElementId!.Value, blockInput.ElementId!.Value);

        var blockInputAfter = vm.FunctionNodes[0].Children[0].Children[0].Children[0].Children[0];
        var productInputAfter = vm.InstallationNodes[0].Children[0].Children[0].Children[0];
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(blockInputAfter.Children, Has.Count.EqualTo(1), "the block input shows a link-from row");
            Assert.That(blockInputAfter.Children[0].DisplayName, Does.StartWith("←"));
            Assert.That(blockInputAfter.Children[0].DisplayName, Does.Contain(productNode.DisplayName), "names the source product path");
            Assert.That(productInputAfter.Children, Has.Count.EqualTo(1), "the product input shows a link-to row");
            Assert.That(productInputAfter.Children[0].DisplayName, Does.StartWith("→"));
            Assert.That(productInputAfter.Children[0].DisplayName, Does.Contain(fbNode.DisplayName), "names the target block path");
            Assert.That(harness.Session.IsDirty, Is.True);
        });
    }

    // US-022: the LinkPins gesture command links two pins and confirms; it ignores non-pin nodes.
    [Test]
    public async Task LinkPinsCommand_LinksPins_AndIgnoresNonPins()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.CategoryPath.StartsWith("Datalinie") && p.Resources.Count > 0);
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        await harness.Session.AddProductAsync(loc, product.ProductIdentifier);
        await harness.Session.AddFunctionBlockAsync(loc, block.MasterType);
        var productInput = vm.InstallationNodes[0].Children[0].Children[0].Children[0];
        var blockInput = vm.FunctionNodes[0].Children[0].Children[0].Children[0].Children[0];
        var localityNode = vm.InstallationNodes[0].Children[0];

        await vm.LinkPins(localityNode, blockInput);   // non-pin source → ignored
        Assert.That(vm.FunctionNodes[0].Children[0].Children[0].Children[0].Children[0].Children, Is.Empty, "linking a non-pin does nothing");

        await vm.LinkPins(productInput, blockInput);
        Assert.Multiple(() =>
        {
            Assert.That(vm.StatusText, Does.StartWith("Linked"));
            Assert.That(vm.FunctionNodes[0].Children[0].Children[0].Children[0].Children[0].Children, Is.Not.Empty);
        });
    }

    // US-022: the two-step link gesture — "Link from here" then "Link to here" — creates the link.
    [Test]
    public async Task StartLinkThenLinkToHere_CreatesTheLink()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.CategoryPath.StartsWith("Datalinie") && p.Resources.Count > 0);
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        await harness.Session.AddProductAsync(loc, product.ProductIdentifier);
        await harness.Session.AddFunctionBlockAsync(loc, block.MasterType);
        var productInput = vm.InstallationNodes[0].Children[0].Children[0].Children[0];
        var blockInput = vm.FunctionNodes[0].Children[0].Children[0].Children[0].Children[0];

        vm.StartLinkCommand.Execute(productInput);
        Assert.That(vm.PendingLinkSource, Is.SameAs(productInput), "the source pin is armed");

        await vm.LinkToHereCommand.ExecuteAsync(blockInput);

        Assert.Multiple(() =>
        {
            Assert.That(vm.PendingLinkSource, Is.Null, "the pending source is consumed");
            Assert.That(vm.FunctionNodes[0].Children[0].Children[0].Children[0].Children[0].Children, Is.Not.Empty,
                "the block input now shows its link row");
        });
    }

    // US-023: linking a block output onto a product output shows the block output "→" and the product output "←".
    [Test]
    public async Task LinkBlockOutputToProductOutput_ShowsReciprocalRows()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;   // Living room
        var product = harness.Session.GetAvailableProducts().First(p => p.CategoryPath.Contains("02#Output"));
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Outputs.Count > 0);
        var pid = (await harness.Session.AddProductAsync(loc, product.ProductIdentifier))!.Value;
        var fbId = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        var productOutputId = harness.Session.Current!.FindById(pid)!.ChildrenOrEmpty().First(c => c.Tag == "dataline_output").Id!.Value;
        var blockOutputId = harness.Session.Current!.FindById(fbId)!.FindChild("outputs")!.ChildrenOrEmpty().First().Id!.Value;

        // Drag the block output onto the product output (dragged = source = block output).
        var ok = await harness.Session.LinkPinsAsync(blockOutputId, productOutputId);

        var productOutputNode = FindNodeById(vm.InstallationNodes, productOutputId)!;
        var blockOutputNode = FindNodeById(vm.FunctionNodes, blockOutputId)!;
        var fbName = vm.FunctionNodes[0].Children[0].Children[0].DisplayName;
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(blockOutputNode.Children, Has.Count.EqualTo(1), "the block output shows a link-to (→) row");
            Assert.That(blockOutputNode.Children[0].DisplayName, Does.StartWith("→"));
            Assert.That(productOutputNode.Children, Has.Count.EqualTo(1), "the product output shows a link-from (←) row");
            Assert.That(productOutputNode.Children[0].DisplayName, Does.StartWith("←"));
            Assert.That(productOutputNode.Children[0].DisplayName, Does.Contain(fbName), "the ← row names the block path");
        });
    }

    // US-024: linking an FB scene output onto a product's scenes container adds a scene member + back-reference.
    [Test]
    public async Task LinkScene_CreatesSceneMembershipAndBackReference()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.Body.ChildrenOrEmpty().Any(c => c.Tag == "scenes"));
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Outputs.Any(o => o.Tag == "resource_scene"));
        var pid = (await harness.Session.AddProductAsync(loc, product.ProductIdentifier))!.Value;
        var fbId = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        var scenes = harness.Session.Current!.FindById(pid)!.ChildrenOrEmpty().First(c => c.Tag == "scenes");
        var scenesId = scenes.Id!.Value;
        var sceneOutId = harness.Session.Current!.FindById(fbId)!.FindChild("outputs")!.ChildrenOrEmpty()
            .First(c => c.Tag == "resource_scene").Id!.Value;
        bool isDimmer = Ihc.Vis.Model.ElementId.TryParse(scenes.GetAttribute("scene_resource"), out var b)
            && harness.Session.Current!.FindById(b)?.Tag == "airlink_dimming";

        var ok = await harness.Session.LinkSceneAsync(sceneOutId, scenesId, new SceneValueResult(true, 80, 0, 1), isDimmer);

        var scenesAfter = harness.Session.Current!.FindById(scenesId)!;
        var pinAfter = harness.Session.Current!.FindById(sceneOutId)!;
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(scenesAfter.ChildrenOrEmpty().Any(c => c.Tag is "scene_relay" or "scene_dimmer"), Is.True,
                "a scene member is added to the scenes container");
            Assert.That(pinAfter.ChildrenOrEmpty().Any(c => c.Tag == "scene_link"), Is.True,
                "the FB scene output gets a scene_link back-reference");
            Assert.That(harness.Session.IsDirty, Is.True);
        });
    }

    // US-024: the two-step gesture onto a scenes container opens the scene-value dialog then creates the link.
    [Test]
    public async Task LinkToScenes_OpensSceneDialog_AndShowsMemberRow()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.Body.ChildrenOrEmpty().Any(c => c.Tag == "scenes"));
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Outputs.Any(o => o.Tag == "resource_scene"));
        var pid = (await harness.Session.AddProductAsync(loc, product.ProductIdentifier))!.Value;
        var fbId = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        var scenesId = harness.Session.Current!.FindById(pid)!.ChildrenOrEmpty().First(c => c.Tag == "scenes").Id!.Value;
        var sceneOutId = harness.Session.Current!.FindById(fbId)!.FindChild("outputs")!.ChildrenOrEmpty()
            .First(c => c.Tag == "resource_scene").Id!.Value;
        var sceneOutNode = FindNodeById(vm.FunctionNodes, sceneOutId)!;
        var scenesNode = FindNodeById(vm.InstallationNodes, scenesId)!;

        harness.Dialogs.SceneValueResult = new SceneValueResult(true, 80, 0, 1);
        vm.StartLinkCommand.Execute(sceneOutNode);
        await vm.LinkToHereCommand.ExecuteAsync(scenesNode);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditSceneValueCalls, Is.EqualTo(1), "the scene-value dialog opened");
            Assert.That(vm.StatusText, Does.Contain("Scene link"));
            Assert.That(FindNodeById(vm.InstallationNodes, scenesId)!.Children, Is.Not.Empty,
                "the scenes container now shows its scene member row");
        });
    }

    // US-025: F4 on a link row jumps to the pin at the other end (in the other pane), both directions.
    [Test]
    public async Task NavigateLinkOpposite_JumpsToTheOtherEnd()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.CategoryPath.StartsWith("Datalinie") && p.Resources.Count > 0);
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        await harness.Session.AddProductAsync(loc, product.ProductIdentifier);
        await harness.Session.AddFunctionBlockAsync(loc, block.MasterType);
        var productInputId = vm.InstallationNodes[0].Children[0].Children[0].Children[0].ElementId!.Value;
        var blockInputId = vm.FunctionNodes[0].Children[0].Children[0].Children[0].Children[0].ElementId!.Value;
        await harness.Session.LinkPinsAsync(productInputId, blockInputId);

        // From the block input's link row → the product input.
        var blockInputLinkRow = vm.FunctionNodes[0].Children[0].Children[0].Children[0].Children[0].Children[0];
        vm.NavigateLinkOppositeCommand.Execute(blockInputLinkRow);
        var jumpedToProduct = vm.SelectedNode;

        // From the product input's link row → the block input.
        var productInputLinkRow = vm.InstallationNodes[0].Children[0].Children[0].Children[0].Children[0];
        vm.NavigateLinkOppositeCommand.Execute(productInputLinkRow);
        var jumpedToBlock = vm.SelectedNode;

        Assert.Multiple(() =>
        {
            Assert.That(jumpedToProduct?.ElementId, Is.EqualTo(productInputId), "F4 from the block end selects the product input");
            Assert.That(jumpedToBlock?.ElementId, Is.EqualTo(blockInputId), "F4 from the product end selects the block input");
            Assert.That(vm.StatusText, Does.Contain("Jumped"));
        });
    }

    // US-057: removing a link deletes both reciprocal halves, leaving the pin's other links intact; undoable-ready.
    [Test]
    public async Task RemoveLink_DeletesBothHalves_KeepingOtherLinks()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.CategoryPath.StartsWith("Datalinie") && p.Resources.Count > 0);
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 1);
        await harness.Session.AddProductAsync(loc, product.ProductIdentifier);
        await harness.Session.AddFunctionBlockAsync(loc, block.MasterType);
        var productInputId = vm.InstallationNodes[0].Children[0].Children[0].Children[0].ElementId!.Value;
        var blockInputs = vm.FunctionNodes[0].Children[0].Children[0].Children[0].Children;
        await harness.Session.LinkPinsAsync(productInputId, blockInputs[0].ElementId!.Value);
        await harness.Session.LinkPinsAsync(productInputId, blockInputs[1].ElementId!.Value);

        var productInput = FindNodeById(vm.InstallationNodes, productInputId)!;
        Assert.That(productInput.Children, Has.Count.EqualTo(2), "the product input has two links");
        var linkRowId = productInput.Children[0].ElementId!.Value;

        var ok = await harness.Session.RemoveLinkAsync(linkRowId);

        var productInputFinal = FindNodeById(vm.InstallationNodes, productInputId)!;
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(productInputFinal.Children, Has.Count.EqualTo(1), "one link removed, the other stays");
            Assert.That(harness.Session.IsDirty, Is.True);
        });
    }

    // US-057: removing a scene link removes both the scene member and the scene_link back-reference.
    [Test]
    public async Task RemoveSceneLink_RemovesMemberAndBackReference()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.Body.ChildrenOrEmpty().Any(c => c.Tag == "scenes"));
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Outputs.Any(o => o.Tag == "resource_scene"));
        var pid = (await harness.Session.AddProductAsync(loc, product.ProductIdentifier))!.Value;
        var fbId = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        var scenes = harness.Session.Current!.FindById(pid)!.ChildrenOrEmpty().First(c => c.Tag == "scenes");
        var scenesId = scenes.Id!.Value;
        var sceneOutId = harness.Session.Current!.FindById(fbId)!.FindChild("outputs")!.ChildrenOrEmpty().First(c => c.Tag == "resource_scene").Id!.Value;
        bool isDimmer = Ihc.Vis.Model.ElementId.TryParse(scenes.GetAttribute("scene_resource"), out var b)
            && harness.Session.Current!.FindById(b)?.Tag == "airlink_dimming";
        await harness.Session.LinkSceneAsync(sceneOutId, scenesId, new SceneValueResult(true, 80, 0, 1), isDimmer);
        var memberId = harness.Session.Current!.FindById(scenesId)!.ChildrenOrEmpty().First(c => c.Tag is "scene_relay" or "scene_dimmer").Id!.Value;

        var ok = await harness.Session.RemoveLinkAsync(memberId);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(harness.Session.Current!.FindById(scenesId)!.ChildrenOrEmpty().Any(c => c.Tag is "scene_relay" or "scene_dimmer"), Is.False,
                "the scene member is removed");
            Assert.That(harness.Session.Current!.FindById(sceneOutId)!.ChildrenOrEmpty().Any(c => c.Tag == "scene_link"), Is.False,
                "the scene_link back-reference is removed");
        });
    }

    // US-058: opening Properties on a scene member reopens the value dialog pre-filled, and stores the new value.
    [Test]
    public async Task EditSceneValue_ReopensPrefilled_AndStoresNewValue()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.Body.ChildrenOrEmpty().Any(c => c.Tag == "scenes"));
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Outputs.Any(o => o.Tag == "resource_scene"));
        var pid = (await harness.Session.AddProductAsync(loc, product.ProductIdentifier))!.Value;
        var fbId = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        var scenes = harness.Session.Current!.FindById(pid)!.ChildrenOrEmpty().First(c => c.Tag == "scenes");
        var scenesId = scenes.Id!.Value;
        var sceneOutId = harness.Session.Current!.FindById(fbId)!.FindChild("outputs")!.ChildrenOrEmpty().First(c => c.Tag == "resource_scene").Id!.Value;
        bool isDimmer = Ihc.Vis.Model.ElementId.TryParse(scenes.GetAttribute("scene_resource"), out var b)
            && harness.Session.Current!.FindById(b)?.Tag == "airlink_dimming";
        await harness.Session.LinkSceneAsync(sceneOutId, scenesId, new SceneValueResult(true, 80, 0, 1), isDimmer);
        var memberId = harness.Session.Current!.FindById(scenesId)!.ChildrenOrEmpty().First(c => c.Tag is "scene_relay" or "scene_dimmer").Id!.Value;
        var memberNode = FindNodeById(vm.InstallationNodes, memberId)!;

        harness.Dialogs.SceneValueResult = new SceneValueResult(false, 20, 0, 3);   // new value
        await vm.PropertiesCommand.ExecuteAsync(memberNode);

        var input = harness.Dialogs.LastSceneValueInput!;
        var memberAfter = harness.Session.Current!.FindById(memberId)!;
        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditSceneValueCalls, Is.EqualTo(1), "the scene-value dialog reopened");
            Assert.That(input.IsDimmer, Is.EqualTo(isDimmer));
            Assert.That(vm.StatusText, Does.Contain("Scene value updated"));
            if (isDimmer)
            {
                Assert.That(input.LevelPercent, Is.EqualTo(80), "prefilled with the initial level");
                Assert.That(input.RampSeconds, Is.EqualTo(1), "prefilled with the initial ramp seconds");
                Assert.That(memberAfter.GetAttribute("dimming_value"), Is.EqualTo("20"), "the new level is stored");
            }
            else
            {
                Assert.That(input.On, Is.True, "prefilled with the initial ON state");
                Assert.That(memberAfter.GetAttribute("relay_value"), Is.Null, "OFF is the default and is dropped");
            }
        });
    }

    // US-026: F3 enters programming mode — panes headed with the block name, left = variable sections, right = program.
    [Test]
    public async Task EnterProgrammingMode_ShowsBlockSectionsAndProgramSubtree()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        var fbNode = vm.FunctionNodes[0].Children[0].Children[0];   // Living room > Empty block

        vm.EnterProgrammingModeCommand.Execute(fbNode);

        var leftBlock = vm.InstallationNodes[0];
        var rightBlock = vm.FunctionNodes[0];
        var programs = rightBlock.Children[0];
        Assert.Multiple(() =>
        {
            Assert.That(vm.IsProgrammingMode, Is.True);
            Assert.That(vm.InstallationPaneHeader, Is.EqualTo("Empty block"));
            Assert.That(vm.FunctionsPaneHeader, Is.EqualTo("Empty block"));
            Assert.That(leftBlock.DisplayName, Is.EqualTo("Empty block"));
            Assert.That(leftBlock.Children.Select(c => c.DisplayName),
                Is.EqualTo(new[] { "Input", "Output", "Settings", "Internal variables" }));
            Assert.That(programs.DisplayName, Is.EqualTo("Programs"));
            Assert.That(programs.Children, Has.Count.EqualTo(1), "the empty block has one program");
            Assert.That(programs.Children[0].Children.Select(c => c.DisplayName),
                Is.EqualTo(new[] { "Events", "Commands" }));
        });
    }

    // US-026: Esc leaves programming mode and restores the two locality trees.
    [Test]
    public async Task LeaveProgrammingMode_ReturnsToLocalities()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);

        vm.LeaveProgrammingModeCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsProgrammingMode, Is.False);
            Assert.That(vm.InstallationPaneHeader, Is.EqualTo("Installation"));
            Assert.That(vm.FunctionsPaneHeader, Is.EqualTo("Functions"));
            Assert.That(vm.InstallationNodes[0].DisplayName, Is.EqualTo("Localities"));
            Assert.That(vm.InstallationNodes[0].Children, Has.Count.EqualTo(10));
        });
    }

    // US-027: adding a typed variable to a block section places it there.
    [Test]
    public async Task AddVariable_ToInternalSection_PlacesTheVariable()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        var internalSectionId = vm.InstallationNodes[0].Children[3].ElementId!.Value;   // "Internal variables"

        var id = await harness.Session.AddVariableAsync(internalSectionId, "resource_temperature", "Temperature");

        var section = harness.Session.Current!.FindById(internalSectionId)!;
        Assert.Multiple(() =>
        {
            Assert.That(id, Is.Not.Null);
            Assert.That(section.ChildrenOrEmpty().Any(c => c.Tag == "resource_temperature" && c.GetAttribute("name") == "Temperature"), Is.True);
            Assert.That(harness.Session.IsDirty, Is.True);
        });
    }

    // US-027: the section constrains the type — settings refuses a pin type.
    [Test]
    public async Task AddVariable_PinTypeIntoSettings_IsRejected()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        var settingsId = vm.InstallationNodes[0].Children[2].ElementId!.Value;   // "Settings"

        var result = await harness.Session.AddVariableAsync(settingsId, "resource_input", "Bad");

        Assert.That(result, Is.Null, "a pin type cannot go into the settings section");
    }

    // US-027: the variable palette is section-aware, and inserting from it confirms with the vendor status string.
    [Test]
    public async Task VariablePalette_IsSectionAware_AndInsertConfirms()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);

        vm.SelectNode(vm.InstallationNodes[0].Children[0]);   // select the Input section
        var inputPalette = vm.VariablePaletteMenu.Select(m => m.Header).ToList();
        vm.SelectNode(vm.InstallationNodes[0].Children[3]);   // select Internal variables
        var internalPalette = vm.VariablePaletteMenu.Select(m => m.Header).ToList();

        var flagLeaf = vm.VariablePaletteMenu.First(m => m.Header == "Flag");
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)flagLeaf.Command!).ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(inputPalette, Is.EqualTo(new[] { "Input" }), "the Input section offers only Input");
            Assert.That(internalPalette, Does.Contain("Flag").And.Not.Contain("Input"), "a value section offers value types, not pins");
            Assert.That(vm.StatusText, Is.EqualTo("Flag was inserted under Internal variables"));
            Assert.That(vm.InstallationNodes[0].Children[3].Children.Any(c => c.DisplayName.Contains("Flag")), Is.True);
        });
    }

    // US-028: arming a variable then choosing an event on the program's Events node authors and renders the event.
    [Test]
    public async Task AddEvent_FromArmedVariable_AuthorsAndRendersEvent()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        var inputSectionId = vm.InstallationNodes[0].Children[0].ElementId!.Value;   // "Input"
        await harness.Session.AddVariableAsync(inputSectionId, "resource_input", "Doorbell");

        vm.UseInProgramCommand.Execute(vm.InstallationNodes[0].Children[0].Children[0]);   // arm the new Input pin
        var eventsNode = FindByFlag(vm.FunctionNodes, n => n.IsEventsContainer)!;
        vm.SelectNode(eventsNode);
        var option = vm.ProgramEventMenu.First(m => m.Header == "Doorbell changes to ON");
        await ((IAsyncRelayCommand)option.Command!).ExecuteAsync(null);

        var eventsAfter = FindByFlag(vm.FunctionNodes, n => n.IsEventsContainer)!;
        Assert.Multiple(() =>
        {
            Assert.That(vm.ProgramEventMenu.Select(m => m.Header),
                Is.EquivalentTo(new[] { "Doorbell changes to ON", "Doorbell changes state", "Doorbell is assigned" }));
            Assert.That(eventsAfter.Children.Any(c => c.DisplayName == "Doorbell -> ON"), Is.True, "the event renders under Events");
            Assert.That(harness.Session.IsDirty, Is.True);
        });
    }

    // US-028: a command authored on the Commands node drives the armed variable and renders under Commands.
    [Test]
    public async Task AddCommand_FromArmedVariable_AuthorsAndRendersCommand()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        var outputSectionId = vm.InstallationNodes[0].Children[1].ElementId!.Value;   // "Output"
        await harness.Session.AddVariableAsync(outputSectionId, "resource_output", "Chime");

        vm.UseInProgramCommand.Execute(vm.InstallationNodes[0].Children[1].Children[0]);   // arm the new Output pin
        var commandsNode = FindByFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!;
        vm.SelectNode(commandsNode);
        var toggle = vm.ProgramCommandMenu.First(m => m.Header == "Chime toggled");
        await ((IAsyncRelayCommand)toggle.Command!).ExecuteAsync(null);

        var commandsAfter = FindByFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!;
        var authored = harness.Session.Current!.FindById(commandsAfter.ElementId!.Value)!
            .ChildrenOrEmpty().First(a => a.Tag == "action");
        Assert.Multiple(() =>
        {
            Assert.That(commandsAfter.Children.Any(c => c.DisplayName == "Toggle Chime"), Is.True, "the command renders under Commands");
            Assert.That(authored.GetAttribute("method"), Is.EqualTo("_0x23"), "toggle uses the vendor token _0x23");
            Assert.That(vm.StatusText, Is.EqualTo("Command added to the program."));
        });
    }

    // US-028: without an armed variable the program nodes offer nothing (the operand is required).
    [Test]
    public async Task ProgramMenus_AreEmpty_UntilAVariableIsArmed()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);

        vm.SelectNode(FindByFlag(vm.FunctionNodes, n => n.IsEventsContainer)!);

        Assert.That(vm.ProgramEventMenu, Is.Empty, "no operand armed → no offered events");
    }

    // US-029: inserting a sub-program on the Commands group builds the Conditions + true/false command structure.
    [Test]
    public async Task AddSubProgram_OnCommands_InsertsConditionalStructure()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);

        await vm.AddSubProgramCommand.ExecuteAsync(FindByFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!);

        var sub = FindByFlag(vm.FunctionNodes, n => n.DisplayName == "Sub-program");
        Assert.Multiple(() =>
        {
            Assert.That(sub, Is.Not.Null, "a Sub-program node is inserted");
            Assert.That(sub!.Children.Any(c => c.IsConditionsContainer), Is.True, "it has a Conditions group");
            Assert.That(sub.Children.Any(c => c.DisplayName == "Commands when conditions true"), Is.True);
            Assert.That(sub.Children.Any(c => c.DisplayName == "Commands when conditions false"), Is.True);
        });
    }

    // US-029: the popup's NOT variant negates the condition — persisted with the vendor "<>" token _0x28.
    [Test]
    public async Task AddCondition_NotVariant_UsesNotToken()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        await harness.Session.AddVariableAsync(vm.InstallationNodes[0].Children[2].ElementId!.Value, "resource_flag", "Away");
        await vm.AddSubProgramCommand.ExecuteAsync(FindByFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!);

        vm.UseInProgramCommand.Execute(vm.InstallationNodes[0].Children[2].Children[0]);   // arm the Away flag
        var conditionsNode = FindByFlag(vm.FunctionNodes, n => n.IsConditionsContainer)!;
        vm.SelectNode(conditionsNode);
        var notOption = vm.ProgramConditionMenu.First(m => m.Header == "Away is NOT ON");
        await ((IAsyncRelayCommand)notOption.Command!).ExecuteAsync(null);

        var condition = harness.Session.Current!.FindById(conditionsNode.ElementId!.Value)!
            .ChildrenOrEmpty().First(c => c.Tag == "condition");
        var rendered = FindByFlag(vm.FunctionNodes, n => n.IsConditionsContainer)!;
        Assert.Multiple(() =>
        {
            Assert.That(condition.GetAttribute("method"), Is.EqualTo("_0x28"), "NOT uses the vendor token _0x28");
            Assert.That(rendered.Children.Any(c => c.DisplayName == "Away <> ON"), Is.True, "the condition renders");
        });
    }

    // US-029: a Conditions group toggles from the default AND (&) to OR (>=1).
    [Test]
    public async Task ToggleConditions_ToOr_ShowsOrCombination()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        await vm.AddSubProgramCommand.ExecuteAsync(FindByFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!);

        await vm.SetConditionsOrCommand.ExecuteAsync(FindByFlag(vm.FunctionNodes, n => n.IsConditionsContainer)!);

        var after = FindByFlag(vm.FunctionNodes, n => n.IsConditionsContainer)!;
        Assert.Multiple(() =>
        {
            Assert.That(after.IsOrGroup, Is.True);
            Assert.That(after.DisplayName, Does.Contain(">=1"));
            Assert.That(harness.Session.Current!.FindById(after.ElementId!.Value)!.GetAttribute("type"), Is.EqualTo("or"));
        });
    }

    // US-029: a nested logic group is inserted inside the Conditions group for a compound expression.
    [Test]
    public async Task AddLogicGroup_NestsAConditionsGroup()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        await vm.AddSubProgramCommand.ExecuteAsync(FindByFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!);

        await vm.AddLogicGroupCommand.ExecuteAsync(FindByFlag(vm.FunctionNodes, n => n.IsConditionsContainer)!);

        var conditions = FindByFlag(vm.FunctionNodes, n => n.IsConditionsContainer)!;
        Assert.That(conditions.Children.Any(c => c.IsConditionsContainer && c.DisplayName.StartsWith("Logic group")),
            Is.True, "a nested logic group renders inside Conditions");
    }

    // US-030: creating an enum through the Settings palette authors a project-global type and a variable of it.
    [Test]
    public async Task CreateEnum_AddsGlobalTypeAndVariable()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        var settingsSectionId = vm.InstallationNodes[0].Children[2].ElementId!.Value;   // "Settings"
        harness.Dialogs.EnumDefinitionResult = new EnumDefinitionResult("Mode", new[] { "Direct", "With delay", "Switched off" });

        vm.SelectNode(vm.InstallationNodes[0].Children[2]);
        var enumLeaf = vm.VariablePaletteMenu.First(m => m.Header == "Enum");
        await ((IAsyncRelayCommand)enumLeaf.Command!).ExecuteAsync(null);

        var enumVar = harness.Session.Current!.FindById(settingsSectionId)!.ChildrenOrEmpty().First(c => c.Tag == "resource_enum");
        ElementId.TryParse(enumVar.GetAttribute("typedef"), out var defId);
        var def = harness.Session.Current!.FindById(defId)!;
        Assert.Multiple(() =>
        {
            Assert.That(enumVar.GetAttribute("name"), Is.EqualTo("Mode"), "the variable is named after the type");
            Assert.That(def.GetAttribute("name"), Is.EqualTo("Mode"));
            Assert.That(def.ChildrenOrEmpty().Count(c => c.Tag == "enum_value"), Is.EqualTo(3), "the three states are stored in order");
            Assert.That(harness.Session.Current!.FindParent(defId)!.Tag, Is.EqualTo("enum_definitions"),
                "the type is project-global (in enum_definitions), reusable by other blocks");
        });
    }

    // US-030: editing an enum variable's type appends only the newly-listed states (append-only, no duplicates).
    [Test]
    public async Task EditEnum_AppendsNewStatesOnly()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        var settingsSectionId = vm.InstallationNodes[0].Children[2].ElementId!.Value;
        harness.Dialogs.EnumDefinitionResult = new EnumDefinitionResult("Mode", new[] { "Direct", "With delay" });
        vm.SelectNode(vm.InstallationNodes[0].Children[2]);
        await ((IAsyncRelayCommand)vm.VariablePaletteMenu.First(m => m.Header == "Enum").Command!).ExecuteAsync(null);
        var enumVarId = harness.Session.Current!.FindById(settingsSectionId)!.ChildrenOrEmpty().First(c => c.Tag == "resource_enum").Id!.Value;

        // Re-run Properties: keep the two existing states and add one new one; a duplicate must not double.
        harness.Dialogs.EnumDefinitionResult = new EnumDefinitionResult("Mode", new[] { "Direct", "With delay", "Switched off" });
        await vm.PropertiesCommand.ExecuteAsync(FindNodeById(vm.InstallationNodes, enumVarId));

        ElementId.TryParse(harness.Session.Current!.FindById(enumVarId)!.GetAttribute("typedef"), out var defId);
        var values = harness.Session.Current!.FindById(defId)!.ChildrenOrEmpty().Where(c => c.Tag == "enum_value").ToList();
        Assert.Multiple(() =>
        {
            Assert.That(values.Count, Is.EqualTo(3), "one new state appended, no duplication of the existing two");
            Assert.That(values[^1].GetAttribute("name"), Is.EqualTo("Switched off"));
            Assert.That(values[^1].GetAttribute("index"), Is.EqualTo("2"), "the appended state continues the 0-based index");
        });
    }

    // US-030: the project ships with built-in enumerator types (they live in the global enum_definitions container).
    [Test]
    public async Task DefaultProject_ProvidesBuiltInEnumerators()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        var container = harness.Session.Current!.Child("enum_definitions");
        var types = container?.ChildrenOrEmpty().Count(c => c.Tag == "enum_definition") ?? 0;
        Assert.That(types, Is.GreaterThanOrEqualTo(2), "at least two default enumerator types are available");
    }

    // US-031: a case on an eligible switch variable (a counter) inserts with only an Else branch.
    [Test]
    public async Task AddCase_OnCommands_WithEligibleVariable_InsertsCaseWithElse()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        await harness.Session.AddVariableAsync(vm.InstallationNodes[0].Children[3].ElementId!.Value, "resource_counter", "Cleanings");

        vm.UseInProgramCommand.Execute(vm.InstallationNodes[0].Children[3].Children[0]);   // arm the counter
        vm.SelectNode(FindByFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!);
        await ((IAsyncRelayCommand)vm.ProgramCaseMenu.First(m => m.Header == "Case (Cleanings)").Command!).ExecuteAsync(null);

        var caseNode = FindByFlag(vm.FunctionNodes, n => n.IsCaseNode)!;
        Assert.Multiple(() =>
        {
            Assert.That(caseNode.DisplayName, Is.EqualTo("Case (Cleanings)"));
            Assert.That(caseNode.Children.Any(c => c.DisplayName == "Else"), Is.True, "the case starts with only an Else branch");
        });
    }

    // US-031: an ineligible variable (a boolean flag) offers no Case option.
    [Test]
    public async Task Case_NotOffered_ForIneligibleVariable()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        await harness.Session.AddVariableAsync(vm.InstallationNodes[0].Children[3].ElementId!.Value, "resource_flag", "Away");

        vm.UseInProgramCommand.Execute(vm.InstallationNodes[0].Children[3].Children[0]);   // arm the flag
        vm.SelectNode(FindByFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!);

        Assert.That(vm.ProgramCaseMenu, Is.Empty, "a flag is not an eligible case switch");
    }

    // US-031: a case value branch stores its criterion as a typed operand (a counter's inivalue).
    [Test]
    public async Task AddCaseValue_AddsTypedCriterionBranch()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        await harness.Session.AddVariableAsync(vm.InstallationNodes[0].Children[3].ElementId!.Value, "resource_counter", "Cleanings");
        vm.UseInProgramCommand.Execute(vm.InstallationNodes[0].Children[3].Children[0]);
        vm.SelectNode(FindByFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!);
        await ((IAsyncRelayCommand)vm.ProgramCaseMenu.First(m => m.Header == "Case (Cleanings)").Command!).ExecuteAsync(null);
        var caseId = FindByFlag(vm.FunctionNodes, n => n.IsCaseNode)!.ElementId!.Value;

        harness.Dialogs.PropertiesResult = new PropertiesResult("100", string.Empty);
        await vm.NewCaseValueCommand.ExecuteAsync(FindByFlag(vm.FunctionNodes, n => n.IsCaseNode));

        var caseAction = harness.Session.Current!.FindById(caseId)!.ChildrenOrEmpty().First(c => c.Tag == "case_action");
        var operand = caseAction.ChildrenOrEmpty().First(c => c.Tag == "resource_counter");
        Assert.Multiple(() =>
        {
            Assert.That(caseAction.GetAttribute("name"), Is.EqualTo("100"), "the value branch is tagged with its criterion");
            Assert.That(operand.GetAttribute("inivalue"), Is.EqualTo("100"), "the criterion is stored as a typed counter operand");
        });
    }

    // US-031: a case value branch is fillable — the normal Add-command gesture drops a command into it.
    [Test]
    public async Task CaseValueBranch_AcceptsCommands()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        await harness.Session.AddVariableAsync(vm.InstallationNodes[0].Children[3].ElementId!.Value, "resource_counter", "Cleanings");
        await harness.Session.AddVariableAsync(vm.InstallationNodes[0].Children[1].ElementId!.Value, "resource_output", "Light");
        vm.UseInProgramCommand.Execute(vm.InstallationNodes[0].Children[3].Children[0]);
        vm.SelectNode(FindByFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!);
        await ((IAsyncRelayCommand)vm.ProgramCaseMenu.First(m => m.Header == "Case (Cleanings)").Command!).ExecuteAsync(null);
        harness.Dialogs.PropertiesResult = new PropertiesResult("100", string.Empty);
        await vm.NewCaseValueCommand.ExecuteAsync(FindByFlag(vm.FunctionNodes, n => n.IsCaseNode));

        var valueBranch = FindByFlag(vm.FunctionNodes, n => n.IsCaseNode)!.Children.First(c => c.DisplayName == "100");
        vm.UseInProgramCommand.Execute(vm.InstallationNodes[0].Children[1].Children[0]);   // arm the output
        vm.SelectNode(valueBranch);
        await ((IAsyncRelayCommand)vm.ProgramCommandMenu.First(m => m.Header == "Light set to ON").Command!).ExecuteAsync(null);

        var branch = harness.Session.Current!.FindById(valueBranch.ElementId!.Value)!;
        Assert.That(branch.ChildrenOrEmpty().Any(a => a.Tag == "action"), Is.True, "the command lands in the case value branch");
    }

    // US-032: the value palette now offers Decimal (resource_floating_point) so decimal arithmetic is possible.
    [Test]
    public async Task VariablePalette_OffersDecimal_AndInserts()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        var settingsId = vm.InstallationNodes[0].Children[2].ElementId!.Value;

        vm.SelectNode(vm.InstallationNodes[0].Children[2]);
        var decimalLeaf = vm.VariablePaletteMenu.First(m => m.Header == "Decimal");
        await ((IAsyncRelayCommand)decimalLeaf.Command!).ExecuteAsync(null);

        Assert.That(harness.Session.Current!.FindById(settingsId)!.ChildrenOrEmpty().Any(c => c.Tag == "resource_floating_point"),
            Is.True, "a decimal variable is inserted");
    }

    // US-032: an arithmetic command line adds two decimals — one operation, target + operand, vendor add token _0x5a.
    [Test]
    public async Task Arithmetic_AddDecimals_AuthorsOneOperationCommand()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        var settingsId = vm.InstallationNodes[0].Children[2].ElementId!.Value;
        await harness.Session.AddVariableAsync(settingsId, "resource_floating_point", "F1");
        await harness.Session.AddVariableAsync(settingsId, "resource_floating_point", "F2");

        vm.UseInProgramCommand.Execute(vm.InstallationNodes[0].Children[2].Children.First(c => c.DisplayName == "F1"));
        vm.SelectNode(FindByFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!);
        var addCategory = vm.ProgramArithmeticMenu.First(m => m.Header.StartsWith("F1 +"));
        await ((IAsyncRelayCommand)addCategory.Children.First(c => c.Header == "F2").Command!).ExecuteAsync(null);

        var commandsId = FindByFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!.ElementId!.Value;
        var action = harness.Session.Current!.FindById(commandsId)!.ChildrenOrEmpty().First(a => a.Tag == "action");
        ElementId.TryParse(action.GetAttribute("link1"), out var l1);
        ElementId.TryParse(action.GetAttribute("link2"), out var l2);
        var rendered = FindByFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!;
        Assert.Multiple(() =>
        {
            Assert.That(action.GetAttribute("method"), Is.EqualTo("_0x5a"), "+ uses the vendor add token");
            Assert.That(action.GetAttribute("link1"), Is.Not.Null.And.Not.Empty, "one target register (link1)");
            Assert.That(action.GetAttribute("link2"), Is.Not.Null.And.Not.Empty, "one operand (link2) — a single operation");
            Assert.That(harness.Session.Current!.FindById(l1)!.GetAttribute("name"), Is.EqualTo("F1"));
            Assert.That(harness.Session.Current!.FindById(l2)!.GetAttribute("name"), Is.EqualTo("F2"));
            Assert.That(rendered.Children.Any(c => c.DisplayName == "F1 = F1 + F2"), Is.True, "the command renders the formula");
        });
    }

    // US-032: subtraction uses the vendor subtract token _0x64.
    [Test]
    public async Task Arithmetic_Subtract_UsesSubtractToken()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        var settingsId = vm.InstallationNodes[0].Children[2].ElementId!.Value;
        await harness.Session.AddVariableAsync(settingsId, "resource_floating_point", "F1");
        await harness.Session.AddVariableAsync(settingsId, "resource_floating_point", "F2");

        vm.UseInProgramCommand.Execute(vm.InstallationNodes[0].Children[2].Children.First(c => c.DisplayName == "F1"));
        vm.SelectNode(FindByFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!);
        var subCategory = vm.ProgramArithmeticMenu.First(m => m.Header.Contains("−"));
        await ((IAsyncRelayCommand)subCategory.Children.First(c => c.Header == "F2").Command!).ExecuteAsync(null);

        var commandsId = FindByFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!.ElementId!.Value;
        var action = harness.Session.Current!.FindById(commandsId)!.ChildrenOrEmpty().First(a => a.Tag == "action");
        Assert.That(action.GetAttribute("method"), Is.EqualTo("_0x64"));
    }

    // US-032: the decimal→integer conversion pattern — a decimal added to an integer target (truncation is runtime).
    [Test]
    public async Task Arithmetic_AddDecimalToInteger_TargetsTheInteger()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        var settingsId = vm.InstallationNodes[0].Children[2].ElementId!.Value;
        await harness.Session.AddVariableAsync(settingsId, "resource_integer", "Number");
        await harness.Session.AddVariableAsync(settingsId, "resource_floating_point", "F1");

        vm.UseInProgramCommand.Execute(vm.InstallationNodes[0].Children[2].Children.First(c => c.DisplayName == "Number"));
        vm.SelectNode(FindByFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!);
        var addCategory = vm.ProgramArithmeticMenu.First(m => m.Header.StartsWith("Number +"));
        await ((IAsyncRelayCommand)addCategory.Children.First(c => c.Header == "F1").Command!).ExecuteAsync(null);

        var commandsId = FindByFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!.ElementId!.Value;
        var action = harness.Session.Current!.FindById(commandsId)!.ChildrenOrEmpty().First(a => a.Tag == "action");
        ElementId.TryParse(action.GetAttribute("link1"), out var target);
        Assert.That(harness.Session.Current!.FindById(target)!.Tag, Is.EqualTo("resource_integer"),
            "the running register that receives (and truncates) the result is the integer");
    }

    // US-033: a Powerup system event is added to the Events group with no operand and renders as "Powerup".
    [Test]
    public async Task AddPowerEvent_AddsPowerupToEvents()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        var eventsNode = FindByFlag(vm.FunctionNodes, n => n.IsEventsContainer)!;

        await vm.AddPowerEventCommand.ExecuteAsync(eventsNode);

        var eventsEl = harness.Session.Current!.FindById(eventsNode.ElementId!.Value)!;
        var after = FindByFlag(vm.FunctionNodes, n => n.IsEventsContainer)!;
        Assert.Multiple(() =>
        {
            Assert.That(eventsEl.ChildrenOrEmpty().Any(e => e.Tag == "event_power"), Is.True, "a Powerup (event_power) is stored");
            Assert.That(after.Children.Any(c => c.DisplayName == "Powerup"), Is.True, "the Powerup event renders");
        });
    }

    // US-033: ticking "Save current value" on a function-block output sets backup=yes and marks it saved; untick clears it.
    [Test]
    public async Task SaveCurrentValue_OnFunctionBlockOutput_TogglesBackup()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        var outputSectionId = vm.InstallationNodes[0].Children[1].ElementId!.Value;   // "Output"
        var outputId = (await harness.Session.AddVariableAsync(outputSectionId, "resource_output", "Light"))!.Value;

        await vm.ToggleSaveValueCommand.ExecuteAsync(FindNodeById(vm.InstallationNodes, outputId));
        var savedNode = FindNodeById(vm.InstallationNodes, outputId)!;
        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.Current!.FindById(outputId)!.GetAttribute("backup"), Is.EqualTo("yes"));
            Assert.That(savedNode.IsValueSaved, Is.True);
            Assert.That(savedNode.DisplayName, Does.Contain("(saved)"), "the saved output is marked in the tree");
        });

        await vm.ToggleSaveValueCommand.ExecuteAsync(savedNode);
        Assert.That(FindNodeById(vm.InstallationNodes, outputId)!.IsValueSaved, Is.False, "unticking clears persistence");
    }

    // US-033: a physical output (a wireless relay) can persist its state across power loss too.
    [Test]
    public async Task SaveCurrentValue_OnPhysicalOutput_SetsBackup()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var relayProduct = harness.Session.GetAvailableProducts()
            .First(p => p.Resources.Any(r => r.Tag == "airlink_relay"));
        await harness.Session.AddProductAsync(loc, relayProduct.ProductIdentifier);
        var relayId = FindTagged(harness.Session.Current!.Groups, "airlink_relay")!.Value;

        var ok = await harness.Session.SetOutputBackupAsync(relayId, true);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(harness.Session.Current!.FindById(relayId)!.GetAttribute("backup"), Is.EqualTo("yes"),
                "the physical output's value is restored after a power loss");
        });
    }

    // US-033b: a compatible fb↔fb variable link (block A output → block B input) is created and renders reciprocal rows.
    [Test]
    public async Task FbToFbLink_OutputToInput_Links_AndRendersReciprocalRows()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        var blocks = harness.Session.Current!.FindById(loc)!.ChildrenOrEmpty().Where(c => c.Tag == "functionblock").ToList();
        var outA = (await harness.Session.AddVariableAsync(blocks[0].FindChild("outputs")!.Id!.Value, "resource_output", "OutA"))!.Value;
        var inB = (await harness.Session.AddVariableAsync(blocks[1].FindChild("inputs")!.Id!.Value, "resource_input", "InB"))!.Value;

        var ok = await harness.Session.LinkPinsAsync(outA, inB);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(FindNodeById(vm.FunctionNodes, outA)!.Children.Any(c => c.IsLinkRow), Is.True, "the source pin shows a link row");
            Assert.That(FindNodeById(vm.FunctionNodes, inB)!.Children.Any(c => c.IsLinkRow), Is.True, "the target pin shows a reciprocal row");
        });
    }

    // US-033b: an incompatible fb↔fb link (an input as source, an output as target) is refused.
    [Test]
    public async Task FbToFbLink_IncompatibleEndpoints_Rejected()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        var blocks = harness.Session.Current!.FindById(loc)!.ChildrenOrEmpty().Where(c => c.Tag == "functionblock").ToList();
        var inA = (await harness.Session.AddVariableAsync(blocks[0].FindChild("inputs")!.Id!.Value, "resource_input", "InA"))!.Value;
        var outB = (await harness.Session.AddVariableAsync(blocks[1].FindChild("outputs")!.Id!.Value, "resource_output", "OutB"))!.Value;

        var ok = await harness.Session.LinkPinsAsync(inA, outB);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False, "an input source / output target is not a compatible endpoint pair");
            Assert.That(FindNodeById(vm.FunctionNodes, inA)!.Children.Any(c => c.IsLinkRow), Is.False, "no link is drawn");
        });
    }

    // US-033b: a variable link must cross two blocks — joining two variables of the same block is refused.
    [Test]
    public async Task FbToFbLink_WithinSameBlock_Rejected()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        var block = harness.Session.Current!.FindById(loc)!.ChildrenOrEmpty().First(c => c.Tag == "functionblock");
        var output = (await harness.Session.AddVariableAsync(block.FindChild("outputs")!.Id!.Value, "resource_output", "O"))!.Value;
        var flag = (await harness.Session.AddVariableAsync(block.FindChild("settings")!.Id!.Value, "resource_flag", "F"))!.Value;

        var ok = await harness.Session.LinkPinsAsync(output, flag);

        Assert.That(ok, Is.False, "a link must connect two different function blocks");
    }

    // US-039: project/customer/installer information is written into the project where the reports read it.
    [Test]
    public async Task ProjectInfo_WritesProjectCustomerInstaller()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var data = new ProjectInfoData("My install", "42", "Alice",
            new ContactInfo("Bob Customer", "1 Main St", "Town", "1234", "DK", "111", "222", "bob@x"),
            new ContactInfo("Eve Installer", "2 High St", "City", "5678", "DK", "333", "444", "eve@y"));

        var ok = await harness.Session.UpdateProjectInfoAsync(data);

        var readBack = harness.Session.GetProjectInfo();
        var cust = harness.Session.Current!.Child("customer_info")!;
        var inst = harness.Session.Current!.Child("installer_info")!;
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(readBack.Customer.Name, Is.EqualTo("Bob Customer"));
            Assert.That(readBack.Installer.Phone, Is.EqualTo("333"));
            Assert.That(readBack.Description, Is.EqualTo("My install"));
            Assert.That(cust.GetAttribute("address"), Is.EqualTo("1 Main St"), "report reads customer_info@address");
            Assert.That(inst.GetAttribute("name"), Is.EqualTo("Eve Installer"), "report reads installer_info@name");
            Assert.That(harness.Session.IsDirty, Is.True);
        });
    }

    // US-039: the Documentation▸Project info command prefills from the project and applies the installer's edits.
    [Test]
    public async Task ProjectInfoCommand_PrefillsCurrent_AndApplies()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.UpdateProjectInfoAsync(ProjectInfoData.Empty with { Number = "7" });
        harness.Dialogs.ProjectInfoResponder = input => input with { Customer = input.Customer with { Name = "New Customer" } };

        await vm.ProjectInfoCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastProjectInfoInput!.Number, Is.EqualTo("7"), "the dialog is prefilled from the project");
            Assert.That(harness.Session.GetProjectInfo().Customer.Name, Is.EqualTo("New Customer"), "edits are applied");
            Assert.That(vm.StatusText, Is.EqualTo("Project information updated."));
        });
    }

    private static ElementId? FindTagged(IEnumerable<ProjectElement> roots, string tag)
    {
        foreach (var e in roots)
        {
            if (e.Tag == tag && e.Id is { } id)
                return id;
            if (FindTagged(e.ChildrenOrEmpty(), tag) is { } found)
                return found;
        }
        return null;
    }

    // US-044/US-045: F1 help shows the selected element's note, or a generic message when it has none.
    [Test]
    public async Task Help_ShowsElementNote_OrGeneric()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var id = vm.InstallationNodes[0].Children[0].ElementId!.Value;   // a locality
        await harness.Session.RenameLocalityAsync(id, "Living room", "Main living area");

        await vm.HelpCommand.ExecuteAsync(FindNodeById(vm.InstallationNodes, id));
        Assert.That(harness.Dialogs.LastMessage, Is.EqualTo("Main living area"), "help shows the element's note");

        await vm.HelpCommand.ExecuteAsync(vm.InstallationNodes[0]);   // the Localities root (no element)
        Assert.That(harness.Dialogs.LastMessage, Does.Contain("No specific help"), "a note-less node shows a generic message");
    }

    // US-045: Ctrl+I / Ctrl+U insert an input / output variable into the programming block's sections.
    [Test]
    public async Task InsertInputAndOutput_InProgrammingMode_AddVariables()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        var blockId = vm.FunctionNodes[0].Children[0].Children[0].ElementId!.Value;
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);

        await vm.InsertInputCommand.ExecuteAsync(null);
        await vm.InsertOutputCommand.ExecuteAsync(null);

        var block = harness.Session.Current!.FindById(blockId)!;
        Assert.Multiple(() =>
        {
            Assert.That(block.FindChild("inputs")!.ChildrenOrEmpty().Any(c => c.Tag == "resource_input"), Is.True);
            Assert.That(block.FindChild("outputs")!.ChildrenOrEmpty().Any(c => c.Tag == "resource_output"), Is.True);
        });
    }

    // US-045: outside programming mode the insert-input shortcut is a guided no-op (nothing added).
    [Test]
    public async Task InsertInput_OutsideProgrammingMode_DoesNothing()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        await vm.InsertInputCommand.ExecuteAsync(null);

        Assert.That(vm.StatusText, Does.Contain("programming mode"), "the shortcut guides the user into programming mode");
    }

    private static TreeNodeViewModel? FindByFlag(IEnumerable<TreeNodeViewModel> nodes, Func<TreeNodeViewModel, bool> match)
    {
        foreach (var node in nodes)
        {
            if (match(node))
                return node;
            if (FindByFlag(node.Children, match) is { } found)
                return found;
        }
        return null;
    }

    private static TreeNodeViewModel? FindNodeById(IEnumerable<TreeNodeViewModel> nodes, Ihc.Vis.Model.ElementId id)
    {
        foreach (var node in nodes)
        {
            if (node.ElementId == id)
                return node;
            if (FindNodeById(node.Children, id) is { } found)
                return found;
        }
        return null;
    }

    private static ProductMenuItemViewModel FirstLeaf(IEnumerable<ProductMenuItemViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsLeaf)
                return node;
            var leaf = node.Children.Count > 0 ? FirstLeaf(node.Children) : null;
            if (leaf is not null)
                return leaf;
        }
        return null!;
    }

    [Test]
    public async Task Title_ReflectsDocumentName()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        Assert.That(vm.Title, Is.EqualTo("Untitled - IHC OpenVisual"));

        harness.Dialogs.SavePath = harness.TempPath("house.vis");
        await harness.Session.SaveAsAsync();

        Assert.That(vm.Title, Is.EqualTo("house.vis - IHC OpenVisual"));
    }

    [Test]
    public async Task ToggleToolbar_FlipsVisibility()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        Assert.That(vm.IsToolbarVisible, Is.True);

        vm.ToggleToolbarCommand.Execute(null);
        Assert.That(vm.IsToolbarVisible, Is.False);

        vm.ToggleToolbarCommand.Execute(null);
        Assert.That(vm.IsToolbarVisible, Is.True);
    }

    [Test]
    public async Task ToggleStatusBar_FlipsVisibility()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        vm.ToggleStatusBarCommand.Execute(null);

        Assert.That(vm.IsStatusBarVisible, Is.False);
    }

    [Test]
    public async Task SetTheme_UpdatesCurrentTheme()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        vm.SetThemeCommand.Execute(AppTheme.Dark);

        Assert.That(vm.CurrentTheme, Is.EqualTo(AppTheme.Dark));
    }
}
