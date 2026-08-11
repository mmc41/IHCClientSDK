using System;
using System.Collections.Generic;
using System.Linq;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The registered difference "every drag-and-drop operation is also reachable from the menus and the keyboard, so
/// linking, moving, and reordering never require a mouse".
///
/// <para>Each individual equivalence is already pinned where the route lives — <see cref="DragMoveTests"/> against
/// Cut/Paste, <see cref="DragReorderTests"/> against Move up/down, <see cref="DragLinkTests"/> against the two-step
/// link supplement, <see cref="DragProgramTests"/> against <i>Brug i program</i>. What none of them can pin is the
/// register's claim, which is about the SET: a promise that <b>no</b> drag route is mouse-only. A fifth route added
/// tomorrow with no menu counterpart would break the promise while every existing pair still passed.</para>
///
/// <para>So this enumerates <see cref="DropRoute"/> itself and requires each member to name its counterparts. The
/// enum is the app's own list of what can be dropped, which is why adding to it is what has to fail here.</para>
///
/// <para>Menu placement is the assertion for "and the keyboard": a command on the node context flyout is operable
/// without a mouse because Shift+F10 opens that flyout on the focused row (pinned by
/// <see cref="KeyboardContextMenuTests"/>), and the menu bar is reachable by its own accelerators. A counterpart
/// placed on <see cref="Surfaces.None"/> would exist without being reachable at all.</para>
/// </summary>
public class DragRouteAlternativesParityTests
{
    /// <summary>Every drag route and the non-mouse commands that reach the same result. Spelled out here rather
    /// than derived, so that RE-POINTING a route at a different command is a deliberate edit to this table.</summary>
    private static readonly Dictionary<DropRoute, string[]> Counterparts = new()
    {
        [DropRoute.PinLink] = ["link.startFromHere", "link.toHere"],
        [DropRoute.ProgramBuild] = ["node.useInProgram"],
        [DropRoute.Reorder] = ["edit.moveUp", "edit.moveDown"],
        [DropRoute.Reparent] = ["edit.cut", "edit.paste"],
    };

    [Test]
    public void EveryDragRoute_HasANonMouseCounterpart()
    {
        DropRoute[] dragRoutes = Enum.GetValues<DropRoute>().Where(r => r != DropRoute.None).ToArray();

        Assert.That(Counterparts.Keys, Is.EquivalentTo(dragRoutes),
            "a drag route with no entry here is a route with no stated keyboard/menu alternative — which is the "
            + "registered difference, so adding one means adding its counterpart too");
    }

    [Test]
    public void EveryCounterpart_IsARegisteredCommandOnAReachableSurface()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();

        Assert.Multiple(() =>
        {
            foreach ((DropRoute route, string[] ids) in Counterparts)
            {
                foreach (string id in ids)
                {
                    CommandSpec? row = vm.Registry.Rows.SingleOrDefault(r => r.Id == id);
                    Assert.That(row, Is.Not.Null, $"{route} names '{id}', which is not a command at all");
                    Assert.That(row!.Placement, Is.Not.EqualTo(Surfaces.None),
                        $"'{id}' is on no menu, so {route} would be mouse-only in practice");
                }
            }
        });
    }
}
