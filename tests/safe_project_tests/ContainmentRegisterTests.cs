using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// Which floor every interactive entry point falls to, and why — one compiled row each.
///
/// <para><b>Why a compiled register and not a markdown table.</b> A table cannot fail. It cannot notice a
/// renamed handler, it cannot notice a new one, and it goes stale in the direction that looks fine: the
/// handlers it lists are the ones somebody remembered. That absence is exactly what lets unguarded handlers
/// ship in one dialog with every suite green: a fixture claiming to "list the guarded handlers" lists only the
/// ones its author knew about.</para>
///
/// <para><b>The set is exact in BOTH directions.</b> A new <c>async void</c> in the GUI assembly fails here
/// naming itself; a row whose member no longer exists fails here naming itself. Neither can be satisfied by
/// editing prose.</para>
///
/// <para><b>The floors are the ADR-001 records</b>, and they differ in what they PROMISE — floor 1 tells
/// the user, writes a log record and marks a span; floor 2 tells the user and does both; floor 3 records only;
/// floor 4 is the process nets. A row's floor is a claim about which promise this site keeps.</para>
/// </summary>
[TestFixture]
public class ContainmentRegisterTests
{
    /// <summary>What a site's fault reaches. The numbers are ADR-001's and are load-bearing: a lower number is a
    /// stronger promise, not merely a different one.</summary>
    internal enum Floor
    {
        /// <summary>The view-model's error boundary: dialog, status line, failed span.</summary>
        ViewModelBoundary = 1,

        /// <summary>A workflow catch: dialog, log record, failed span.</summary>
        WorkflowCatch = 2,

        /// <summary>The view layer's guard: a log record, a durable row, and the exception returned to a caller
        /// that can react.</summary>
        HandlerGuard = 3,
    }

    /// <param name="Owner">The declaring type — compiled, so a renamed TYPE stops the build.</param>
    /// <param name="Member">The handler. Checked to exist, so a renamed MEMBER fails the register.</param>
    /// <param name="Floor">Which promise this site keeps.</param>
    /// <param name="Reason">Why that floor and not a stronger one. A site on floor 3 has to justify it.</param>
    internal sealed record GuardedSite(Type Owner, string Member, Floor Floor, string Reason);

    /// <summary>
    /// Every interactive entry point in the GUI, with its floor and the reason for it.
    /// </summary>
    private static readonly IReadOnlyList<GuardedSite> Register =
    [
        new(typeof(AboutWindow), "OnRepoLinkClick", Floor.HandlerGuard,
            "A link that would not open is reported on the window it was pressed on — the address is on screen "
            + "right above the button, so a modal over a modal would add nothing a reader cannot already see."),

        new(typeof(ihc_openvisual.App), "OnFrameworkInitializationCompleted", Floor.ViewModelBoundary,
            "The shell's Opened lambda: an async void with the view-model in reach, so its fault takes floor 1 "
            + "and the installer gets a dialog. It is a LAMBDA, and named by the method it was written inside "
            + "rather than by its emitted name, whose ordinal shifts on unrelated edits."),

        new(typeof(EnumTypeManagerWindow), "OnNewType", Floor.HandlerGuard,
            "This dialog owns its own document edits and has no view-model in reach; the guard records the fault "
            + "and the dialog stays open on the panes it re-reads after every operation."),
        new(typeof(EnumTypeManagerWindow), "OnRenameType", Floor.HandlerGuard, SameAsNewType),
        new(typeof(EnumTypeManagerWindow), "OnDeleteType", Floor.HandlerGuard, SameAsNewType),
        new(typeof(EnumTypeManagerWindow), "OnNewValue", Floor.HandlerGuard, SameAsNewType),
        new(typeof(EnumTypeManagerWindow), "OnRenameValue", Floor.HandlerGuard, SameAsNewType),
        new(typeof(EnumTypeManagerWindow), "OnDeleteValue", Floor.HandlerGuard, SameAsNewType),

        new(typeof(InternalErrorWindow), "OnCopyClick", Floor.HandlerGuard,
            "The copy reports in place on its own button, which is the only surface visible from inside this "
            + "modal — a dialog over it would hide the very text being copied."),

        new(typeof(MainWindow), "OnTreeSourcePointerMoved", Floor.HandlerGuard,
            "A drag that failed to start has nothing to tell the user: the gesture simply did not take, and the "
            + "caller drops its highlight on the returned exception."),
        new(typeof(MainWindow), "OnTreeDrop", Floor.HandlerGuard,
            "Same gesture, other end. The drop's own outcome reaches the user through the command it runs; a "
            + "fault in the gesture plumbing is a record, not a message."),
        new(typeof(MainWindow), "OnClosing", Floor.HandlerGuard,
            "The quit path REACTS to the returned exception rather than reporting it — a save prompt that "
            + "failed cancels the close, because answering 'yes, discard' on its behalf is the one wrong move."),

        new(typeof(PinPropertiesWindow), "OnApply", Floor.HandlerGuard,
            "The guard is the BACKSTOP here, not the floor the installer meets: the Anvend callback is wrapped "
            + "at its supplier so a fault reaches floor 1 with a dialog and a status line. This catches only "
            + "what happens outside that callback."),

        new(typeof(ProblemsPanel), "OnCopyInternalsClick", Floor.HandlerGuard,
            "The bulk copy reports in place on its own control, exactly as the details dialog's copy does."),

        new(typeof(ProductDialogWindow), "Step", Floor.HandlerGuard,
            "Backstop, as with Anvend: the step callback is wrapped at its supplier so a fault reaches floor 1. "
            + "A failed step leaves the product dialog open on the rows it already had."),
    ];

