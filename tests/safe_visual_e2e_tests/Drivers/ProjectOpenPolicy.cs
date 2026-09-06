using System;

using ihc_openvisual.Services;

namespace safe_visual_e2e_tests;

/// <summary>
/// What the <c>project open</c> verb means, written once for both drivers.
/// </summary>
/// <remarks>
/// <para>The two routes into the application could not be more different — a real file picker driven by
/// synthesized keystrokes on one side, a shell command invoked in-process on the other — and that is exactly why
/// everything ELSE about the verb has to be one thing. The preconditions, the settle ceiling and, above all, what
/// counts as the open having ARRIVED are the verb's contract; written twice they drift, and a scenario then
/// passes in the leg CI gates and fails on the desktop for a reason that is about the harness rather than the
/// application.</para>
///
/// <para><b>What both drivers do NOT do here is refuse.</b> A file the application cannot read is reported
/// through a modal and leaves the previous document bound, and that is a failed LOAD, not a failed gesture: the
/// verb answers OK and publishes what the application ended up showing, so the scenario asserts on the document
/// and on the modal stack. A driver that refused instead would report one failure twice, in two vocabularies,
/// and — because <c>RunOk</c> throws at the verb — would stop the scenario before its own, better-worded
/// assertions ran.</para>
/// </remarks>
internal static class ProjectOpenPolicy
{
    /// <summary>The argument every route needs, missing.</summary>
    internal const string MissingPathRefusal = "project open needs --path <file>";

    /// <summary>
    /// A fixture that is not there. Refused BEFORE the gesture, deliberately: a missing file is the scenario's own
    /// mistake, and letting it through would spend a modal and a fault row on it and then report it as an
    /// application failure.
    /// </summary>
    internal static string MissingFileRefusal(string path) => $"no project file at {path}";

    /// <summary>
    /// How long a document change is given to reach the published snapshot. Generous because it covers reading and
    /// normalizing the largest oracle in the corpus, and it is a ceiling rather than a wait — both drivers return
    /// on the first look that sees the new document.
    /// </summary>
    internal static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The document generation to measure the open against, read before the gesture.
    /// </summary>
    /// <remarks>
    /// Negative when nothing is published at all, so an application without the test surface never looks like a
    /// document that has already arrived.
    /// </remarks>
    internal static int GenerationOf(SnapshotRead read) => read.Value?.Generation ?? -1;

    /// <summary>The document the application says it is showing, empty when it says none.</summary>
    internal static string DocumentOf(SnapshotRead read) => read.Value?.DocumentName ?? string.Empty;

    /// <summary>
    /// Whether the open has ARRIVED: the published snapshot names the expected file AND belongs to a later
    /// document generation than the one the gesture started from.
    /// </summary>
    /// <remarks>
    /// The name alone is not enough, and the case it misses is not exotic: re-opening the file already shown — or
    /// another file of the same name in another folder — matches the name before anything has happened at all, so
    /// the verb returns instantly and the wait that follows it reads the PREVIOUS document's already-current
    /// validation and passes. <c>ValidationMonitor</c> bumps the generation on a document replacement and on
    /// nothing else, which is precisely the transition being waited for; a failed load never bumps it, which is
    /// what makes "the document did not change" observable rather than merely slow.
    /// </remarks>
    internal static bool HasArrived(SnapshotRead read, string fileName, int generationBefore) =>
        read.Value is { } snapshot
        && snapshot.Generation != generationBefore
        && string.Equals(snapshot.DocumentName, fileName, StringComparison.OrdinalIgnoreCase);
}
