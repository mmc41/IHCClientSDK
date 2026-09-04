using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ihc_openvisual.Services;

/// <summary>
/// The application's lifecycle, validation-currency and fault state, as one read-only value.
/// </summary>
/// <remarks>
/// <para><b>What it is for.</b> A driver outside the process can see the application richly as a USER INTERFACE
/// — window title, rows, counts, automation ids — and has no direct view of any of these three. Everything it
/// knows about them it infers from rendering: a spinner's visibility, a sentence's text, a bullet in the title
/// bar. Every such inference is lossy, and one of them is provably blind for a whole second after an edit, so
/// what a driver actually does instead is sleep a fixed interval and hope.</para>
///
/// <para><b>The field that matters is <c>val</c>.</b> It carries the bound validation result's own keys, so
/// <see cref="IsValidationCurrent"/> — <c>val == gen.ver</c> — is exactly the comparison
/// <c>ValidationMonitor.IsStale</c> already makes and then discards. Nothing new is computed here; something
/// already computed stops being thrown away. Because it is a LEVEL-triggered comparison rather than an
/// edge-triggered counter, a driver waiting on it needs no pre-action baseline and cannot miss a transition
/// that happened while it was not looking.</para>
///
/// <para>No Avalonia type is named here, deliberately: the value, its format and its parser are testable
/// without a window, and both of the end-to-end suite's drivers share this one implementation rather than
/// each interpreting the format its own way.</para>
/// </remarks>
/// <param name="Generation">Which document — <c>ValidationMonitor.Generation</c>.</param>
/// <param name="Version">Which edit state — <c>ProjectWorkflow.Version</c>. Answers "did my edit land?".</param>
/// <param name="ValidatedGeneration">The bound result's generation, or null when nothing is bound.</param>
/// <param name="ValidatedVersion">The bound result's version, or null when nothing is bound.</param>
/// <param name="Dirty">Unsaved changes — today visible only as a bullet in the title bar.</param>
/// <param name="Faults">The cumulative internal-fault count; see <see cref="InternalErrorTally"/>.</param>
/// <param name="LastFault">The most recent fault's code, or null when nothing has faulted.</param>
/// <param name="DocumentName">Which file is open — what replaces polling the window title.</param>
public readonly record struct AutomationSnapshot(
    int Generation,
    int Version,
    int? ValidatedGeneration,
    int? ValidatedVersion,
    bool Dirty,
    long Faults,
    string? LastFault,
    string DocumentName)
{
    private const string Absent = "-";
    private const char PairSeparator = '|';
    private const char KeyValueSeparator = '=';

    private const string GenerationKey = "gen";
    private const string VersionKey = "ver";
    private const string ValidatedKey = "val";
    private const string DirtyKey = "dirty";
    private const string FaultsKey = "faults";
    private const string FaultKey = "fault";
    private const string DocumentKey = "doc";

    /// <summary>
    /// Whether the findings on screen describe the CURRENT document. False while nothing is bound, which is the
    /// state during the first run of every launch and immediately after every document replacement — right,
    /// because a panel with nothing in it describes nothing.
    /// </summary>
    public bool IsValidationCurrent => ValidatedGeneration == Generation && ValidatedVersion == Version;

    /// <summary>
    /// Renders the snapshot in the published format: <c>|</c>-separated <c>key=value</c> pairs, in a FIXED
    /// order, with the document name last because it is the only value not drawn from a constrained alphabet.
    /// </summary>
    /// <remarks>
    /// A parser may not rely on the order — it is written down so that a diff of two snapshots is readable by a
    /// person, which is half of what makes a failed wait diagnosable.
    /// </remarks>
    public string Format()
    {
        StringBuilder text = new();
        Append(text, GenerationKey, Number(Generation));
        Append(text, VersionKey, Number(Version));
        Append(text, ValidatedKey, ValidatedGeneration is { } g && ValidatedVersion is { } v
            ? $"{Number(g)}.{Number(v)}"
            : Absent);
        Append(text, DirtyKey, Dirty ? "1" : "0");
        Append(text, FaultsKey, Number(Faults));
        // Keyed by the same counter the assertion uses, so a reader can line the two up — and the reader holds
        // it to that: a pair whose sequence is not the count beside it is rejected. NOT a set of every code
        // seen: a repeat of an already-seen code moves the count and leaves a set identical, so a set could
        // never diagnose a delta.
        Append(text, FaultKey, LastFault is { } code ? $"{Number(Faults)}:{code}" : Absent);
        Append(text, DocumentKey, Encode(DocumentName));
        return text.ToString();
    }

    /// <summary>
    /// Reads a published property back. The three outcomes are distinguished rather than collapsed — see
    /// <see cref="SnapshotRead"/>.
    /// </summary>
    /// <remarks>
    /// <b>The parse fails CLOSED.</b> An unknown key, a duplicate, a missing field, a value that will not
    /// parse or two fields that contradict each other rejects the WHOLE snapshot and says so, carrying the
    /// offending text. It never yields a
    /// default-valued snapshot: one that quietly defaulted <c>val</c> to current would make every wait return
    /// instantly, which is precisely the class of failure this surface exists to remove. Rejecting an UNKNOWN
    /// key is deliberate too — a driver reading a newer application than it understands must fail loudly rather
    /// than silently ignore a field that might have mattered.
    /// </remarks>
    /// <param name="published">The property's value, as read from the application.</param>
    public static SnapshotRead Read(string? published)
    {
        if (string.IsNullOrEmpty(published))
        {
            // NOT an error: this is the application running as a user runs it, without the test surface. A
            // driver has to report that differently from a snapshot it could not understand.
            return default;
        }

        Dictionary<string, string> fields = [];
        foreach (string pair in published.Split(PairSeparator))
        {
            int at = pair.IndexOf(KeyValueSeparator, StringComparison.Ordinal);
            if (at <= 0)
            {
                return Reject($"'{pair}' is not a key=value pair", published);
            }

            string key = pair[..at];
            if (!fields.TryAdd(key, pair[(at + 1)..]))
            {
                return Reject($"'{key}' appears more than once", published);
            }
        }

        foreach (string key in fields.Keys)
        {
            if (key is not (GenerationKey or VersionKey or ValidatedKey or DirtyKey or FaultsKey
                or FaultKey or DocumentKey))
            {
                return Reject($"'{key}' is not a field this reader knows", published);
            }
        }

        if (!TryInt(fields, GenerationKey, out int generation, out string? bad)
            || !TryInt(fields, VersionKey, out int version, out bad)
            || !TryValidated(fields, out int? validatedGeneration, out int? validatedVersion, out bad)
            || !TryDirty(fields, out bool dirty, out bad)
            || !TryFaults(fields, out long faults, out string? lastFault, out bad)
            || !TryText(fields, DocumentKey, out string document, out bad))
        {
            return Reject(bad!, published);
        }

        return new SnapshotRead(
            new AutomationSnapshot(generation, version, validatedGeneration, validatedVersion, dirty, faults,
                lastFault, document),
            Rejection: null);
    }

    private static SnapshotRead Reject(string why, string published) =>
        new(null, $"the published snapshot was rejected — {why}. Raw: '{published}'");

    private static void Append(StringBuilder text, string key, string value)
    {
        if (text.Length > 0)
        {
            text.Append(PairSeparator);
        }

        text.Append(key).Append(KeyValueSeparator).Append(value);
    }

    // Invariant, always: this is a machine-readable wire value, and a thousands separator or a comma decimal
    // point would make the format depend on where the application happens to be running.
    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Percent-encodes the two separators, and the escape character itself — without the third, decoding is
    /// ambiguous and a document literally named <c>%7C</c> would come back as one holding a bar.
    /// </summary>
    private static string Encode(string value) => value
        .Replace("%", "%25", StringComparison.Ordinal)
        .Replace("|", "%7C", StringComparison.Ordinal)
        .Replace("=", "%3D", StringComparison.Ordinal);

    private static string Decode(string value) => value
        .Replace("%3D", "=", StringComparison.Ordinal)
        .Replace("%7C", "|", StringComparison.Ordinal)
        .Replace("%25", "%", StringComparison.Ordinal);

    private static bool TryText(Dictionary<string, string> fields, string key, out string value, out string? why)
    {
        if (!fields.TryGetValue(key, out string? raw))
        {
            value = string.Empty;
            why = $"'{key}' is missing";
            return false;
        }

        value = Decode(raw);
        why = null;
        return true;
    }

    private static bool TryInt(Dictionary<string, string> fields, string key, out int value, out string? why)
    {
        value = 0;
        if (!fields.TryGetValue(key, out string? raw))
        {
            why = $"'{key}' is missing";
            return false;
        }

        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            why = $"'{key}={raw}' is not a number";
            return false;
        }

        why = null;
        return true;
    }

    private static bool TryValidated(
        Dictionary<string, string> fields, out int? generation, out int? version, out string? why)
    {
        generation = null;
        version = null;
        if (!fields.TryGetValue(ValidatedKey, out string? raw))
        {
            why = $"'{ValidatedKey}' is missing";
            return false;
        }

        if (raw == Absent)
        {
            why = null;
            return true;   // Nothing bound yet, which is a state and not a fault.
        }

        string[] keys = raw.Split('.');
        if (keys.Length != 2
            || !int.TryParse(keys[0], NumberStyles.None, CultureInfo.InvariantCulture, out int boundGeneration)
            || !int.TryParse(keys[1], NumberStyles.None, CultureInfo.InvariantCulture, out int boundVersion))
        {
            why = $"'{ValidatedKey}={raw}' is neither '{Absent}' nor a generation.version pair";
            return false;
        }

        generation = boundGeneration;
        version = boundVersion;
        why = null;
        return true;
    }

    private static bool TryDirty(Dictionary<string, string> fields, out bool dirty, out string? why)
    {
        dirty = false;
        if (!fields.TryGetValue(DirtyKey, out string? raw))
        {
            why = $"'{DirtyKey}' is missing";
            return false;
        }

        if (raw is not ("0" or "1"))
        {
            why = $"'{DirtyKey}={raw}' is neither 0 nor 1";
            return false;
        }

        dirty = raw == "1";
        why = null;
        return true;
    }

    private static bool TryFaults(
        Dictionary<string, string> fields, out long faults, out string? lastFault, out string? why)
    {
        faults = 0;
        lastFault = null;
        if (!fields.TryGetValue(FaultsKey, out string? count) || !fields.TryGetValue(FaultKey, out string? last))
        {
            why = $"'{FaultsKey}' or '{FaultKey}' is missing";
            return false;
        }

        if (!long.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out faults))
        {
            why = $"'{FaultsKey}={count}' is not a number";
            return false;
        }

        if (last == Absent)
        {
            if (faults != 0)
            {
                why = $"'{FaultsKey}={count}' counts faults but '{FaultKey}={last}' names none";
                return false;
            }

            why = null;
            return true;
        }

        int at = last.IndexOf(':', StringComparison.Ordinal);
        if (at <= 0 || at == last.Length - 1)
        {
            why = $"'{FaultKey}={last}' is neither '{Absent}' nor a sequence:code pair";
            return false;
        }

        // The sequence is not decoration: it is the count the code arrived on, written from the same reading of
        // one counter as the count beside it. A pair that disagrees with that count, or a code with no count
        // behind it, is a snapshot that was never written whole, and it fails closed like any other.
        if (!long.TryParse(last[..at], NumberStyles.None, CultureInfo.InvariantCulture, out long sequence)
            || sequence != faults)
        {
            why = $"'{FaultKey}={last}' is not keyed by the count '{FaultsKey}={count}' beside it";
            return false;
        }

        if (faults == 0)
        {
            why = $"'{FaultKey}={last}' names a fault while '{FaultsKey}={count}' counts none";
            return false;
        }

        lastFault = last[(at + 1)..];
        why = null;
        return true;
    }
}

/// <summary>
/// What reading the published property produced. The three outcomes are NOT interchangeable and a caller has to
/// tell them apart: a snapshot, a rejection, or nothing published at all.
/// </summary>
/// <param name="Value">The snapshot, when one was read.</param>
/// <param name="Rejection">
/// Why the published text was refused, with the text itself. Null when there was nothing wrong — which includes
/// the case where there was nothing at all.
/// </param>
public readonly record struct SnapshotRead(AutomationSnapshot? Value, string? Rejection)
{
    /// <summary>
    /// The application published nothing, which is what a run WITHOUT the test flag looks like. Distinct from a
    /// rejection: one says the surface is off, the other says it is on and unreadable.
    /// </summary>
    public bool Absent => Value is null && Rejection is null;
}
