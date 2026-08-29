using System;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// The dialog leg of a route: whose dialog opens, which element carries the value, and which field to land on.
/// <para>The pair is what encodes the sub-dialog decision. <c>Site</c> equal to <c>Owner</c> means the value
/// lives on the owner's own dialog; <c>Site</c> differing means the owner's dialog has to select that sub-item
/// first. <c>Attribute</c> null means open without focusing anything in particular.</para>
/// </summary>
/// <param name="Owner">The element whose properties dialog opens — the coordinator decides WHICH dialog that is.</param>
/// <param name="Site">The element carrying the value; may equal <paramref name="Owner"/>.</param>
/// <param name="Attribute">The attribute to focus, or null to open the dialog plain.</param>
public sealed record DialogHop(ElementId Owner, ElementId Site, string? Attribute);

/// <summary>
/// One finding's route: where the tree goes, which dialog (if any) follows, and what the row may PROMISE.
/// <para><see cref="Kind"/> lives on the plan rather than being computed beside it. That is what makes the
/// row's tooltip and its activation literally the same value instead of two derivations that can disagree.</para>
/// </summary>
/// <param name="Reveal">The tree node to select — the site itself, or the nearest ancestor that has a row.</param>
/// <param name="Dialog">The dialog leg, or null for a tree-only route.</param>
/// <param name="Kind">What the row may claim before the click.</param>
/// <param name="Host">
/// A window of the HOST's own, for a finding that names no element to derive one from.
/// <para>Separate from <paramref name="Dialog"/> because that leg is addressed by element, and these findings
/// have none: <i>every masthead is blank</i> is about the project, not about a thing in it.</para>
/// </param>
public sealed record NavigationPlan(
    ElementId? Reveal, DialogHop? Dialog, NavigationKind Kind, HostRoute Host = HostRoute.None);

/// <summary>
/// The host windows a whole-project finding can be taken to (T046).
/// <para>Keyed by CODE at the planner, which is what an id is for — a grouping key. These findings carry no
/// element, so there is nothing else to route from.</para>
/// </summary>
public enum HostRoute
{
    /// <summary>No window: the finding is real, and nothing in the application repairs it in one place.</summary>
    None,

    /// <summary>The project-information dialog, which opens on the first masthead field.</summary>
    ProjectInfo,
}

