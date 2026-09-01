using System;

namespace Ihc.Vis.Problems
{
    /// <summary>
    /// One code family — the scheme every SDK-minted problem identity follows. The VALIDATION family is the bare
    /// kebab-case catalogue ids (<c>load-empty</c>, <c>id-duplicate-token</c>), unchanged because they are already
    /// published; every other family carries a dotted prefix.
    /// <para>
    /// An enum rather than a value type wrapping a string: the set is closed — an unrecognised prefix is
    /// <see cref="Unknown"/>, not a new member — so a family is one comparison and ownership has exactly one
    /// answer, <see cref="ProblemCode.IsHostOwned"/>.
    /// </para>
    /// </summary>
    public enum ProblemFamily
    {
        /// <summary>
        /// An unrecognised prefix, or a code whose shape is not a valid identifier. Deliberately not an error
        /// state to throw on: a consumer meeting a code it does not know DEGRADES — it shows the message, groups
        /// it here, and carries on — so a host built against one SDK version does not break when the next adds a
        /// family.
        /// </summary>
        Unknown,

        /// <summary>The bare kebab-case catalogue ids. The ONE family with no prefix.</summary>
        Validation,

        /// <summary>Session edit preconditions and edit refusals — <c>edit.*</c>.</summary>
        Edit,

        /// <summary>Load and save operation outcomes — <c>io.*</c>.</summary>
        Io,

        /// <summary>Catalog-file import outcomes — <c>import.*</c>.</summary>
        Import,

        /// <summary>Controller download/upload outcomes — <c>bridge.*</c>.</summary>
        Bridge,

        /// <summary>The SDK catch-all — <c>internal.*</c>, home of <c>internal.unexpected</c>.</summary>
        Internal,

        /// <summary>The RESERVED host family — <c>app.*</c>. The SDK never mints into it.</summary>
        App,
    }

    /// <summary>
    /// A problem's stable identity: the family-scoped code, and nothing else. A value rather than an enum member
    /// because the vocabulary stays OPEN — a host constructs its own <c>app.*</c> codes, so no closed enum can
    /// exist. It is never the user-facing message.
    /// <para>
    /// Because this is a struct, <c>default(ProblemCode)</c> exists and carries a null <see cref="Value"/> despite
    /// the annotation. Every member here reads it defensively and returns <see cref="ProblemFamily.Unknown"/> or an
    /// empty string rather than throwing, which is the same posture the unknown-prefix rule takes.
    /// </para>
    /// </summary>
    /// <param name="Value">
    /// The code itself — a bare kebab-case catalogue id, or a dotted family-prefixed id. Compared with
    /// <see cref="StringComparison.Ordinal"/>: an identifier, not text.
    /// </param>
    public readonly record struct ProblemCode(string Value)
    {
        private const string EditPrefix = "edit";
        private const string IoPrefix = "io";
        private const string ImportPrefix = "import";
        private const string BridgePrefix = "bridge";
        private const string InternalPrefix = "internal";

        /// <summary>The reserved host prefix. The one segment that makes a code host-owned.</summary>
        private const string AppPrefix = "app";

        /// <summary>
        /// The family, read off the first dotted segment; <see cref="ProblemFamily.Unknown"/> for a prefix this
        /// version does not know, or for a code that is not a well-formed identifier. Reading the family off the
        /// code rather than storing it beside it is what makes ownership recoverable from the code alone, so a
        /// code and its family cannot disagree.
        /// </summary>
        public ProblemFamily Family
        {
            get
            {
                if (string.IsNullOrEmpty(Value))
                {
                    return ProblemFamily.Unknown;
                }

                int dot = Value.IndexOf('.', StringComparison.Ordinal);
                if (dot < 0)
                {
                    return IsKebabSegment(Value) ? ProblemFamily.Validation : ProblemFamily.Unknown;
                }

                return PrefixFamily(Value.AsSpan(0, dot));
            }
        }

        /// <summary>
        /// Whether a HOST minted this code — true exactly when the first dotted segment is <c>app</c>. THE
        /// ownership predicate, so "who owns this code" has one answer in the codebase.
        /// </summary>
        public bool IsHostOwned => Family == ProblemFamily.App;

        /// <summary>
        /// The stable documentation anchor: the code with its dots flattened to hyphens, so it is a single
        /// heading-style slug. Derived rather than stored, so it cannot point somewhere else.
        /// </summary>
        public string ExplanationAnchor =>
            string.IsNullOrEmpty(Value) ? string.Empty : Value.Replace('.', '-');

        /// <summary>
        /// Parses and VALIDATES the shape: kebab-case segments (lowercase ASCII letters, digits and single
        /// interior hyphens), no empty segment, and a known family prefix whenever the code is dotted.
        /// <para>
        /// Note the gap this cannot close: the positional constructor is public, so
        /// <c>new ProblemCode("nonsense!!")</c> bypasses it. That is deliberate — an open vocabulary needs a
        /// constructor a host can call — which is why nothing downstream may ASSUME a well-formed code, and why
        /// <see cref="Family"/> degrades instead of throwing.
        /// </para>
        /// </summary>
        /// <exception cref="ArgumentException">The value is not a well-formed problem code.</exception>
        public static ProblemCode Parse(string value) =>
            TryParse(value, out ProblemCode code)
                ? code
                : throw new ArgumentException(
                    $"'{value}' is not a well-formed problem code: expected kebab-case segments and, when dotted, " +
                    $"one of the known family prefixes ({EditPrefix}, {IoPrefix}, {ImportPrefix}, {BridgePrefix}, " +
                    $"{InternalPrefix}, {AppPrefix}).",
                    nameof(value));

        /// <summary>The non-throwing counterpart, for reading a code back from stored data or a log.</summary>
        public static bool TryParse(string? value, out ProblemCode code)
        {
            code = default;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            ReadOnlySpan<char> remaining = value;
            bool first = true;
            while (true)
            {
                int dot = remaining.IndexOf('.');
                ReadOnlySpan<char> segment = dot < 0 ? remaining : remaining[..dot];
                if (!IsKebabSegment(segment))
                {
                    return false;
                }

                if (first && dot >= 0 && PrefixFamily(segment) == ProblemFamily.Unknown)
                {
                    return false;
                }

                if (dot < 0)
                {
                    break;
                }

                first = false;
                remaining = remaining[(dot + 1)..];
            }

            code = new ProblemCode(value);
            return true;
        }

        /// <inheritdoc/>
        public override string ToString() => Value ?? string.Empty;

        private static ProblemFamily PrefixFamily(ReadOnlySpan<char> prefix) => prefix switch
        {
            EditPrefix => ProblemFamily.Edit,
            IoPrefix => ProblemFamily.Io,
            ImportPrefix => ProblemFamily.Import,
            BridgePrefix => ProblemFamily.Bridge,
            InternalPrefix => ProblemFamily.Internal,
            AppPrefix => ProblemFamily.App,
            _ => ProblemFamily.Unknown,
        };

        /// <summary>One kebab-case segment: lowercase ASCII letters and digits, single interior hyphens only.</summary>
        private static bool IsKebabSegment(ReadOnlySpan<char> segment)
        {
            if (segment.IsEmpty || segment[0] == '-' || segment[^1] == '-')
            {
                return false;
            }

            bool previousWasHyphen = false;
            foreach (char c in segment)
            {
                if (c == '-')
                {
                    if (previousWasHyphen)
                    {
                        return false;
                    }

                    previousWasHyphen = true;
                    continue;
                }

                if (!char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c))
                {
                    return false;
                }

                previousWasHyphen = false;
            }

            return true;
        }
    }
}
