#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Reporting
{
    /// <summary>
    /// The findings export writer: a flat, attribute-only XML document in the <c>.vis</c> encoding.
    ///
    /// <para><b>A pure formatter of the FINDINGS.</b> It validates no project, applies no filter and — this is
    /// the load-bearing one — never re-sorts. It emits the sequence it is handed, in that sequence, so the file
    /// and whatever produced the list cannot disagree. That is also what makes it byte-testable against a
    /// hand-built list instead of only against a corpus run.</para>
    ///
    /// <para><b>The OPTIONS are checked, and that is a different thing.</b> Nothing here inspects a finding to
    /// decide whether it belongs in the file. But <c>@severities</c> and <c>@error_tiers</c> are two statements
    /// about one filter, so a caller supplying both may not make them contradict each other — that pair is
    /// refused rather than written. Declining to emit a self-contradicting header is a property of the FORMAT,
    /// not a filter over the content.</para>
    ///
    /// <para><b>Byte contract.</b> ISO-8859-1 with the matching declaration, no BOM, CRLF throughout, three
    /// spaces of indent per level — the same shape a <c>.vis</c> file has, because these files sit beside the
    /// <c>.vis</c> oracles and are reviewed with the same tools.</para>
    ///
    /// <para><b>Attribute order is part of the format.</b> XML gives it no meaning, but a byte oracle does, and a
    /// reader scanning 618 lines in a terminal needs the left edge to stay column-comparable. So: machine-readable
    /// identity first, prose in the middle, payload last — and fixed regardless of which order the rows are in.</para>
    ///
    /// <para><b>No schema reference, deliberately.</b> The format HAS a published grammar —
    /// <c>ihcclient/schemas/ihc_project_findings.xsd</c>, packed into the NuGet package under <c>schemas/</c> —
    /// but the document does not name it. An <c>xsi:noNamespaceSchemaLocation</c> resolves against the
    /// document's own directory, so naming it would oblige every export to travel with a copy of the schema to
    /// mean anything, and would put two more attributes on the one line of the oracle corpus that is read by
    /// eye in every regeneration diff. A consumer that wants to validate supplies the schema; the repository
    /// applies it to the oracle corpus at build time instead. The document therefore stays standalone and
    /// namespace-free, which is what every reader of these files already relies on.</para>
    /// </summary>
    internal static class FindingExportWriter
    {
        /// <summary>The export format's own version — not the <c>.vis</c> format's, which no longer appears here.</summary>
        internal const string FormatVersion = "1";

        /// <summary>The document element.</summary>
        internal const string RootTag = "ihc_project_findings";

        /// <summary>One finding.</summary>
        internal const string FindingTag = "finding";

        /// <summary>
        /// The root's attributes, in emitted order. The completeness caveats sit last, together, because they
        /// are read together.
        /// </summary>
        internal static ImmutableArray<string> RootAttributes { get; } =
        [
            "version", "source", "generated", "saved_stamp", "order", "severities", "error_tiers",
            "rules_not_run",
        ];

        /// <summary>
        /// Every attribute name a <c>&lt;finding&gt;</c> may carry that is NOT an <c>arg_</c> slot, in emitted
        /// order.
        /// <para>
        /// Declared here, on the writer that emits them, so the conformance gate over the oracle files reads the
        /// same list rather than restating it. A restated list would keep passing after the writer's changed and
        /// the whole point of that gate is to catch an attribute nobody meant to add.
        /// </para>
        /// </summary>
        internal static ImmutableArray<string> FixedFindingAttributes { get; } =
        [
            "severity", "code", "category", "blocks", "locator", "message",
            "related", "xpath", "related_xpath",
        ];

        private const string XmlDeclaration = "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>";

        private const string Crlf = "\r\n";

        private const string Indent = "   ";

        /// <summary>
        /// Formats <paramref name="findings"/> as the export document's bytes.
        /// </summary>
        /// <param name="project">The validated project — read only for its save stamp.</param>
        /// <param name="findings">The sequence to emit, verbatim and in order.</param>
        /// <param name="profile">The profile the findings came from, for the not-run caveat.</param>
        /// <param name="options">What the caller knows and the SDK does not; null means <see cref="FindingExportOptions.Default"/>.</param>
        /// <param name="generatedAt">When this export was produced, from the service's clock.</param>
        internal static byte[] Write(
            Project project,
            IReadOnlyList<ValidationFinding> findings,
            ValidationProfile profile,
            FindingExportOptions? options,
            DateTimeOffset generatedAt)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(findings);
            ArgumentNullException.ThrowIfNull(profile);

            FindingExportOptions settings = options ?? FindingExportOptions.Default;
            bool errorIncluded = settings.Severities.Contains(ValidationSeverity.Error);

            // The two attributes are one statement written twice, so a caller that supplies both may not make
            // them disagree. Refused here rather than reconciled: either half could be the one the caller meant,
            // and a file that quietly picks for them is worse than no file — it reads as a complete export while
            // being a narrow one, or the reverse. The DERIVED path is untouched (null ErrorTiers follows
            // @severities by construction), and all-tiers-off stays legal: an export of nothing is honest.
            if (settings.ErrorTiers is { } declared
                && (declared.Refusing || declared.Ordinary) != errorIncluded)
            {
                throw new ArgumentException(
                    $"ErrorTiers (Refusing={declared.Refusing}, Ordinary={declared.Ordinary}) contradicts "
                    + $"Severities ({SeverityTokens(settings.Severities)}): "
                    + "including either error tier requires Error among the severities, and excluding both "
                    + "requires it absent.",
                    nameof(options));
            }
            var sb = new StringBuilder();

            sb.Append(XmlDeclaration).Append(Crlf);
            AppendRoot(sb, project, profile, settings, generatedAt);
            foreach (ValidationFinding finding in findings)
            {
                AppendFinding(sb, finding);
            }

            sb.Append("</").Append(RootTag).Append('>').Append(Crlf);

            // StrictEncoding rather than the lenient one: it throws on anything outside Latin-1 instead of
            // substituting '?'. Nothing above U+00FF can reach it, because AppendValue has already escaped those
            // to numeric references — so the throw is a guard on this writer, not a limit on what it can export.
            return ProjectFile.StrictEncoding.GetBytes(sb.ToString());
        }

        private static void AppendRoot(
            StringBuilder sb,
            Project project,
            ValidationProfile profile,
            FindingExportOptions options,
            DateTimeOffset generatedAt)
        {
            sb.Append('<').Append(RootTag);
            AppendAttribute(sb, "version", FormatVersion);
            AppendAttribute(sb, "source", options.SourceName ?? string.Empty);

            // Invariant culture and a fixed pattern: a machine-readable stamp must not acquire a calendar or a
            // separator from whoever happens to run the export.
            AppendAttribute(sb, "generated", generatedAt.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture));
            AppendAttribute(sb, "saved_stamp", project.Id2 ?? string.Empty);
            AppendAttribute(sb, "order", options.Order);

            AppendAttribute(sb, "severities", SeverityTokens(options.Severities));

            AppendAttribute(sb, "error_tiers", ErrorTiers(options));

            AppendAttribute(sb, "rules_not_run", string.Join(' ', RulesNotRun(profile)));
            sb.Append('>').Append(Crlf);
        }

        /// <summary>
        /// The severity set as tokens, in ENUM order rather than the caller's: this attribute answers "which
        /// tiers could appear", and the answer is a set. Letting a caller's click order through would make two
        /// identical filters produce two different files.
        /// <para>
        /// One helper rather than the expression written twice, because the contradiction guard quotes this
        /// attribute back at the caller — a separator or a spelling that changed in one place and not the other
        /// would make the refusal describe a file the writer does not emit.
        /// </para>
        /// </summary>
        private static string SeverityTokens(EquatableArray<ValidationSeverity> severities) =>
            string.Join(' ', severities.Order().Select(s => s.ToString()));

        /// <summary>
        /// Which halves of the Error severity the caller included, as tokens.
        /// <para>
        /// ALWAYS emitted, and a list rather than a flag. The first shape tried was an optional boolean
        /// present only when the two halves were filtered differently, so its ABSENCE carried the meaning
        /// "both included". That inverts under every ordinary reading of an optional boolean — deserialising
        /// to <c>bool</c>, or <c>(bool?)a ?? false</c> — turning the commonest state into its opposite. No
        /// amount of schema documentation fixes a default that lies.
        /// </para>
        /// <para>
        /// Empty when neither half was included, which is a state <c>@severities</c> also records by omitting
        /// <c>Error</c>; the two agree by construction because both are computed from the same input. The
        /// tokens are the SDK's own words: a producer that does not split its errors says
        /// <c>refusing ordinary</c> or nothing at all, and never has to know the host's word "fatal".
        /// </para>
        /// </summary>
        private static string ErrorTiers(FindingExportOptions options)
        {
            // A producer with no split follows @severities: Error in means both halves, Error out means
            // neither. That keeps the two attributes consistent without the caller restating one of them.
            bool bothHalves = options.Severities.Contains(ValidationSeverity.Error);
            ErrorTierFilter tiers = options.ErrorTiers ?? new ErrorTierFilter(bothHalves, bothHalves);

            return (tiers.Refusing, tiers.Ordinary) switch
            {
                (true, true) => "refusing ordinary",
                (true, false) => "refusing",
                (false, true) => "ordinary",
                _ => string.Empty,
            };
        }

        /// <summary>
        /// The codes this profile could not evaluate, ordinal-sorted.
        /// <para>
        /// The EVALUABILITY axis only — a rule needing controller limits or a library the caller did not supply —
        /// which is what <see cref="ValidationProfile.CanEvaluate"/> answers. Deliberately NOT
        /// <see cref="ValidationProfile.Includes"/>, which also answers the AUDIENCE question: a structural
        /// profile legitimately omits the whole Documentation family because that audience does not want it, and
        /// listing 30-odd rows as "not run" would turn a deliberate scope into an apology.
        /// </para>
        /// </summary>
        private static IEnumerable<string> RulesNotRun(ValidationProfile profile) =>
            ProblemCatalog.Current.Entries
                .Where(entry => !profile.CanEvaluate(entry))
                .Select(entry => entry.Code.Value)
                .OrderBy(code => code, StringComparer.Ordinal);

        private static void AppendFinding(StringBuilder sb, ValidationFinding finding)
        {
            sb.Append(Indent).Append('<').Append(FindingTag);

            // Enum member names verbatim. Lowercasing them here would only oblige every reader to title-case them
            // back, and @severities would have had to repeat the mapping a third time.
            AppendAttribute(sb, "severity", finding.Severity.ToString());
            AppendAttribute(sb, "code", finding.Code.Value);
            AppendAttribute(sb, "category", finding.Category.ToString());

            // Beside the other three classification attributes rather than with the trailing sites, because it
            // is one: it says what this row COSTS, which is the question @severity answers only half of. Two
            // findings can share a severity and differ in whether the project can be written at all. Omitted
            // entirely when the row refuses nothing, which is the overwhelming majority — an empty blocks="" and
            // no attribute would be the same statement written two ways.
            if (!finding.RefusedOperations.IsEmpty)
            {
                AppendAttribute(
                    sb, "blocks", string.Join(' ', finding.RefusedOperations.Select(op => op.Value)));
            }

            // A finding about the project as a whole has no site, and says so by carrying no locator at all
            // rather than a sentinel a reader would have to know to decode.
            if (finding.Primary?.Locator is { } locator)
            {
                AppendAttribute(sb, "locator", locator);
            }

            AppendAttribute(sb, "message", finding.Problem.Message);
            AppendArguments(sb, finding.Problem);
            AppendSites(sb, finding);
            sb.Append("/>").Append(Crlf);
        }

        /// <summary>
        /// The three trailing attributes: the related sites' locators, the primary's exact path, and the
        /// related sites' paths.
        /// <para>
        /// Last because they are the widest and the rarest — a grouped finding can list dozens of sites, and
        /// under one line in a hundred carries a path — so keeping them at the right edge leaves the columns a
        /// reader scans in the same place on every line.
        /// </para>
        /// <para>
        /// Space-separated rather than one child element per site: a locator is a <c>_0x</c> token or a bare
        /// tag, and a positional path has no spaces either, so the delimiter needs no escaping of its own. This
        /// is XML's own IDREFS convention, which <c>.vis</c> already uses. The child-element alternative would
        /// take the corpus from 618 lines to roughly 1 100 and put child elements back into a format whose
        /// point is that a finding is one line.
        /// </para>
        /// <para>
        /// What this drops is each related site's own Danish label. Nothing consumes it, it is derived from the
        /// element by one function — so a change in it is reviewable there — and the machine-identity problem
        /// the labels were the only answer to is what <c>related_xpath</c> now solves.
        /// </para>
        /// </summary>
        private static void AppendSites(StringBuilder sb, ValidationFinding finding)
        {
            EquatableArray<FindingLocation> related = finding.Related;
            if (!related.IsEmpty)
            {
                AppendAttribute(sb, "related", string.Join(' ', related.Select(r => r.Locator ?? string.Empty)));
            }

            if (finding.Primary?.Xpath is { } xpath)
            {
                AppendAttribute(sb, "xpath", xpath);
            }

            // ALL or none. The two lists pair by POSITION, so a list with holes would silently attach entry 2
            // to site 3 — worse than carrying no machine identity for the related sites at all. The writer
            // cannot fill a hole either: it is a pure formatter and holds no tree to derive a path from. So a
            // group whose sites are partly ambiguous and partly resolved degrades to no list rather than to a
            // wrong one. Whether that can arise is a question about the rules, and it is asserted over the
            // corpus rather than assumed here.
            if (!related.IsEmpty && related.All(r => r.Xpath is not null))
            {
                AppendAttribute(sb, "related_xpath", string.Join(' ', related.Select(r => r.Xpath)));
            }
        }

        /// <summary>
        /// The finding's arguments as <c>arg_&lt;slot&gt;</c> attributes: the slots its catalogue row DECLARES,
        /// in the order the row declares them, and only those.
        /// <para>
        /// Declared slots, not bound arguments, because the two are not the same set. Three shipping codes bind
        /// an argument no row declares — one is diagnostic-only, with nowhere in <c>Slots</c> to live, and one
        /// comes from a raise site three rules share where only two render it. Both bindings are legitimate
        /// where they are, and neither is a fact this file can describe: a reader looking up
        /// <c>arg_noun</c> in the catalogue would find nothing. Emitting what the row declares keeps the file's
        /// vocabulary equal to the catalogue's, which is also what makes the conformance gate over the oracles
        /// expressible at all.
        /// </para>
        /// <para>
        /// Declared ORDER rather than bound order, so two findings of one code always read the same way
        /// regardless of the sequence their rule happened to bind in. A declared slot with nothing bound is
        /// omitted rather than emitted empty: "the rule supplied nothing" is not a fact about the project.
        /// </para>
        /// <para>
        /// The <c>arg_</c> prefix is load-bearing rather than decoration. One catalogue slot is named
        /// <c>id</c> and two more take <c>element</c> and <c>version</c>, all of which this format either uses
        /// or reserves; and the slot namespace grows with the catalogue, so it cannot be kept clear by
        /// convention.
        /// </para>
        /// </summary>
        private static void AppendArguments(StringBuilder sb, Problem problem)
        {
            // The catalogue is already indexed by code, so this is a hash lookup rather than a scan — a second
            // map here would be a copy of one the catalogue keeps for exactly this question.
            if (!ProblemCatalog.Current.TryGet(problem.Code, out ProblemCatalogEntry entry))
            {
                // A code with no catalogue row — a host's own, or the unexpected-failure problem. There is
                // nothing declaring what its arguments mean, so none are emitted, and the file stays writable.
                return;
            }

            foreach (ProblemArgumentSlot slot in entry.Slots)
            {
                foreach (ProblemArgument argument in problem.Arguments)
                {
                    if (argument.Name == slot.Name)
                    {
                        // The SAME formatter the message binder uses, so a value cannot be spelled one way
                        // inside the sentence and another way in the attribute beside it.
                        AppendAttribute(sb, "arg_" + slot.Name, ProblemTemplate.Format(argument.Value));
                        break;
                    }
                }
            }
        }

        private static void AppendAttribute(StringBuilder sb, string name, string value)
        {
            sb.Append(' ').Append(name).Append('=').Append('"');
            AppendValue(sb, value);
            sb.Append('"');
        }

        /// <summary>
        /// The escape rule: the shared <c>.vis</c> escaping for everything Latin-1 can hold, plus numeric
        /// character references for everything it cannot.
        /// <para>
        /// The apostrophe is left LITERAL. XML does not require escaping it inside a double-quoted value, and
        /// Danish findings quote the user's own names constantly — three messages in five contain one, so
        /// escaping would fill the file with <c>&amp;apos;</c> for no reader's benefit.
        /// </para>
        /// <para>
        /// Above U+00FF the character has no Latin-1 byte at all, so it becomes <c>&amp;#NNNN;</c>. Surrogate
        /// pairs are combined into one code point FIRST: emitting each half separately would produce two
        /// references that no parser recombines, silently corrupting the character. This rule lives here rather
        /// than in <see cref="XmlText.AppendEscaped"/> because three byte-fidelity suites depend on that method
        /// throwing rather than escaping.
        /// </para>
        /// </summary>
        private static void AppendValue(StringBuilder sb, string value)
        {
            // The overwhelmingly common case: Danish findings are Latin-1 throughout, so the whole value is one
            // run and it goes straight through without the substring a split would cost.
            int start = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c <= 0xFF)
                {
                    continue;
                }

                Flush(sb, value, start, i);

                int codePoint = char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1])
                    ? char.ConvertToUtf32(c, value[++i])
                    : c;
                sb.Append("&#").Append(codePoint.ToString(CultureInfo.InvariantCulture)).Append(';');
                start = i + 1;
            }

            Flush(sb, value, start, value.Length);
        }

        /// <summary>The Latin-1 run <c>[start, end)</c>, escaped — or the whole string uncopied when it is all one run.</summary>
        private static void Flush(StringBuilder sb, string value, int start, int end)
        {
            if (start >= end)
            {
                return;
            }

            XmlText.AppendEscaped(
                sb,
                start == 0 && end == value.Length ? value : value[start..end],
                escapeApostrophe: false);
        }
    }
}
