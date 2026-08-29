using System.Collections.Generic;
using System.Linq;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;

namespace safe_visual_tests;

/// <summary>
/// The route planner, driven as a table. Its reads are injected, so each case states the world it reasons about
/// — which rows exist, and what the product dialog offers — instead of building a tree and a descriptor to imply
/// one. That is the point of the planner being pure.
///
/// <para>Every DEGRADATION has a case here, because a degradation is where a route stops being honest. The row
/// promised something before the click, and these are the situations in which the promise must be weaker than it
/// looks: no element, a vanished one, nothing drawn above it, or an attribute the dialog does not actually offer
/// as an editable field.</para>
/// </summary>
public class ProblemNavigationPlannerTests
{
    private static ProjectElement Element(string tag, string id, params ProjectElement[] children) =>
        ProjectElement.Create(tag, ElementId.ParseOrNull(id), [], children);

    private static ElementId Id(string token) => ElementId.ParseOrNull(token)!.Value;

    private static ProblemCode Code(string value) => new(value);

    /// <summary>A locality holding a product with a drawn terminal and an undrawn settings child.</summary>
    private static Project Build() =>
        new(Element("project", "_0x0",
            Element("groups", "_0x1",
                Element("group", "_0x2",
                    Element("product_dataline", "_0x100",
                        Element("dataline_input", "_0x101"),
                        Element("dimmer_settings", "_0x106",
                            Element("dimmer_setting_fade_rate_up", "_0x107")))))));

    private static DialogDescriptorField Field(string id, string target, string attribute, bool readOnly = false) =>
        new(id, attribute, DialogControlKind.Text, Id(target), attribute, "", readOnly, null, null, null);

    /// <summary>
    /// What the product's dialog offers: the terminal's cable colour and the product's note are editable,
    /// <c>product_identifier</c> is present but READ-ONLY, and nothing else is a field at all.
    /// </summary>
    private static ProductDialogDescriptor Descriptor => new("Produkt",
    [
        new DialogDescriptorGroup("g", null, 1,
        [
            Field("dlg.g.cable", "_0x101", "cable_colour"),
            Field("dlg.g.note", "_0x100", "note"),
            Field("dlg.g.identifier", "_0x100", "product_identifier", readOnly: true),
        ], []),
    ]);

    /// <summary>A planner over a stated world: these ids have rows, and this is what sits above each of them.</summary>
    private static ProblemNavigationPlanner Planner(
        IEnumerable<string> rowBearing, IDictionary<string, string?>? ancestors = null)
    {
        var rows = rowBearing.Select(Id).ToHashSet();
        return new ProblemNavigationPlanner(
            (_, id) => rows.Contains(id),
            (_, id) => ancestors is not null
                && ancestors.TryGetValue(id.ToToken(), out string? token)
                && token is not null
                    ? Id(token)
                    : null,
            (_, _) => Descriptor);
    }

    private static ProblemNavigationPlanner Default() =>
        ProblemNavigationPlanner.Over((_, _) => Descriptor);

    // ── §5.4's DialogHop table: the four Site/Owner × Attribute combinations ────────────────────────────────

