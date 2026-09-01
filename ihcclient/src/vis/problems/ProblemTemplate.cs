using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ihc.Vis.Problems
{
    /// <summary>
    /// THE ONE template substitution in the SDK: a declared <c>{slot}</c> replaced by the datum bound to it.
    /// <para>
    /// It lives in the contract namespace rather than on the catalogue entry because TWO layers bind, and only
    /// one of them may read the catalogue. A whole-project finding binds through
    /// <see cref="Ihc.Vis.Validation.ProblemCatalogEntry"/>; a refusing site below the validation engine binds
    /// through <see cref="RefusalIdentity.Binding"/>, carrying its own copy of the sentence. Two implementations
    /// would be two answers to "what does this row say", and the drift gate that keeps the two copies of the
    /// TEMPLATE equal would not notice them diverging in how they FILL it.
    /// </para>
    /// <para>
    /// A slot the caller does not supply is left as its own <c>{name}</c> placeholder rather than blanked: a
    /// visible gap is a defect a reader reports, where a silent blank reads as intended text.
    /// </para>
    /// <para>
    /// INTERNAL, deliberately. The public problem contract is a short list of value types and none of them is a
    /// gate; a substitution helper is machinery, not vocabulary, and adding it to that list would grow the
    /// surface a host has to read without giving a host anything it can use.
    /// </para>
    /// </summary>
    internal static class ProblemTemplate
    {
        /// <summary>Binds <paramref name="arguments"/> into <paramref name="template"/>'s declared slots.</summary>
        /// <param name="template">The sentence carrying <c>{slot}</c> placeholders.</param>
        /// <param name="arguments">The values, by slot name. An unmatched name changes nothing.</param>
        internal static string Bind(string template, IEnumerable<ProblemArgument> arguments)
        {
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(arguments);
            if (template.Length == 0 || template.IndexOf('{', StringComparison.Ordinal) < 0)
            {
                return template;
            }

            // FIRST value wins for a repeated name, which is what the sequential replace this replaced did: its
            // first pass consumed every occurrence, leaving the second nothing to find.
            Dictionary<string, string> byName = new(StringComparer.Ordinal);
            foreach (ProblemArgument argument in arguments)
            {
                byName.TryAdd(argument.Name, Format(argument.Value));
            }

            // ONE PASS OVER THE TEMPLATE, copying literal text and substituting only the placeholders the
            // TEMPLATE carries. The sequential Replace this replaced searched the whole buffer once per argument,
            // so a value inserted early became part of the text later arguments searched: an authored value
            // containing the characters "{tag}" was substituted a second time and rewritten by the engine's own
            // binding. Scanning the original means an inserted value is never looked at again.
            StringBuilder bound = new(template.Length);
            int at = 0;
            while (at < template.Length)
            {
                int open = template.IndexOf('{', at);
                int close = open < 0 ? -1 : template.IndexOf('}', open + 1);
                if (close < 0)
                {
                    bound.Append(template, at, template.Length - at);
                    break;
                }

                bound.Append(template, at, open - at);
                string name = template[(open + 1)..close];

                // An undeclared or unsupplied slot is left standing, placeholder and all: a visible gap is a
                // defect a reader reports, where a silent blank reads as intended text.
                bound.Append(byName.TryGetValue(name, out string? value)
                    ? value
                    : template[open..(close + 1)]);
                at = close + 1;
            }

            return bound.ToString();
        }

        /// <summary>
        /// A value as a template renders it. Invariant culture on purpose: a bound number is part of a sentence
        /// pinned by tests and oracles, so it may not change with the machine's locale.
        /// <para>
        /// Internal rather than private because the findings export writes the same argument values a second
        /// time, as <c>arg_*</c> attributes beside the message they are already inside. A second formatter is
        /// exactly how one number ends up spelled two ways on one line, so both renderings go through this one.
        /// </para>
        /// </summary>
        internal static string Format(object value) =>
            value as string ?? (value as IFormattable)?.ToString(null, CultureInfo.InvariantCulture)
            ?? value.ToString() ?? string.Empty;
    }
}
