using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc.Vis;
using Ihc.Vis.Addressing;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// fablerefac W3-8: the per-node-type <i>Properties</i> dialog flows, extracted from
/// <see cref="MainWindowViewModel"/> (C# 12 primary ctor). <see cref="OpenAsync"/> is the node dispatch; each flow
/// reads the element through a typed SDK read view (<see cref="PinView"/>/<see cref="ProductView"/>/
/// <see cref="ModemView"/>/<see cref="DimmerView"/> or <see cref="ElementView"/>), opens its dialog through
/// <see cref="IDialogService"/>, and applies the result as a command via <paramref name="applyAndReport"/> — the
/// view-model's single outcome→status/dialog rule (<paramref name="setStatus"/> serves the one flow, pin
/// addressing, that reports a bespoke message). No raw schema attribute reads remain in this layer.
/// </summary>
internal sealed class PropertiesDialogCoordinator(
    ProjectWorkflow session,
    IDialogService dialogs,
    Func<ProjectCommand, string, Task> applyAndReport,
    Action<string> setStatus)
{
    // Read element attributes through the SDK read surface; the element always belongs to the open project.
    private ElementView View(ProjectElement element) => session.Current!.View(element);

    /// <summary>Opens the properties dialog appropriate to the element's type (the node dispatch, US-044). A modem, a
    /// product, a data-line pin, a scenes container, a scene value, an enum variable, and a locality/function block
    /// each route to their own flow.</summary>
    public async Task OpenAsync(ElementId id)
    {
        if (session.Current is not { } project || project.FindById(id) is not { } element)
            return;
        if (ProductClassifier.IsModem(element.Tag))
            await OpenModemAsync(id);
        else if (ProductClassifier.IsProduct(element.Tag))
            await OpenProductAsync(id);
        else if (element.Kind == ElementKind.DatalinePin)
            await OpenPinAsync(id, element);
        else if (element.IsScenesContainer)
            await OpenSceneContainerAsync(id, element);   // the product's Scenarier dialog (US-024)
        else if (element.IsSceneMember && !element.IsSceneShutter)
            await OpenSceneValueAsync(id, element);   // edit a scenario link's value (US-058)
        else if (element.Kind == ElementKind.EnumResource)
            await OpenEnumAsync(id);   // edit the enum type's states (US-030)
        else if (element.IsLocalityGroup || element.Kind is ElementKind.FunctionBlock)
            // A function block renames through the same Name/Note dialog as a locality (US-007/US-019).
            await OpenLocalityAsync(id, View(element).Name ?? string.Empty);
    }

    /// <summary>Renames a locality or function block through the shared Name/Note dialog (US-007/US-019).</summary>
    public async Task OpenLocalityAsync(ElementId id, string currentName)
    {
        if (session.Current is not { } project)
            return;
        string currentNote = project.FindById(id) is { } locality ? View(locality).Note ?? string.Empty : string.Empty;
        PropertiesResult? result = await dialogs.EditPropertiesAsync($"Edit {currentName} properties", currentName, currentNote);
        if (result is null)
            return;   // cancelled — the locality keeps its original name and note
        await applyAndReport(session.Commands.RenameLocality(project, id, result.Name, result.Note), $"Renamed to {result.Name}.");
    }

    // The product's scene container (US-024): its fixed name, its note, and a row per membership naming the
    // scenario, the function block driving it and that block's locality — the same triple the membership's link row
    // shows as a path, split into columns.
    public async Task OpenSceneContainerAsync(ElementId scenesId, ProjectElement scenes)
    {
        var rows = new List<SceneContainerRow>();
        foreach (ProjectElement member in scenes.ChildrenOrEmpty())
        {
            if (!member.IsSceneMember)
                continue;
            IReadOnlyList<string> parts = TreeLabelFormatter.LinkOppositeParts(session.Current!, member);
            (string value, string ramp) = TreeLabelFormatter.SceneMemberValue(member);
            rows.Add(new SceneContainerRow(
                SceneName: parts.Count > 2 ? parts[2] : string.Empty,
                FunctionBlock: parts.Count > 1 ? parts[1] : string.Empty,
                Locality: parts.Count > 0 ? parts[0] : string.Empty,
                Value: value, RampTime: ramp));
        }
        ElementView scenesView = View(scenes);
        string name = scenesView.Name ?? "Scenarier";
        SceneContainerResult? result = await dialogs.EditSceneContainerAsync(
            new SceneContainerInput(name, scenesView.Note ?? string.Empty, rows));
        if (result is null)
            return;
        await applyAndReport(session.Commands.UpdateSceneContainer(session.Current!, scenesId, result.Note), $"'{name}' updated.");
    }

    public async Task OpenSceneValueAsync(ElementId memberId, ProjectElement member)
    {
        if (!SceneValue.TryParse(member, out SceneValue sv))
            return;
        bool isDimmer = sv.Kind == SceneValueKind.Dimmer;
        int ms = (int)sv.RampTime.TotalMilliseconds;
        var input = new SceneValueInput("Scene value", isDimmer, sv.On, sv.LevelPercent, ms / 60000, ms / 1000 % 60);

        SceneValueResult? result = await dialogs.EditSceneValueAsync(input);
        if (result is null)
            return;
        await applyAndReport(session.Commands.UpdateSceneValue(session.Current!, memberId, result), "Scene value updated.");
    }

    public async Task OpenEnumAsync(ElementId enumVariableId)
    {
        if (ReadEnumInfo(enumVariableId) is not { } info)
            return;
        EnumDefinitionResult? result = await dialogs.EditEnumDefinitionAsync(
            new EnumDefinitionInput($"Edit {info.Name}", info.Name, info.States, IsNew: false));
        if (result is null)
            return;
        if (session.Commands.UpdateEnumStates(session.Current!, enumVariableId, result.States) is { } command)
            await applyAndReport(command, $"Enumerator '{info.Name}' updated.");
    }

    // Reads an enum variable's type name and ordered state names for the Edit dialog (US-030); null if not an enum.
    private (string Name, List<string> States)? ReadEnumInfo(ElementId enumVariableId)
    {
        if (session.Current is not { } project || project.FindById(enumVariableId) is not { } variable
            || variable.Kind != ElementKind.EnumResource
            || !ElementId.TryParse(project.View(variable).Effective("typedef"), out ElementId defId)
            || project.FindById(defId) is not { } def)
        {
            return null;
        }
        var states = def.ChildrenOrEmpty().Where(c => c.IsEnumValue)
            .Select(c => project.View(c).Name ?? string.Empty).ToList();
        return (project.View(def).Name ?? string.Empty, states);
    }

    private async Task OpenModemAsync(ElementId modemId)
    {
        if (session.Current is not { } project || project.FindById(modemId) is not { } modem)
            return;
        var view = new ModemView(project, modem);
        List<LocalityChoice> localities = BuildLocalityChoices(project);
        string currentLocalityId = project.FindParent(modemId)?.Id?.ToToken() ?? string.Empty;
        string pin = view.PinCode ?? string.Empty;
        if (pin == "0")
            pin = string.Empty;   // the DTD default reads as blank in the dialog

        var input = new ModemPropertiesInput(
            "SMS modem properties",
            view.Name ?? string.Empty,
            view.Note ?? string.Empty,
            view.DocumentationTag ?? string.Empty,
            view.CableColour0V ?? string.Empty,
            view.CableColour24V ?? string.Empty,
            view.CableColourRS485Minus ?? string.Empty,
            view.CableColourRS485Plus ?? string.Empty,
            pin, view.PhoneNumbers, localities, currentLocalityId);

        ModemPropertiesResult? result = await dialogs.EditModemPropertiesAsync(input);
        if (result is null)
            return;
        await applyAndReport(session.Commands.UpdateModem(project, modemId, result), $"Updated {result.Name}.");
    }

    private async Task OpenPinAsync(ElementId pinId, ProjectElement pin)
    {
        var view = new PinView(session.Current!, pin);
        bool isOutput = view.IsOutput;
        (int dataLine, int terminal) = view.Address is { } addr ? (addr.DataLine, addr.Terminal) : (1, 0);
        var input = new PinPropertiesInput(
            $"{(isOutput ? "Output" : "Input")} '{view.Name}'",
            isOutput, dataLine, terminal,
            view.CableColour ?? string.Empty,
            view.Note ?? string.Empty,
            view.InitialValueOn,
            InUseTerminals(isOutput, pinId));

        PinPropertiesResult? result = await dialogs.EditPinPropertiesAsync(input);
        if (result is null)
            return;   // cancelled — the pin keeps its addressing
        // A bespoke failure message (invalid address) rather than the generic mapping, so read the outcome directly.
        EditOutcome outcome = await session.ApplyAsync(session.Commands.UpdatePin(session.Current!, pinId, result));
        setStatus(outcome.Status == EditStatus.Committed
            ? $"Addressed {view.Name} to data line {result.DataLine}, terminal {result.Terminal}."
            : $"Data line {result.DataLine}, terminal {result.Terminal} is not a valid address.");
    }

    // The localities offered as re-parent choices in the product/modem dialogs.
    private static List<LocalityChoice> BuildLocalityChoices(Project project)
    {
        var localities = new List<LocalityChoice>();
        foreach (ProjectElement group in project.Groups)
        {
            if (group.Id is { } gid)
                localities.Add(new LocalityChoice(gid.ToToken(), project.View(group).Name ?? string.Empty));
        }
        return localities;
    }

    // The line.terminal addresses already used by other pins of the same direction (US-012 in-use indication).
    private IReadOnlyList<string> InUseTerminals(bool isOutput, ElementId except)
    {
        var used = new List<string>();
        if (session.Current is not { } project)
            return used;
        string tag = isOutput ? "dataline_output" : "dataline_input";
        foreach (ProjectElement element in project.Root.DescendantsAndSelf())
        {
            if (element.Tag == tag && element.Id is { } eid && eid != except
                && new PinView(project, element).Address is { } a)
            {
                used.Add($"{a.DataLine}.{a.Terminal}");
            }
        }
        return used;
    }

    // The product documentation dialog (US-011) plus its terminal-addressing grids (US-012). Re-entrant: choosing to
    // configure a terminal applies the documentation, opens the addressing sub-dialog for that terminal, then re-opens
    // this dialog — the vendor's in-place "Konfigurer indgang/udgang" flow.
    private async Task OpenProductAsync(ElementId productId)
    {
        while (true)
        {
            if (session.Current is not { } project || project.FindById(productId) is not { } product)
                return;
            var view = new ProductView(project, product);
            List<LocalityChoice> localities = BuildLocalityChoices(project);
            string currentLocalityId = project.FindParent(productId)?.Id?.ToToken() ?? string.Empty;
            // The dialog is titled with the product TYPE (the catalog name), not the generic "Product properties" —
            // it is how the vendor tells two open product dialogs apart (A-8/F-015).
            string productType = session.GetAvailableProducts()
                .FirstOrDefault(p => p.ProductIdentifier == view.ProductIdentifier)?.DisplayName
                ?? view.Name ?? "Product properties";
            var input = new ProductPropertiesInput(
                productType,
                view.Name ?? string.Empty,
                view.Note ?? string.Empty,
                view.CableType ?? string.Empty,
                view.CableNumber ?? string.Empty,
                view.DocumentationTag ?? string.Empty,
                view.PowerGroup ?? string.Empty,
                localities, currentLocalityId, view.IsWireless, view.IsWirelessDimmer,
                BuildTerminals(view), view.Position ?? string.Empty,
                // A locked (library) product's name is fixed to the catalog type name — greyed out (A-15/F-032).
                // Read locked off the ELEMENT, resolved via the project's inline DTD (default "no"); never a catalog
                // lookup (whose default is "yes" and would grey the wrong products).
                NameLocked: view.Locked,
                EndUserReport: view.EnduserReport);

            ProductPropertiesResult? result = await dialogs.EditProductPropertiesAsync(input);
            if (result is null)
                return;   // cancelled — the product keeps its documentation
            await applyAndReport(session.Commands.UpdateProduct(project, productId, result), $"Updated {result.Name}.");
            if (result.ConfigureTerminalPinId is { } pinToken && ElementId.TryParse(pinToken, out ElementId pinId)
                && session.Current?.FindById(pinId) is { } pinEl && pinEl.Kind == ElementKind.DatalinePin)
            {
                await OpenPinAsync(pinId, pinEl);
                continue;   // re-open the product dialog after addressing the terminal (US-012)
            }
            if (result.OpenAdvanced)
                await OpenAdvancedDimmerAsync(productId);   // Properties ▸ Advanced (US-015)
            return;
        }
    }

    // The product's input/output terminals for the addressing grids (US-012): each terminal's name, its
    // vendor-formatted "Datalinie N.PP" address (blank when unassigned), cable colour and note. The typed PinView
    // owns the reads + address decode — the coordinator only formats the row.
    private static IReadOnlyList<ProductTerminal> BuildTerminals(ProductView product)
    {
        var terminals = new List<ProductTerminal>();
        foreach (PinView t in product.Terminals)
        {
            string label = DatalineAddress.ToVendorLabel(t.AddressToken, t.IsOutput);
            terminals.Add(new ProductTerminal(
                t.Name ?? string.Empty,
                label == "?" ? string.Empty : $"Datalinie {label}",
                t.CableColour ?? string.Empty,
                t.Note ?? string.Empty,
                t.IsOutput,
                t.Id?.ToToken() ?? string.Empty));
        }
        return terminals;
    }

    private async Task OpenAdvancedDimmerAsync(ElementId productId)
    {
        if (session.Current is not { } project || project.FindById(productId) is not { } product)
            return;
        // The fallbacks (700/700/2/0/100) are the vendor's FACTORY new-device defaults, NOT the DTD defaults: the
        // schema default for a dimmer_setting `value` is "0", so an unset device reads 0 and PositiveSetting returns
        // null, letting the `?? factory` here apply the constant (fablerefac W1-3 finding — kept app-side).
        var view = new DimmerView(project, product);
        var input = new AdvancedDimmerInput(
            view.PositiveSetting("dimmer_setting_fade_rate_up") ?? 700,
            view.PositiveSetting("dimmer_setting_fade_rate_down") ?? 700,
            view.PositiveSetting("dimmer_setting_dimming_rate") ?? 2,
            view.PositiveSetting("dimmer_setting_minimum_value") ?? 0,
            view.PositiveSetting("dimmer_setting_maximum_value") ?? 100,
            view.LoadMode);

        AdvancedDimmerResult? result = await dialogs.EditAdvancedDimmerAsync(input);
        if (result is null)
            return;
        await applyAndReport(session.Commands.UpdateDimmerSettings(project, productId, result), "Updated dimmer settings.");
    }
}