/// <summary>
/// Turns a finding into a route. The single route-capability resolver: the panel asks it for a KIND when the
/// result binds and for a PLAN when a row is activated, so what a row promises and where it lands are one
/// answer.
///
/// <para><b>Pure and Avalonia-free.</b> Its reads arrive as injected functions rather than being fetched, which
/// is what keeps it a table-driven unit-test surface — a test supplies the row-bearing map it wants to reason
/// about instead of building a tree to imply one.</para>
///
/// <para><b>It replicates the projector's PREDICATE, never the projector's output.</b> Asking the live tree
/// would be wrong twice: the panel holds a project snapshot and no tree, and row presence is mode-dependent —
/// an element inside a block's program has no configuration-tree row at all, and the reveal CREATES one by
/// entering programming mode. A planner that asked the tree as it stands would call every logic finding
/// row-less, fall back to its block, and skip the mode switch that already works.</para>
/// </summary>
/// <param name="hasRow">Whether the tree draws a row for this element.</param>
/// <param name="nearestRowBearingAncestor">The nearest strict ancestor that has one, or null.</param>
/// <param name="composeProductDialog">
/// The product dialog a product would show — the authority on which <c>(element, attribute)</c> pairs are
/// writable fields. Composing it is the cost of never claiming a field that is not there.
/// </param>
public sealed class ProblemNavigationPlanner(
    Func<Project, ElementId, bool> hasRow,
    Func<Project, ElementId, ElementId?> nearestRowBearingAncestor,
    Func<Project, ElementId, ProductDialogDescriptor> composeProductDialog)
{
    /// <summary>
    /// The planner the shell uses: the projector's own predicates — which own the row-bearing ladder — over
    /// whatever door the caller has to the product dialog.
    /// <para>The compose function is the caller's because the GUI has no door of its own to the composer: the
    /// descriptor is the SDK's to build, and the shell reaches it through the app service.</para>
    /// </summary>
    /// <param name="composeProductDialog">The SDK's product-dialog compose, as the shell can reach it.</param>
    public static ProblemNavigationPlanner Over(
        Func<Project, ElementId, ProductDialogDescriptor> composeProductDialog) =>
        new(ProjectTreeProjector.HasRow, ProjectTreeProjector.NearestRowBearingAncestor, composeProductDialog);

    /// <summary>The route for one finding.</summary>
    /// <param name="project">The snapshot the finding was produced against.</param>
    /// <param name="site">The finding's primary element, or null when it names none.</param>
    /// <param name="targetAttribute">The attribute the finding's entry declares, or null.</param>
    /// <param name="code">The finding's code — used only where a family is routed as a family.</param>
    /// <param name="fix">
    /// This occurrence's own fix location, when the rule gave one. It WINS over the other two: a row whose
    /// attribute differs per occurrence, or whose repair is on a child of the element the reader was shown,
    /// can only be routed by what the emission said, and the declaration is the weaker statement precisely
    /// because it has to hold for every occurrence.
    /// </param>
    public NavigationPlan Plan(
        Project project, ElementId? site, string? targetAttribute, ProblemCode code, FixLocation? fix = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        // Resolved ONCE, here, rather than by each caller: the row's promise and the activation's route are the
        // same call, so a preference applied at one of them and not the other is exactly the disagreement this
        // class exists to prevent.
        if (fix is { } at)
        {
            site = at.Element;
            targetAttribute = at.Attribute ?? targetAttribute;
        }

        // No element to route from. Three different findings arrive here and they are NOT the same fact: a
        // whole-project row that never named one, a row whose id was malformed, and a duplicate-id row whose
        // anchor the panel dropped because the token names two elements. None of them can be navigated, and
        // pretending otherwise would send the user somewhere the finding was not about.
        if (site is not { } id)
        {
            // A whole-project finding names no element, so the only thing left to route from is its CODE.
            // The table is small and literal on purpose: it is the host's own answer about the host's own
            // windows, and the GUI may not read the catalogue to ask.
            return HostRouteFor(code) is { } host
                ? new NavigationPlan(null, null, NavigationKind.Dialog, host)
                : Nowhere;
        }

        // Gone since the run. A finding is a fact as of the validation it came from, so an element deleted
        // afterwards is an ordinary state rather than an error — the route degrades and the activation says so.
        if (project.FindById(id) is not { } element)
        {
            return Nowhere;
        }

        // THE LINK FAMILY IS TREE-ONLY, and it is an exception rather than a shade of the pin route below.
        // Its fix is a link gesture on the tree row; a modal the user has to dismiss first is a detour, not a
        // shortcut. Without this the generic pin class would open the product dialog with the terminal row
        // selected and stack nothing — a plausible-looking route to the wrong kind of repair.
        if (targetAttribute is null && IsLinkFamily(code) && hasRow(project, id))
        {
            return new NavigationPlan(id, null, NavigationKind.Tree);
        }

        // A data-line pin: the pin has a tree row, but the PRODUCT owns the dialog, so the reveal lands on the
        // product and the dialog selects the terminal row — the same pin→product redirect the tree's own
        // Egenskaber gesture makes.
        if (element.Kind == ElementKind.DatalinePin && OwningProduct(project, id) is { } owner)
        {
            // A terminal's values are the TERMINAL EDITOR's fields, not the product dialog's — the product
            // dialog shows terminals as grid ROWS, so asking its descriptor whether it offers `cable_colour`
            // answers no for every terminal attribute there is. The capability question for this route is
            // whether the editor the route ends in has a field for the attribute.
            return DialogRoute(
                owner, owner, id, targetAttribute,
                PropertiesDialogCoordinator.PinFieldFor(targetAttribute) is not null);
        }

        if (ProductClassifier.IsProduct(element.Tag))
        {
            return ProductRoute(project, id, id, targetAttribute);
        }

        // THE SCENES, before the inside-a-product class below — a scenes container and its members live under a
        // product, so that class would otherwise claim them and open the product's dialog, which is not where a
        // scene value is edited. Both are drawn in the tree (§2.4), so the reveal is the element itself.
        //
        // A SHUTTER member is excluded: no dialog edits one, so it keeps its tree row and nothing more, which is
        // the same judgement the node dispatch already makes.
        if (element.IsScenesContainer || (element.IsSceneMember && !element.IsSceneShutter))
        {
            // The DIALOG is the element's either way; only the reveal depends on whether the tree draws it. An
            // empty Scenarier container has no row, and revealing an element the tree has no node for is a
            // dead end — which would have swallowed the dialog with it.
            return DialogRoute(
                hasRow(project, id) ? id : nearestRowBearingAncestor(project, id),
                id, id, targetAttribute,
                PropertiesDialogCoordinator.SceneFieldFor(targetAttribute) is not null);
        }

        // A flagged SETTING inside a product: the constant behind an Indstillinger row (T047). Before the
        // generic class below, and for exactly the reason the terminals needed their own class — a setting is a
        // grid ROW, not a descriptor field, so asking the product dialog whether it offers `inivalue` answers no
        // and every settings route degraded to dialog-level. The capability question is the editor's.
        if (ProductRows.IsSetting(element.GetAttribute(ProductRows.SettingAttribute))
            && OwningProduct(project, id) is { } settingOwner)
        {
            return DialogRoute(
                settingOwner, settingOwner, id, targetAttribute,
                PropertiesDialogCoordinator.ConstantFieldFor(targetAttribute));
        }

        // ANY OTHER element inside a product: its values are that product's dialog, whether or not the tree draws
        // a row for it. A modem's telephone slots and a dimmer's settings both live here — the first are fields
        // of the product dialog, the second are reached through it — and both would otherwise fall to the
        // ancestor fallback below, which lands in the right place by luck and promises the wrong depth.
        if (OwningProduct(project, id) is { } holder)
        {
            return ProductRoute(project, holder, id, targetAttribute);
        }

        // A VARIABLE — an ordinary function-block resource or an enum one. It has a tree row, but its values are
        // its own dialog's fields, so this class has to come BEFORE the row test below: reaching that test first
        // would make every variable finding tree-level and land the installer on a row with the field they came
        // for still one gesture away.
        //
        // ONLY when the finding names an attribute — the same rule the locality/block class follows, and it was
        // a live E2E run that showed why. A variable owns its tree row, and an attribute-less finding about one
        // is structural: `logic-variable-write-only` says the variable is written and never read, and the repair
        // is to edit the PROGRAM. Opening its name/note editor there is a modal to dismiss before the installer
        // can do the thing the finding asked for.
        if (targetAttribute is not null && element.Kind is ElementKind.Resource or ElementKind.EnumResource)
        {
            // The capability question is the variable editor's, not the product composer's — the same shape the
            // pin route uses, and for the same reason: this dialog's fields are hand-written, so the descriptor
            // has no opinion about them.
            return DialogRoute(
                id, id, id, targetAttribute,
                PropertiesDialogCoordinator.VariableFieldFor(targetAttribute) is not null);
        }

        // A LOCALITY, a FUNCTION BLOCK or a Betingelser group: the three elements that share the plain
        // name/note dialog (T044). Before the row test for the same reason the variables are — all three are
        // drawn, so the fallback would make every one of their findings tree-level.
        //
        // ONLY when the finding names an attribute, which is where this class differs from the pin and product
        // ones. Those two are about elements whose fixes live in a dialog whatever the finding; these three own
        // their tree row, and their attribute-less findings are structural — an empty locality, an unlinked
        // block — whose repair is a gesture ON that row. Opening a name/note dialog over one would be a modal
        // the installer has to dismiss before they can do the thing the finding asked for.
        if (targetAttribute is not null
            && (element.IsLocalityGroup || element.Kind is ElementKind.FunctionBlock || element.Tag == "conditions"))
        {
            // `master_*` lands here and answers null: the provenance group is read-only, so the route degrades
            // to the dialog rather than promising a field the installer cannot type in.
            return DialogRoute(
                id, id, id, targetAttribute,
                PropertiesDialogCoordinator.ElementFieldFor(targetAttribute) is not null);
        }

        if (hasRow(project, id))
        {
            return new NavigationPlan(id, null, NavigationKind.Tree);
        }

        // Not drawn: a setting inside a *_settings container, a calibration row. The nearest ancestor that IS
        // drawn is where the fix lives anyway — and the row said "Ancestor" before the click, so this is a
        // stated destination rather than a silent substitution.
        return nearestRowBearingAncestor(project, id) is { } ancestor
            ? new NavigationPlan(ancestor, null, NavigationKind.Ancestor)
            : Nowhere;
    }

    /// <summary>
    /// A route ending in a dialog. The pair <c>(owner, site)</c> is what tells the coordinator whether the value
    /// sits on that dialog or on a sub-item it has to select first.
    /// </summary>
    /// <remarks>
    /// THE DEGRADATION RULE lives here, stated ONCE for every dialog route rather than restated per element
    /// class: a field the dialog does not offer, or offers read-only, yields <see cref="NavigationKind.Dialog"/>
    /// and a hop carrying NO attribute. It never yields <see cref="NavigationKind.Field"/>, because a row that
    /// promised a field and then opened a dialog with nothing focused is exactly the dishonesty the kinds exist
    /// to prevent — and it is the ordinary case, not an edge one: the rows that already declare
    /// <c>product_identifier</c> or a <c>master_*</c> provenance attribute name a real attribute that is not a
    /// writable field.
    /// </remarks>
    /// <param name="reveal">The tree node to select, which is not always the owner.</param>
    /// <param name="field">Whether the dialog this route ends in really offers the attribute, editable.</param>
    private static NavigationPlan DialogRoute(
        ElementId? reveal, ElementId owner, ElementId site, string? attribute, bool field) =>
        new(reveal,
            new DialogHop(owner, site, field ? attribute : null),
            field ? NavigationKind.Field : NavigationKind.Dialog);

    /// <summary>A route ending in the product's own dialog, whose capability answer is the composed descriptor's.</summary>
    private NavigationPlan ProductRoute(Project project, ElementId owner, ElementId site, string? attribute) =>
        DialogRoute(
            owner, owner, site, attribute,
            attribute is { } name && IsWritableField(project, owner, site, name));

    private bool IsWritableField(Project project, ElementId owner, ElementId site, string attribute)
    {
        foreach (DialogDescriptorField candidate in composeProductDialog(project, owner).AllFields)
        {
            if (candidate.Target == site && candidate.Attribute == attribute)
            {
                return !candidate.ReadOnly;
            }
        }
        return false;
    }

    /// <summary>The product this element sits inside, or null when it sits in none.</summary>
    private static ElementId? OwningProduct(Project project, ElementId id)
    {
        for (ElementId current = id; project.FindParent(current) is { } parent;)
        {
            if (ProductClassifier.IsProduct(parent.Tag))
            {
                return parent.Id;
            }
            if (parent.Id is not { } parentId)
            {
                return null;
            }
            current = parentId;
        }
        return null;
    }

    /// <summary>
    /// Which host window repairs this whole-project finding, or null when none does.
    /// </summary>
    /// <remarks>
    /// The project-information rows are the only ones with a destination. A CAPACITY finding — too many
    /// modules, too many wireless links — is a fact about the whole installation with no single field behind
    /// it, and the module map is read-only, so opening it would move the installer without helping them. Those
    /// rows say so before the click instead, which is the honest answer and the one the panel already knows how
    /// to show.
    /// </remarks>
    private static HostRoute? HostRouteFor(ProblemCode code) =>
        code.Value.StartsWith("doc-project-info", StringComparison.Ordinal) ? HostRoute.ProjectInfo : null;

    /// <summary>
    /// Whether the code belongs to the link family, whose findings are repaired by a link gesture on the tree.
    /// <c>doc-not-linked</c> is named outright because it predates the family prefix and does not carry it.
    /// </summary>
    private static bool IsLinkFamily(ProblemCode code) =>
        code.Value is "doc-not-linked" || code.Value.StartsWith("link-", StringComparison.Ordinal);

    private static NavigationPlan Nowhere { get; } = new(null, null, NavigationKind.None);
}
