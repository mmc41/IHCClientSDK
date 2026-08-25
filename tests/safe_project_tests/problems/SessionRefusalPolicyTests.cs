using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The one layer fact the retired error-origin inventory protected that a type cannot: the session layer
    /// refuses ONLY through its typed channels.
    ///
    /// <para><b>What replaced the inventory, and what did not.</b> "Every user-facing refusal carries a code"
    /// is enforced by construction — <c>EditVerdict.Refuse</c>, <c>EditRefusedException</c>,
    /// <c>PreviewOutcome.Refused</c> and the coded IO exceptions all demand a problem — so a per-file count of
    /// every throw in the engine bought churn, not detection. What construction cannot enforce is a session
    /// command bypassing those channels with a plain BCL throw: its message would reach a user as a raw English
    /// diagnostic instead of a coded Danish refusal. That is the regression this scan still watches for, in the
    /// one layer where every error is a user-facing outcome.</para>
    ///
    /// <para><b>Elsewhere a plain throw stays legitimate.</b> Below the session layer it is a FAILURE — an
    /// English diagnostic for the log behind the catch-all label — so the rest of the engine is deliberately
    /// out of scope.</para>
    /// </summary>
    [TestFixture]
    public sealed class SessionRefusalPolicyTests
    {
        /// <summary>A plain BCL operation throw — the shape a refusal must never take in this layer.</summary>
        private static readonly Regex PlainOperationThrow = new(
            @"throw new (?:InvalidOperationException|IOException|FormatException|NotSupportedException)\(",
            RegexOptions.Compiled);

        /// <summary>The typed channels, matched only to prove the scan is looking at the right layer.</summary>
        private static readonly Regex TypedRefusal = new(
            @"EditVerdict\.Refuse\(|throw new EditRefusedException\(|PreviewOutcome\.Refused\(",
            RegexOptions.Compiled);

        [Test]
        public void TheSessionLayerRefusesOnlyThroughItsTypedChannels()
        {
            string sessionRoot = Path.Combine(TestRepository.RequireRoot(), "ihcclient", "src", "vis", "session");
            ImmutableArray<(string Name, string Source)> files =
            [
                .. Directory.EnumerateFiles(sessionRoot, "*.cs", SearchOption.AllDirectories)
                    .OrderBy(f => f, System.StringComparer.Ordinal)
                    .Select(f => (Path.GetFileName(f), File.ReadAllText(f))),
            ];

            string[] offenders = [.. files
                .Where(f => PlainOperationThrow.IsMatch(f.Source))
                .Select(f => f.Name)];

            Assert.Multiple(() =>
            {
                Assert.That(files, Is.Not.Empty, "non-vacuity: the session layer is where this scan expects it");
                Assert.That(
                    files.Count(f => TypedRefusal.IsMatch(f.Source)), Is.GreaterThan(0),
                    "non-vacuity: typed refusals really are raised in this layer");
                Assert.That(
                    offenders, Is.Empty,
                    "these session files raise a plain BCL exception — a session error is a user-facing outcome, "
                    + "so refuse through EditVerdict.Refuse, EditRefusedException or PreviewOutcome.Refused, "
                    + "which all carry a coded Danish problem");
            });
        }
    }
}
