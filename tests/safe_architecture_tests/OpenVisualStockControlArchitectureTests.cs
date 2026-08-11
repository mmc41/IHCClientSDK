using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using static Ihc.Tests.ArchRuleHelpers;
using Type = System.Type;

namespace Ihc.Tests
{
    public partial class OpenVisualArchitectureTests
    {
        // ---- The automation surface's STRUCTURAL preconditions ----------------------------------------------------
        //
        // Avalonia keeps ONE tree: an AutomationPeer feeds the platform accessibility API (UIA, NSAccessibility,
        // AT-SPI2) and every UI-automation driver alike. safe_visual_tests' AutomationCoverageTests walks that tree
        // and asserts what it CONTAINS (a name, an id, an operable pattern per window it knows about). The four rules
        // below are the complement it cannot express: the structural preconditions that make the peer tree possible at
        // all, over every type the assembly declares — including markup, since the Avalonia XAML compiler emits
        // `!XamlIlPopulate` ONTO the window type itself (ihc_openvisual.Views.MainWindow::!XamlIlPopulate), so a
        // control authored in .axaml is a constructor-call edge of that window in this model exactly like a `new` in
        // C# would be. StockControlBanScan_SeesMarkupAuthoredConstructions pins that premise.

        /// <summary>The stock Avalonia menu/tree controls that cannot be operated through UI Automation at all:
        /// <c>MenuItemAutomationPeer</c> offers only Toggle and <c>TreeViewItemAutomationPeer</c> only Scroll and
        /// SelectionItem, so neither a driver nor a screen-reader user can invoke a command or open a submenu/node.
        /// The app supplies <c>AccessibleMenu</c>/<c>AccessibleMenuItem</c>/<c>AccessibleTreeView</c>/
        /// <c>AccessibleTreeViewItem</c> in their place. <see cref="Separator"/> is deliberately NOT here, and for a
        /// different reason from the four above: a separator must stay a Separator — wrapping one into a menu item is
        /// the opposite defect (a nameless, invokable row a client counts as a command and a screen reader reads out).
        /// The menus author <c>AccessibleSeparator</c>, which IS one: it subclasses <see cref="Separator"/> purely to
        /// give it the peer Avalonia's stock separator lacks, so the grouping reaches the automation tree instead of
        /// vanishing from it (alignment F-11).</summary>
        private static IReadOnlyCollection<string> StockMenuAndTreeControlTypeNames() => new HashSet<string>
        {
            typeof(Menu).FullName!,
            typeof(MenuItem).FullName!,
            typeof(TreeView).FullName!,
            typeof(TreeViewItem).FullName!,
        };

        /// <summary>The sanctioned replacements for those stock controls — the one list, since all three automation
        /// rules are about the same four types. The constructor ban exempts only each exact replacement-to-base
        /// constructor edge; other forbidden constructions written inside a replacement remain violations.</summary>
        private static readonly IReadOnlyCollection<Type> AccessibleControlTypes = new[]
        {
            typeof(global::ihc_openvisual.Controls.AccessibleMenu),
            typeof(global::ihc_openvisual.Controls.AccessibleMenuItem),
            typeof(global::ihc_openvisual.Controls.AccessibleTreeView),
            typeof(global::ihc_openvisual.Controls.AccessibleTreeViewItem),
        };

        private static readonly IReadOnlyCollection<ConstructorCallExemption> AccessibleBaseConstructorEdges =
            AccessibleControlTypes
                .Select(type => new ConstructorCallExemption(type.FullName!, type.BaseType!.FullName!))
                .ToList();