    private const string SameAsNewType =
        "One of the buttons on the same dialog, on the same floor for the same reason: no view-model in reach, "
        + "and the panes are re-read after every operation whatever the verdict.";

    /// <summary>Every <c>async void</c> the GUI assembly declares, named as a reader finds it.</summary>
    private static IReadOnlyList<(string Type, string Member)> Sites() =>
        [.. typeof(MainWindow).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(m => m.ReturnType == typeof(void)
                        && m.GetCustomAttribute<AsyncStateMachineAttribute>() is not null)
            .Select(m => (Type: Outermost(m.DeclaringType!), Member: AuthoredName(m.Name)))
            .Distinct()];

    /// <summary>
    /// The member a reader would name. A lambda compiles to <c>&lt;Method&gt;b__N</c>, and the N is an ORDINAL
    /// that shifts when an unrelated lambda is added earlier in the same type — so a register keyed on the raw
    /// name would break on edits that changed nothing about containment. The method the lambda was written
    /// inside is both what a reader looks for and what stays put.
    /// </summary>
    private static string AuthoredName(string emitted)
    {
        if (!emitted.StartsWith('<'))
        {
            return emitted;
        }
        int close = emitted.IndexOf('>');
        return close > 1 ? emitted[1..close] : emitted;
    }

    /// <summary>The type a reader would name: a compiler-generated closure belongs to the type it was written
    /// in.</summary>
    private static string Outermost(Type type)
    {
        Type current = type;
        while (current.IsNested && current.DeclaringType is { } parent)
        {
            current = parent;
        }
        return current.FullName!;
    }

    /// <summary>
    /// THE GATE. The register and the assembly agree exactly — a new interactive entry point fails here naming
    /// itself, and a row for a handler that no longer exists fails here naming itself.
    /// </summary>
    [Test]
    public void TheRegisterNamesEveryInteractiveEntryPointAndNoOther()
    {
        var found = Sites().Select(s => $"{s.Type}.{s.Member}").ToHashSet(StringComparer.Ordinal);
        var declared = Register.Select(r => $"{r.Owner.FullName}.{r.Member}").ToHashSet(StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.Not.Empty,
                "the scan found no async void at all — this gate would pass vacuously; fix the scan, not the assert");
            Assert.That(found.Except(declared), Is.Empty,
                "an interactive entry point with no register row: give it one naming its floor and why");
            Assert.That(declared.Except(found), Is.Empty,
                "a register row naming no entry point: the member was renamed or removed, so the row now widens "
                + "the register for something that does not exist");
        });
    }

    /// <summary>
    /// Every row names a member that really is declared on the type it claims. The set comparison above would
    /// catch a typo too, but this says WHICH half is wrong — a row is either a wrong name or a wrong type, and
    /// the difference is the whole of the fix.
    /// </summary>
    [Test]
    public void EveryRowNamesARealMemberOfItsOwnType()
    {
        Assert.Multiple(() =>
        {
            foreach (GuardedSite site in Register)
            {
                Assert.That(
                    site.Owner.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                          BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                        .Any(m => m.Name == site.Member),
                    Is.True, $"{site.Owner.Name} declares no {site.Member}");
            }
        });
    }

    /// <summary>
    /// Every row records WHY its floor, and a floor-3 row is the one that has to justify itself: floors 1 and 2
    /// tell the user, and choosing not to is a decision rather than a default.
    /// </summary>
    [Test]
    public void EveryRowRecordsWhyItsFloorAndNotAStrongerOne()
    {
        Assert.Multiple(() =>
        {
            foreach (GuardedSite site in Register)
            {
                Assert.That(site.Reason, Is.Not.Empty.And.Length.GreaterThan(60),
                    $"{site.Owner.Name}.{site.Member}: a real reason, not a label");
                Assert.That(Enum.IsDefined(site.Floor), Is.True,
                    $"{site.Owner.Name}.{site.Member}: an undeclared floor claims a promise nobody defined");
            }
        });
    }

    /// <summary>
    /// No row sits on NO floor. The absence of a floor is what the architecture suite's own ratchet is for, and
    /// its baseline is empty — this register would be the wrong place to record such a site, because a row here
    /// asserts a promise is kept.
    /// </summary>
    [Test]
    public void NoRowClaimsToBeUnguarded()
    {
        Assert.That(Register.Select(r => (int)r.Floor), Is.All.GreaterThan(0));
    }
}
