using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// RF-11: a guard that can only refuse from inside <c>Execute</c> keeps its OWN refusal identity.
    ///
    /// <para><c>edit.deep-guard</c> exists to say "this refusal came from below the gate, so there was no verdict
    /// to take a code from". That is true about WHERE the refusal was raised and says nothing about WHAT was
    /// refused — so using it for every deep guard threw away seven distinct identities, and a caller filtering on
    /// a code got one bucket instead of the conditions the catalogue publishes.</para>
    ///
    /// <para>The evidence it was thrown away rather than merely unused: <c>edit.section-not-variables</c>,
    /// <c>edit.section-rejects-enum</c> and <c>edit.variable-not-added</c> each had a declared code AND a
    /// catalogue entry, yet appeared nowhere in the SDK outside the catalogue — their conditions are raised, but
    /// only ever reported as <c>edit.deep-guard</c>. Two more, <c>edit.terminal-address-range</c> and
    /// <c>edit.enum-value-missing</c>, were reachable through the shallow verdict and NOT through the deep guard,
    /// so one condition answered to two different codes depending on which path happened to catch it.</para>
    /// </summary>
    [TestFixture]
    public sealed class DeepGuardIdentityTests
    {
        [Test]
        public void ADeepGuardCarriesTheCodeItWasRaisedWith()
        {
            EditRefusedException refusal = new(
                EditRefusalCodes.TerminalAddressRange, "Klemmenummeret ligger uden for datalinjens område.");

            Assert.Multiple(() =>
            {
                Assert.That(refusal.Code, Is.EqualTo(EditRefusalCodes.TerminalAddressRange),
                    "the guard names what it refused, not merely where it refused from");
                Assert.That(refusal.Message, Is.EqualTo("Klemmenummeret ligger uden for datalinjens område."));
            });
        }

        /// <summary>
        /// The fallback is still there and still means what it says: a guard that names no code refused from
        /// below the gate with nothing more specific to offer.
        /// </summary>
        [Test]
        public void AGuardThatNamesNoCodeStillReportsDeepGuard()
        {
            EditRefusedException refusal = new("noget gik galt.");

            Assert.That(refusal.Code, Is.EqualTo(EditRefusalCodes.DeepGuard));
        }

        /// <summary>
        /// THE POINT OF THE TASK, stated as a property rather than as one example: every deep-guard site in the
        /// SDK names a code. A site that does not is one whose condition is invisible to a caller that filters.
        /// </summary>
        [Test]
        public void EveryDeepGuardSiteNamesACode()
        {
            string[] anonymous =
            [
                .. GuardSites()
                    .Where(site => !site.Text.Contains("EditRefusalCodes.", StringComparison.Ordinal))
                    .Select(site => $"{site.File}:{site.Number}: {site.Text.Trim()}")
            ];

            Assert.That(anonymous, Is.Empty,
                "these deep guards still refuse without an identity, so their condition reports as "
                + "edit.deep-guard:" + Environment.NewLine + string.Join(Environment.NewLine, anonymous));
        }

        /// <summary>
        /// The three codes that had a catalogue entry and NO raiser now have one. Named explicitly, because
        /// "declared but unreachable" is the state this task found them in and a regression would be silent.
        /// </summary>
        [Test]
        public void TheCodesThatHadNoRaiserNowHaveOne()
        {
            ProblemCode[] wereUnreachable =
            [
                EditRefusalCodes.SectionNotVariables,
                EditRefusalCodes.SectionRejectsEnum,
                EditRefusalCodes.VariableNotAdded,
            ];

            List<(string File, int Number, string Text)> sites = [.. GuardSites()];

            Assert.Multiple(() =>
            {
                foreach (ProblemCode code in wereUnreachable)
                {
                    string member = Member(code);
                    Assert.That(
                        sites.Any(s => s.Text.Contains($"EditRefusalCodes.{member}", StringComparison.Ordinal)),
                        Is.True,
                        $"{code.Value} still has no deep-guard site raising it");
                }
            });
        }

        /// <summary>
        /// The one deep guard that cannot name a code at its site: <c>ProjectEditor.Resolve</c>. The layer rule
        /// keeps <c>EditRefusalCodes</c> out of <c>Ihc.Vis.Editing</c>, so the pairing lives behind the one type
        /// that layer may name — and this asserts the factory really does carry the identity, which is what the
        /// source scan would otherwise have proved.
        /// </summary>
        [Test]
        public void TheEditingLayersStaleIdGuardCarriesTargetMissing()
        {
            EditRefusedException refusal = EditRefusedException.TargetMissing("Elementet");

            Assert.Multiple(() =>
            {
                Assert.That(refusal.Code, Is.EqualTo(EditRefusalCodes.TargetMissing));
                Assert.That(refusal.Message, Is.EqualTo("Elementet findes ikke længere."),
                    "the same sentence RequireExists composes, from the same owner");
            });
        }

        /// <summary>The C# member name for a code: the last dotted segment, pascal-cased from kebab.</summary>
        private static string Member(ProblemCode code) =>
            string.Concat(code.Value.Split('.')[^1].Split('-')
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

        /// <summary>
        /// Every place in the SDK that constructs an <see cref="EditRefusedException"/>, as a whole STATEMENT
        /// rather than a line: a construction that names its code on a continuation line is still a construction
        /// that names its code, and a line-wise scan would report it as anonymous.
        /// </summary>
        private static IEnumerable<(string File, int Number, string Text)> GuardSites()
        {
            string root = TestRepository.RequireRoot();
            string[] files =
            [
                // ProjectEditor is deliberately NOT scanned: it lives in Ihc.Vis.Editing, which the architecture
                // permits to name exactly ONE type from Ihc.Vis.Session, so it cannot say EditRefusalCodes at
                // all. Its stale-id guard goes through EditRefusedException.TargetMissing instead, and that
                // factory is asserted directly below.
                "ihcclient/src/vis/session/MetadataCommands.cs",
                "ihcclient/src/vis/session/ProductCommands.cs",
            ];

            var found = new List<(string, int, string)>();
            foreach (string relative in files)
            {
                string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                Assert.That(File.Exists(path), Is.True, $"a scanned source file is missing at {path}");
                string text = File.ReadAllText(path, Encoding.UTF8);
                foreach (Match match in Regex.Matches(
                    text, @"new\s+(?:Ihc\.Vis\.Session\.)?EditRefusedException\([^;]*?\)", RegexOptions.Singleline))
                {
                    int line = text.Take(match.Index).Count(c => c == '\n') + 1;
                    found.Add((relative, line, Regex.Replace(match.Value, @"\s+", " ")));
                }
            }

            Assert.That(found, Is.Not.Empty, "the scan must find deep guards, or this gate is vacuous");
            return found;
        }
    }
}
