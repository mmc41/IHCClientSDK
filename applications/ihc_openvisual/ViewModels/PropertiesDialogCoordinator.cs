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
/// <see cref="MainWindowViewModel"/> (C# 12 primary ctor). Each flow reads the element, opens its dialog through
/// <see cref="IDialogService"/>, and applies the result as a command via <paramref name="applyAndReport"/> — the
/// view-model's single outcome→status/dialog rule. The view-model keeps the node dispatch and calls these.
/// <para>Extracted in slices: this holds the locality / scene-container / scene-value / enum flows; the
/// product / pin / modem / dimmer cluster and the typed read views (which retire the dialog-DTO GetAttribute reads)
/// follow in later increments.</para>
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
        else if (element.Tag is "dataline_input" or "dataline_output")
            await OpenPinAsync(id, element);
        else if (element.Tag == "scenes")
            await OpenSceneContainerAsync(id, element);   // the product's Scenarier dialog (US-024)
        else if (element.Tag is "scene_relay" or "scene_dimmer")
            await OpenSceneValueAsync(id, element);   // edit a scenario link's value (US-058)
        else if (element.Kind == ElementKind.EnumResource)
            await OpenEnumAsync(id);   // edit the enum type's states (US-030)
        else if (element.Tag is "group" or "functionblock")
            // A function block renames through the same Name/Note dialog as a locality (US-007/US-019).
            await OpenLocalityAsync(id, View(element).Name ?? string.Empty);
    }

    /// <summary>Renames a locality or function block through the shared Name/Note dialog (US-007/US-019).</summary>
    public async Task OpenLocalityAsync(ElementId id, string currentName)
    {
        string currentNote = session.Current?.FindById(id)?.GetAttribute("note") ?? string.Empty;
        PropertiesResult? result = await dialogs.EditPropertiesAsync($"Edit {currentName} properties", currentName, currentNote);
        if (result is null)
            return;   // cancelled — the locality keeps its original name and note
        await applyAndReport(new RenameLocality(id, result.Name, result.Note), $"Renamed to {result.Name}.");
    }

    // The product's scene container (US-024): its fixed name, its note, and a row per membership naming the
    // scenario, the function block driving it and that block's locality — the same triple the membership's link row
    // shows as a path, split into columns.
    public async Task OpenSceneContainerAsync(ElementId scenesId, ProjectElement scenes)
    {
        var rows = new List<SceneContainerRow>();
        foreach (ProjectElement member in scenes.ChildrenOrEmpty())
        {
            if (!IsSceneMember(member.Tag))
                continue;
            IReadOnlyList<string> parts = LinkOppositeParts(member);
            (string value, string ramp) = SceneMemberValue(member);
            rows.Add(new SceneContainerRow(
                SceneName: parts.Count > 2 ? parts[2] : string.Empty,
                FunctionBlock: parts.Count > 1 ? parts[1] : string.Empty,
                Locality: parts.Count > 0 ? parts[0] : string.Empty,
                Value: value, RampTime: ramp));
        }
        string name = scenes.GetAttribute("name") ?? "Scenarier";
        SceneContainerResult? result = await dialogs.EditSceneContainerAsync(
            new SceneContainerInput(name, scenes.GetAttribute("note") ?? string.Empty, rows));
        if (result is null)
            return;
        await applyAndReport(new UpdateSceneContainer(scenesId, result.Note), $"'{name}' updated.");
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
        await applyAndReport(new UpdateSceneValue(memberId, result), "Scene value updated.");
    }

    public async Task OpenEnumAsync(ElementId enumVariableId)
    {
        if (ReadEnumInfo(enumVariableId) is not { } info)
            return;
        EnumDefinitionResult? result = await dialogs.EditEnumDefinitionAsync(
            new EnumDefinitionInput($"Edit {info.Name}", info.Name, info.States, IsNew: false));
        if (result is null)
            return;
        if (session.BuildUpdateEnumStates(enumVariableId, result.States) is { } command)
            await applyAndReport(command, $"Enumerator '{info.Name}' updated.");
    }

    // Reads an enum variable's type name and ordered state names for the Edit dialog (US-030); null if not an enum.
    private (string Name, List<string> States)? ReadEnumInfo(ElementId enumVariableId)
    {
        if (session.Current is not { } project || project.FindById(enumVariableId) is not { Tag: "resource_enum" } variable
            || !ElementId.TryParse(variable.GetAttribute("typedef"), out ElementId defId)
            || project.FindById(defId) is not { } def)
        {
            return null;
        }
        var states = def.ChildrenOrEmpty().Where(c => c.Tag == "enum_value")
            .Select(c => c.GetAttribute("name") ?? string.Empty).ToList();
        return (def.GetAttribute("name") ?? string.Empty, states);
    }

    // The value-carrying rows inside a product's scenes container — its memberships of the scenarios FBs drive.
    private static bool IsSceneMember(string tag) => tag is "scene_relay" or "scene_dimmer" or "scene_shutter";

    // A scene membership's stored value and, for a dimmer, its ramp time — the two columns the scene-container
    // dialog shows separately.
    private static (string Value, string RampTime) SceneMemberValue(ProjectElement member)
    {
        if (!SceneValue.TryParse(member, out SceneValue sv))
            return (string.Empty, string.Empty);
        return sv.Kind switch
        {
            SceneValueKind.Relay => (sv.On ? "ON" : "OFF", string.Empty),
            SceneValueKind.Dimmer => ($"{sv.LevelPercent}%", $"{sv.RampTime.TotalSeconds:0.#}s"),
            SceneValueKind.Shutter => (sv.ShutterUp ? "up" : "down", string.Empty),
            _ => (string.Empty, string.Empty),
        };
    }

    // A product's tree label carries its placement descriptor "name (position) " (F-003) — reproduced so the
    // scene-container dialog names a product exactly as the Installation pane does.
    private static string ProductLabel(string name, string? position) =>
        string.IsNullOrEmpty(position) ? name : $"{name} ({position}) ";

    // The opposite end's path parts, outermost first: [locality, product-or-block, pin]. Empty when unresolvable.
    private IReadOnlyList<string> LinkOppositeParts(ProjectElement linkRow)
    {
        if (session.Current is not { } project
            || !ElementId.TryParse(linkRow.GetAttribute("link"), out ElementId partnerId)
            || project.FindParent(partnerId) is not { } oppositePin)
        {
            return Array.Empty<string>();
        }
        var parts = new List<string>();
        ProjectElement? current = oppositePin;
        bool leaf = true;
        while (current is not null)
        {
            bool significant = leaf || current.Tag is "group" or "functionblock" || ProductClassifier.IsProduct(current.Tag);
            if (significant && View(current).Name is { Length: > 0 } partName)
                parts.Insert(0, ProductClassifier.IsProduct(current.Tag)
                    ? ProductLabel(partName, View(current).Position)
                    : partName);
            current = current.Id is { } cid ? project.FindParent(cid) : null;
            leaf = false;
        }
        return parts;
    }

    private async Task OpenModemAsync(ElementId modemId)
    {
        if (session.Current is not { } project || project.FindById(modemId) is not { } modem)
            return;
        var localities = new List<LocalityChoice>();
        foreach (ProjectElement group in project.Groups)
        {
            if (group.Id is { } gid)
                localities.Add(new LocalityChoice(gid.ToToken(), group.GetAttribute("name") ?? string.Empty));
        }
        string currentLocalityId = project.FindParent(modemId)?.Id?.ToToken() ?? string.Empty;
        var phones = new List<string>();
        for (int slot = 1; slot <= 4; slot++)
        {
            string s = slot.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ProjectElement? pn = modem.DescendantsAndSelf()
                .FirstOrDefault(e => e.Tag == "sms_modem_phonenumber" && e.GetAttribute("address") == s);
            phones.Add(pn?.GetAttribute("phonenumber") ?? string.Empty);
        }
        string pin = modem.DescendantsAndSelf().FirstOrDefault(e => e.Tag == "sms_modem_pincode")?.GetAttribute("value") ?? string.Empty;
        if (pin == "0")
            pin = string.Empty;   // the DTD default reads as blank in the dialog

        var input = new ModemPropertiesInput(
            "SMS modem properties",
            modem.GetAttribute("name") ?? string.Empty,
            modem.GetAttribute("note") ?? string.Empty,
            modem.GetAttribute("documentation_tag") ?? string.Empty,
            modem.GetAttribute("cablecolour_0V") ?? string.Empty,
            modem.GetAttribute("cablecolour_24V") ?? string.Empty,
            modem.GetAttribute("cablecolour_RS485Minus") ?? string.Empty,
            modem.GetAttribute("cablecolour_RS485Plus") ?? string.Empty,
            pin, phones, localities, currentLocalityId);

        ModemPropertiesResult? result = await dialogs.EditModemPropertiesAsync(input);
        if (result is null)
            return;
        await applyAndReport(session.BuildUpdateModem(modemId, result), $"Updated {result.Name}.");
    }

    private async Task OpenPinAsync(ElementId pinId, ProjectElement pin)
    {
        bool isOutput = pin.Tag == "dataline_output";
        int dataLine = 1, terminal = 0;
        if (DatalineAddress.TryParse(pin.GetAttribute("address_dataline"), isOutput, out DatalineAddress addr))
            (dataLine, terminal) = (addr.DataLine, addr.Terminal);
        var input = new PinPropertiesInput(
            $"{(isOutput ? "Output" : "Input")} '{pin.GetAttribute("name")}'",
            isOutput, dataLine, terminal,
            pin.GetAttribute("cable_colour") ?? string.Empty,
            pin.GetAttribute("note") ?? string.Empty,
            View(pin).InitialValue == "on",
            InUseTerminals(isOutput, pinId));

        PinPropertiesResult? result = await dialogs.EditPinPropertiesAsync(input);
        if (result is null)
            return;   // cancelled — the pin keeps its addressing
        // A bespoke failure message (invalid address) rather than the generic mapping, so read the outcome directly.
        EditOutcome outcome = await session.ApplyAsync(new UpdatePin(pinId, result));
        setStatus(outcome.Status == EditStatus.Committed
            ? $"Addressed {pin.GetAttribute("name")} to data line {result.DataLine}, terminal {result.Terminal}."
            : $"Data line {result.DataLine}, terminal {result.Terminal} is not a valid address.");
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
                && DatalineAddress.TryParse(element.GetAttribute("address_dataline"), isOutput, out DatalineAddress a))
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
            var localities = new List<LocalityChoice>();
            foreach (ProjectElement group in project.Groups)
            {
                if (group.Id is { } gid)
                    localities.Add(new LocalityChoice(gid.ToToken(), group.GetAttribute("name") ?? string.Empty));
            }
            string currentLocalityId = project.FindParent(productId)?.Id?.ToToken() ?? string.Empty;
            // The dialog is titled with the product TYPE (the catalog name), not the generic "Product properties" —
            // it is how the vendor tells two open product dialogs apart (A-8/F-015).
            string productType = session.GetAvailableProducts()
                .FirstOrDefault(p => p.ProductIdentifier == product.GetAttribute("product_identifier"))?.DisplayName
                ?? product.GetAttribute("name") ?? "Product properties";
            var input = new ProductPropertiesInput(
                productType,
                product.GetAttribute("name") ?? string.Empty,
                product.GetAttribute("note") ?? string.Empty,
                product.GetAttribute("cabletype") ?? string.Empty,
                product.GetAttribute("cablenumber") ?? string.Empty,
                product.GetAttribute("documentation_tag") ?? string.Empty,
                product.GetAttribute("power_group") ?? string.Empty,
                localities, currentLocalityId, ProductClassifier.IsWireless(product.Tag), IsWirelessDimmer(product),
                BuildTerminals(product), product.GetAttribute("position") ?? string.Empty,
                // A locked (library) product's name is fixed to the catalog type name — greyed out (A-15/F-032).
                // Read locked off the ELEMENT, resolved via the project's inline DTD (default "no"); never a catalog
                // lookup (whose default is "yes" and would grey the wrong products).
                NameLocked: View(product).Locked,
                EndUserReport: View(product).EnduserReport);

            ProductPropertiesResult? result = await dialogs.EditProductPropertiesAsync(input);
            if (result is null)
                return;   // cancelled — the product keeps its documentation
            await applyAndReport(session.BuildUpdateProduct(productId, result), $"Updated {result.Name}.");
            if (result.ConfigureTerminalPinId is { } pinToken && ElementId.TryParse(pinToken, out ElementId pinId)
                && session.Current?.FindById(pinId) is { Tag: "dataline_input" or "dataline_output" } pinEl)
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
    // vendor-formatted "Datalinie N.PP" address (blank when unassigned), cable colour and note. The SDK owns the
    // address decode (DatalineAddress) — the coordinator only formats the row.
    private static IReadOnlyList<ProductTerminal> BuildTerminals(ProjectElement product)
    {
        var terminals = new List<ProductTerminal>();
        foreach (ProjectElement t in product.DescendantsAndSelf().Where(e => e.Tag is "dataline_input" or "dataline_output"))
        {
            bool isOutput = t.Tag == "dataline_output";
            string label = DatalineAddress.ToVendorLabel(t.GetAttribute("address_dataline"), isOutput);
            terminals.Add(new ProductTerminal(
                t.GetAttribute("name") ?? string.Empty,
                label == "?" ? string.Empty : $"Datalinie {label}",
                t.GetAttribute("cable_colour") ?? string.Empty,
                t.GetAttribute("note") ?? string.Empty,
                isOutput,
                t.Id?.ToToken() ?? string.Empty));
        }
        return terminals;
    }

    private static bool IsWirelessDimmer(ProjectElement product) =>
        ProductClassifier.IsWireless(product.Tag) && product.DescendantsAndSelf().Any(e => e.Tag == "dimmer_settings");

    private async Task OpenAdvancedDimmerAsync(ElementId productId)
    {
        if (session.Current is not { } project || project.FindById(productId) is not { } product)
            return;
        // The fallbacks (700/700/2/0/100) are the vendor's FACTORY new-device defaults, NOT the DTD defaults: the
        // schema default for a dimmer_setting `value` is "0" (load_mode "auto"), so Effective returns 0 for an unset
        // device and the `v > 0` guard applies these constants. They stay app-side by design (fablerefac W1-3 finding).
        int Read(string tag, int fallback)
        {
            ProjectElement? el = product.DescendantsAndSelf().FirstOrDefault(e => e.Tag == tag);
            return el is not null && int.TryParse(project.View(el).Effective("value"), out int v) && v > 0 ? v : fallback;
        }
        ProjectElement? loadModeEl = product.DescendantsAndSelf().FirstOrDefault(e => e.Tag == "dimmer_setting_load_mode");
        string loadMode = loadModeEl is { } lm && project.View(lm).Effective("value") is { } mode ? mode : "auto";
        var input = new AdvancedDimmerInput(
            Read("dimmer_setting_fade_rate_up", 700),
            Read("dimmer_setting_fade_rate_down", 700),
            Read("dimmer_setting_dimming_rate", 2),
            Read("dimmer_setting_minimum_value", 0),
            Read("dimmer_setting_maximum_value", 100),
            loadMode);

        AdvancedDimmerResult? result = await dialogs.EditAdvancedDimmerAsync(input);
        if (result is null)
            return;
        await applyAndReport(new UpdateDimmerSettings(productId, result), "Updated dimmer settings.");
    }
}