        /// <summary>
        /// The menu bar, the node flyout and the two trees are this app's whole command surface, and every string on
        /// them is Danish — so they must be reachable by a driver, not merely by a click at a screen coordinate. A
        /// bare <c>&lt;MenuItem&gt;</c> authored in markup is therefore not a style preference but a hole in the
        /// command surface, and it is invisible in a screenshot and in a passing behavioural test alike (the item
        /// looks and clicks exactly right; only the peer is empty). CLAUDE.md states the convention — "never author a
        /// bare <c>&lt;MenuItem&gt;</c> in this app" — and this makes it structural, over C# and XAML together, and
        /// over every window including ones no test fixture's roster knows about yet.
        /// </summary>
        [Test]
        public void Gui_DoesNotInstantiateStockMenuOrTreeControls() =>
            AssertDoesNotConstructTypeNames(Gui, GuiRoot, StockMenuAndTreeControlTypeNames(),
                "the stock Avalonia menu/tree controls",
                "menus and trees must be authored as the Accessible* subclasses — Avalonia's stock peers expose no Invoke/ExpandCollapse, so a bare MenuItem or TreeView is unreachable by UI Automation and by assistive technology",
                AccessibleBaseConstructorEdges);

        /// <summary>
        /// The positive control for <see cref="Gui_DoesNotInstantiateStockMenuOrTreeControls"/>, and the evidence for
        /// the premise the whole rule rests on: that a control authored in XAML is visible to a constructor-call scan
        /// at all. MainWindow's markup authors separators, so that edge MUST be observable on the
        /// <c>ihc_openvisual.Views.MainWindow</c> type. If Avalonia's XAML compiler ever moves populate code out of the
        /// window type (into the <c>CompiledAvaloniaXaml</c> namespace, which is outside <see cref="GuiRoot"/> and
        /// therefore unscanned), this fails — instead of the ban silently going blind to all markup while its four
        /// forbidden types quietly reappear in the menus.
        /// <para>The probe is the type the markup ACTUALLY authors, which is
        /// <c>AccessibleSeparator</c> since alignment F-11 gave separators a peer. Probing for the base
        /// <see cref="Separator"/> instead measured the subclass's own base-constructor call, not markup — so it went
        /// blind to XAML the day the menus adopted the subclass, which is exactly the failure this test exists to
        /// catch. Keep it pointed at a type the markup names.</para>
        /// </summary>
        [Test]
        public void StockControlBanScan_SeesMarkupAuthoredConstructions()
        {
            string markupProbe = typeof(global::ihc_openvisual.Controls.AccessibleSeparator).FullName!;
            var markupAuthored = ConstructorCallEdges(Gui, GuiRoot)
                .Where(edge => edge.Target == markupProbe)
                .Select(edge => edge.Origin)
                .ToList();

            Assert.That(markupAuthored, Does.Contain(typeof(global::ihc_openvisual.Views.MainWindow).FullName),
                "MainWindow's markup authors separators, so XAML-authored constructions must be attributed to the window type — otherwise this ban cannot see markup at all");

            // And the ban reports what it sees: the same scan with that type forbidden must fail.
            Assert.That(
                () => AssertDoesNotConstructTypeNames(Gui, GuiRoot,
                    new HashSet<string> { markupProbe }, "seeded probe", "seeded probe",
                    AccessibleBaseConstructorEdges),
                Throws.InstanceOf<AssertionException>(),
                "the scan must report a forbidden markup-authored construction, not merely observe it");

            // And the allowlist is what makes the real rule green, not an empty result set: each Accessible*
            // control calls its stock base's constructor, and ArchUnitNET models that as a constructor-call edge
            // like any other. Dropping the exemption must therefore report all four — which simultaneously proves
            // the scan sees construction edges onto the four genuinely forbidden types.
            Assert.That(
                () => AssertDoesNotConstructTypeNames(Gui, GuiRoot, StockMenuAndTreeControlTypeNames(),
                    "allowlist probe", "allowlist probe", exemptBaseConstructorEdges: null),
                Throws.InstanceOf<AssertionException>(),
                "the four sanctioned subclasses must be visible to the scan — otherwise the ban is green because it detects nothing");
            var wrongTargetExemptions = AccessibleControlTypes
                .Select(type => new ConstructorCallExemption(type.FullName!, typeof(Separator).FullName!))
                .ToList();
            Assert.That(
                () => AssertDoesNotConstructTypeNames(Gui, GuiRoot, StockMenuAndTreeControlTypeNames(),
                    "exact-edge probe", "exact-edge probe", wrongTargetExemptions),
                Throws.InstanceOf<AssertionException>(),
                "an allowed origin paired with the wrong target must not exempt every construction written by that type");
        }

    }
}
