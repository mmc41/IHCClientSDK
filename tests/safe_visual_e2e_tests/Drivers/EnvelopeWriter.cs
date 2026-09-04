using System;
using System.Collections.Generic;
using System.Text.Json;

namespace safe_visual_e2e_tests;

/// <summary>
/// Builds the result envelope every driver answers with, in ONE place.
/// </summary>
/// <remarks>
/// <para>The round trip through <see cref="E2E.Envelope.Parse"/> is not waste. Every consumer reads either
/// <c>data</c> or the <c>context</c> block beside it, so serializing and re-parsing is the only way both halves
/// are guaranteed to describe the same envelope — and it makes a shape mismatch between drivers a JSON
/// difference rather than a C# one.</para>
///
/// <para>Shared rather than written per driver, because the envelope SHAPE is the suite's contract: a scenario
/// asserts on the same fields whichever driver produced them. Two independent copies is exactly how they came
/// to disagree once already — one populated no context at all, and every reader of the modal stack, the window
/// title and the selections quietly saw nothing in that mode.</para>
///
/// <para>The <c>context</c> block is the caller's, supplied as a function so it is evaluated fresh for each
/// envelope: it describes the application as it is NOW, after the verb ran.</para>
/// </remarks>
/// <param name="context">Produces the <c>context</c> block for the driver that owns this writer.</param>
internal sealed class EnvelopeWriter(Func<Dictionary<string, object?>> context)
{
    /// <summary>A success, optionally carrying data.</summary>
    internal E2E.Envelope Ok(object? data = null) => Build(true, "OK", string.Empty, data);

    /// <summary>A refusal. Never an exception: an <c>ok:false</c> envelope is a valid answer.</summary>
    internal E2E.Envelope Refuse(string code, string message) => Build(false, code, message, null);

    /// <summary>The general form, for the verbs that put something in both <c>message</c> and <c>data</c>.</summary>
    internal E2E.Envelope Build(bool ok, string code, string message, object? data) =>
        E2E.Envelope.Parse(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["ok"] = ok,
            ["code"] = code,
            ["message"] = message,
            ["data"] = data ?? new Dictionary<string, object?>(),
            ["context"] = context(),
        }));
}