    /// <summary>Site equals Owner, attribute set: focus that field on the owner's own dialog.</summary>
    [Test]
    public void AProductsOwnWritableField_PlansFieldOnItsOwnDialog()
    {
        NavigationPlan plan = Planner(["_0x100"]).Plan(Build(), Id("_0x100"), "note", Code("doc-note"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Field));
            Assert.That(plan.Reveal, Is.EqualTo(Id("_0x100")));
            Assert.That(plan.Dialog, Is.EqualTo(new DialogHop(Id("_0x100"), Id("_0x100"), "note")));
        });
    }

    /// <summary>Site equals Owner, attribute null: open the dialog plain.</summary>
    [Test]
    public void AProductWithNoDeclaredAttribute_PlansTheDialogPlain()
    {
        NavigationPlan plan = Planner(["_0x100"]).Plan(Build(), Id("_0x100"), null, Code("product-advisory"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Dialog));
            Assert.That(plan.Dialog, Is.EqualTo(new DialogHop(Id("_0x100"), Id("_0x100"), null)));
        });
    }

    /// <summary>Site differs from Owner, attribute set: select the terminal row AND stack the sub-dialog on it.</summary>
    [Test]
    public void ATerminalsWritableField_PlansTheProductDialogWithThatTerminalAndTheField()
    {
        NavigationPlan plan = Planner(["_0x100", "_0x101"])
            .Plan(Build(), Id("_0x101"), "cable_colour", Code("doc-cable-colour"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Field));
            Assert.That(plan.Reveal, Is.EqualTo(Id("_0x100")),
                "the pin has a row, but the PRODUCT owns the dialog — the same pin-to-product redirect the "
                + "tree's own Egenskaber gesture makes");
            Assert.That(plan.Dialog, Is.EqualTo(new DialogHop(Id("_0x100"), Id("_0x101"), "cable_colour")));
        });
    }

    /// <summary>
    /// Site differs from Owner, attribute present but NOT a field of the editor the route ends in: select the
    /// terminal row, stack nothing.
    /// <para>A terminal's capability is the TERMINAL EDITOR's field set, not the product dialog's — the product
    /// dialog shows terminals as grid rows, so asking its descriptor about a terminal attribute answers no for
    /// every one of them and would degrade every terminal route. <c>documentation_tag</c> is a real attribute
    /// that editor does not offer, which is what makes this the honest degradation rather than an artefact.</para>
    /// </summary>
    [Test]
    public void ATerminalWithNoWritableField_PlansTheProductDialogWithThatTerminalOnly()
    {
        NavigationPlan plan = Planner(["_0x100", "_0x101"])
            .Plan(Build(), Id("_0x101"), "documentation_tag", Code("doc-documentation-tag"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Dialog),
                "the terminal editor has no field for it, so a Field promise would be a lie");
            Assert.That(plan.Dialog, Is.EqualTo(new DialogHop(Id("_0x100"), Id("_0x101"), null)),
                "the hop still selects the row — it just focuses nothing");
        });
    }

    /// <summary>
    /// And the positive half: an attribute the terminal editor DOES offer plans a field, even though the product
    /// dialog's own descriptor contains no such field. Pinned because getting this backwards degraded every
    /// terminal route to dialog-level while looking entirely reasonable.
    /// </summary>
    [Test]
    public void ATerminalAttributeTheEditorOffersPlansAField()
    {
        NavigationPlan plan = Planner(["_0x100", "_0x101"])
            .Plan(Build(), Id("_0x101"), "address_dataline", Code("doc-address"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Field));
            Assert.That(plan.Dialog, Is.EqualTo(new DialogHop(Id("_0x100"), Id("_0x101"), "address_dataline")));
        });
    }

    // ── The degradation rule ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The rows that ALREADY declare a non-writable attribute are this rule's day-one population, not an edge
    /// case: <c>product_identifier</c> is rendered but read-only, and the <c>master_*</c> provenance attributes
    /// are not rendered at all.
    /// </summary>
    [Test]
    public void AnAttributeThatIsNotAWritableField_DegradesToDialogAndCarriesNoField()
    {
        Project project = Build();
        ProblemNavigationPlanner planner = Planner(["_0x100"]);

        Assert.Multiple(() =>
        {
            NavigationPlan readOnly = planner.Plan(project, Id("_0x100"), "product_identifier", Code("migration"));
            Assert.That(readOnly.Kind, Is.EqualTo(NavigationKind.Dialog), "offered, but not editable");
            Assert.That(readOnly.Dialog!.Attribute, Is.Null,
                "and the hop drops the attribute, so the coordinator cannot be asked to focus it anyway");

            NavigationPlan absent = planner.Plan(project, Id("_0x100"), "master_type", Code("fb-provenance"));
            Assert.That(absent.Kind, Is.EqualTo(NavigationKind.Dialog), "not rendered as a field at all");
            Assert.That(absent.Dialog!.Attribute, Is.Null);
        });
    }

    // ── The doc-not-linked exception ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A link finding is repaired by a link gesture on the tree row, so it stays TREE-only. The generic pin
    /// class above would otherwise open the product dialog with the terminal selected — a plausible-looking
    /// route to the wrong kind of repair, and a modal the user must dismiss before they can do anything.
    /// </summary>
    [Test]
    public void ALinkFindingOnATerminal_PlansTreeWithNoDialog()
    {
        NavigationPlan plan = Planner(["_0x100", "_0x101"])
            .Plan(Build(), Id("_0x101"), null, Code("doc-not-linked"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Tree));
            Assert.That(plan.Reveal, Is.EqualTo(Id("_0x101")), "the TERMINAL row, not its product");
            Assert.That(plan.Dialog, Is.Null);
        });
    }

    [Test]
    public void TheLinkExceptionCoversTheWholeFamilyAndNothingElse()
    {
        Project project = Build();
        ProblemNavigationPlanner planner = Planner(["_0x100", "_0x101"]);

        Assert.Multiple(() =>
        {
            Assert.That(planner.Plan(project, Id("_0x101"), null, Code("link-product-unwired")).Dialog, Is.Null,
                "the prefixed members of the family route the same way as doc-not-linked");
            Assert.That(planner.Plan(project, Id("_0x101"), null, Code("name-note-missing")).Kind,
                Is.EqualTo(NavigationKind.Dialog),
                "an ordinary pin finding is NOT swept into the exception — it takes the product route");
        });
    }

    // ── Reveal derivation and the remaining degradations ────────────────────────────────────────────────────

    [Test]
    public void AnUnclassifiedElementWithItsOwnRowIsRevealedDirectly()
    {
        NavigationPlan plan = Planner(["_0x2"]).Plan(Build(), Id("_0x2"), null, Code("name-empty"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Tree), "a locality is neither a pin nor a product");
            Assert.That(plan.Reveal, Is.EqualTo(Id("_0x2")));
            Assert.That(plan.Dialog, Is.Null);
        });
    }

    /// <summary>
    /// An element the tree does not draw, but which sits INSIDE a product, routes to that product's dialog — the
    /// settings row of §5.4's table. The reveal lands on the product either way; what the deeper route adds is
    /// that the dialog opens, which is where such a value is actually edited.
    /// </summary>
    [Test]
    public void AnUndrawnElementInsideAProductRoutesToThatProductsDialog()
    {
        NavigationPlan plan = Planner(["_0x100"])
            .Plan(Build(), Id("_0x107"), "inivalue", Code("dev-setting-default"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Reveal, Is.EqualTo(Id("_0x100")), "the owning product");
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Dialog),
                "its dialog offers no 'inivalue' field, so the route is honest about stopping there");
            Assert.That(plan.Dialog, Is.EqualTo(new DialogHop(Id("_0x100"), Id("_0x107"), null)));
        });
    }

    /// <summary>
    /// The ANCESTOR fallback is what is left for an undrawn element with no owning product. It is the planner's
    /// last resort, below every named route class.
    /// </summary>
    [Test]
    public void AnUndrawnElementOutsideAnyProductFallsBackToItsNearestDrawnAncestor()
    {
        NavigationPlan plan = Planner(["_0x2"], new Dictionary<string, string?> { ["_0x300"] = "_0x2" })
            .Plan(Loose(), Id("_0x300"), "note", Code("doc-note"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.Ancestor),
                "named apart from Tree, so the row can say WHICH element the click will select");
            Assert.That(plan.Reveal, Is.EqualTo(Id("_0x2")));
        });
    }

    /// <summary>
    /// A locality holding an element that belongs to no product — and that no named route class claims, so what
    /// these two tests observe really is the fallback.
    /// <para>A <c>program_simple</c> rather than a function block: since T044 a block has a route of its own, so
    /// using one here measured that route instead of the fallback the tests are about.</para>
    /// </summary>
    private static Project Loose() =>
        new(Element("project", "_0x0",
            Element("groups", "_0x1",
                Element("group", "_0x2",
                    Element("program_simple", "_0x300")))));

    /// <summary>
    /// A whole-project finding with no host route goes nowhere. The exemplar is a CAPACITY code since T046:
    /// <c>doc-project-info-blank</c> now has a window of its own, so using it here would have measured the
    /// route rather than the absence of one.
    /// </summary>
    [Test]
    public void AWholeProjectFindingWithNoHostRouteGoesNowhere()
    {
        NavigationPlan plan = Planner(["_0x101"]).Plan(Build(), null, null, Code("capacity-modules-exceeded"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.None));
            Assert.That(plan.Reveal, Is.Null);
            Assert.That(plan.Host, Is.EqualTo(HostRoute.None));
        });
    }

    /// <summary>
    /// The panel drops a duplicate token's anchor, because the token names two elements and choosing either
    /// would move the tree to an element the row was never about. It reaches the planner as no site at all.
    /// </summary>
    [Test]
    public void ADroppedDuplicateIdAnchorRoutesNowhere()
    {
        NavigationPlan plan = Planner(["_0x101"]).Plan(Build(), null, "cable_colour", Code("doc-cable-colour"));

        Assert.That(plan.Kind, Is.EqualTo(NavigationKind.None),
            "an attribute is no help when there is no element to apply it to");
    }

    [Test]
    public void AnElementDeletedSinceTheRunRoutesNowhere()
    {
        NavigationPlan plan = Planner(["_0xdead"])
            .Plan(Build(), Id("_0xdead"), "cable_colour", Code("doc-cable-colour"));

        Assert.That(plan.Kind, Is.EqualTo(NavigationKind.None),
            "the row-bearing map says yes, but the project no longer holds the element — the project wins, or "
            + "the plan would promise a row for something that is gone");
    }

    [Test]
    public void AnUndrawnElementWithNoDrawnAncestorRoutesNowhere()
    {
        NavigationPlan plan = Planner([]).Plan(Loose(), Id("_0x300"), "note", Code("doc-note"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Kind, Is.EqualTo(NavigationKind.None));
            Assert.That(plan.Reveal, Is.Null, "there is nothing to select, so none is named");
        });
    }

    [Test]
    public void ANullAttributeDoesNotChangeWhereTheTreeGoes()
    {
        Project project = Build();
        ProblemNavigationPlanner planner = Planner(["_0x2"]);

        Assert.That(planner.Plan(project, Id("_0x2"), null, Code("name-empty")).Reveal,
            Is.EqualTo(planner.Plan(project, Id("_0x2"), "name", Code("name-empty")).Reveal),
            "the attribute decides the DIALOG leg; where the tree goes does not depend on it");
    }

    /// <summary>
    /// The shell's planner is wired to the projector's own ladder, so its routes and the tree's rows cannot come
    /// from two different opinions about what is drawn.
    /// </summary>
    [Test]
    public void TheShellPlannerUsesTheProjectorsOwnPredicates()
    {
        Project project = Build();

        Assert.Multiple(() =>
        {
            Assert.That(Default().Plan(project, Id("_0x2"), null, Code("name-empty")).Kind,
                Is.EqualTo(NavigationKind.Tree), "a locality is drawn");
            Assert.That(Default().Plan(project, Id("_0x107"), null, Code("dev-setting-default")).Reveal,
                Is.EqualTo(Id("_0x100")),
                "a setting inside a *_settings container is not drawn, and its product is where it is edited");
        });
    }
}
