using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The <c>arg_*</c> attributes: which of a finding's arguments reach the file, in what order, and spelled
    /// how.
    ///
    /// <para><b>The rule is DECLARED slots, not bound arguments.</b> Those are not the same set. Three shipping
    /// codes bind an argument their catalogue row does not declare, for two different and both legitimate
    /// reasons — a diagnostic-only value that <c>Slots</c> has nowhere to hold, and a raise site shared by
    /// three rules of which only two render the value. Emitting "whatever was bound" would put those into the
    /// file, where nothing declares what they mean; emitting declared slots keeps the file's vocabulary equal
    /// to the catalogue's.</para>
    ///
    /// <para><b>Why the prefix.</b> One catalogue slot is literally named <c>id</c>, and two more take
    /// <c>element</c> and <c>version</c> — names this format reserves for other purposes. Unprefixed, one of
    /// those would have to be renamed, and the slot namespace is not a curated set that could be kept clear.</para>
    /// </summary>
    [TestFixture]
    public sealed class FindingExportArgumentTests
    {
        private static ValidationFinding Finding(string code, params ProblemArgument[] arguments) =>
            new(
                new Problem(new ProblemCode(code), "besked", EquatableArray.CreateRange(arguments)),
                ValidationSeverity.Warning,
                ValidationCategory.ProjectStructure,
                new FindingLocation("_0x2132", null, null),
                EquatableArray<FindingLocation>.Empty);

        private static string Line(params ProblemArgument[] arguments) => Line("inline-constant", arguments);

        private static string Line(string code, params ProblemArgument[] arguments)
        {
            byte[] bytes = FindingExportWriter.Write(
                FindingExportProbe.Stamped(), [Finding(code, arguments)], ValidationProfile.Categorized,
                FindingExportOptions.Default, FindingExportProbe.Instant);
            return ProjectFile.Encoding.GetString(bytes)
                .Split("\r\n").First(l => l.Contains("<finding "));
        }

        /// <summary>The <c>arg_</c> attribute names of one emitted line, in emitted order.</summary>
        private static ImmutableArray<string> ArgNames(string line) =>
        [
            .. line.Split(' ')
                .Where(token => token.StartsWith("arg_", StringComparison.Ordinal))
                .Select(token => token.Split('=')[0]),
        ];

        // ----- how many -----

        /// <summary>A code whose row declares no slots carries no <c>arg_</c> attribute at all.</summary>
        [Test]
        public void ACodeThatDeclaresNoSlotsCarriesNoArgumentAttributes()
        {
            string line = Line("luid-ceiling");

            Assert.Multiple(() =>
            {
                Assert.That(ArgNames(line), Is.Empty);
                Assert.That(line, Does.Not.Contain("arg_"));
            });
        }

        /// <summary>One slot, one attribute.</summary>
        [Test]
        public void ASingleDeclaredSlotBecomesASingleArgumentAttribute()
        {
            string line = Line("struct-product-no-terminals", new ProblemArgument("product", "Stue"));

            Assert.Multiple(() =>
            {
                Assert.That(ArgNames(line), Is.EqualTo(new[] { "arg_product" }));
                Assert.That(line, Does.Contain(" arg_product=\"Stue\""));
            });
        }

        /// <summary>
        /// The widest row in the catalogue — five slots — in DECLARED order, which is deliberately not the
        /// order the arguments were bound in. Order comes from the row, so two findings of one code always
        /// read the same way.
        /// </summary>
        [Test]
        public void FiveDeclaredSlotsAreEmittedInDeclaredOrderNotBoundOrder()
        {
            string line = Line(
                new ProblemArgument("value", "on"),
                new ProblemArgument("attribute", "state"),
                new ProblemArgument("parent", "product"),
                new ProblemArgument("id", "_0x2132"),
                new ProblemArgument("tag", "output"));

            Assert.That(
                ArgNames(line),
                Is.EqualTo(new[] { "arg_tag", "arg_id", "arg_parent", "arg_attribute", "arg_value" }));
        }

        /// <summary>
        /// Arguments sit after <c>message</c>, which keeps the left edge of every line column-comparable: the
        /// columns a reader scans are identity and prose, and the payload is what varies in width.
        /// </summary>
        [Test]
        public void ArgumentsFollowTheFixedAttributes()
        {
            string line = Line("struct-product-no-terminals", new ProblemArgument("product", "Stue"));

            Assert.That(
                line.IndexOf(" arg_product=", StringComparison.Ordinal),
                Is.GreaterThan(line.IndexOf(" message=", StringComparison.Ordinal)));
        }

        // ----- the undeclared-argument rule -----

        /// <summary>
        /// <c>link-bijection</c> and <c>scene-bijection</c> bind a <c>noun</c> their rows do not declare. It is
        /// a real binding — the English diagnostic renders it — but the diagnostic is not in this file, and a
        /// slot the catalogue never declared has no meaning a reader could look up. Declared slots only.
        /// </summary>
        [TestCase("link-bijection")]
        [TestCase("scene-bijection")]
        public void ADiagnosticOnlyArgumentIsNotEmitted(string code)
        {
            string line = Line(
                code,
                new ProblemArgument("tag", "link"),
                new ProblemArgument("id", "_0x2132"),
                new ProblemArgument("noun", "forbindelse"));

            Assert.Multiple(() =>
            {
                Assert.That(ArgNames(line), Is.EqualTo(new[] { "arg_tag", "arg_id" }));
                Assert.That(line, Does.Not.Contain("arg_noun"));
            });
        }

        /// <summary>
        /// <c>luid-low</c> declares no slots at all, yet its raise site — shared with two rules that DO render
        /// a value — binds one. Nothing of it reaches the file.
        /// </summary>
        [Test]
        public void AnArgumentFromASharedRaiseSiteIsNotEmitted()
        {
            string line = Line("luid-low", new ProblemArgument("value", "_0x2a"));

            Assert.Multiple(() =>
            {
                Assert.That(ArgNames(line), Is.Empty);
                Assert.That(line, Does.Not.Contain("arg_value"));
            });
        }

        /// <summary>
        /// A declared slot with nothing bound to it is absent rather than empty. The alternative would put an
        /// attribute in the file whose value means "the rule did not supply this", which is not a fact about
        /// the project.
        /// </summary>
        [Test]
        public void ADeclaredSlotWithNoBoundArgumentIsOmitted()
        {
            string line = Line(new ProblemArgument("tag", "output"));

            Assert.That(ArgNames(line), Is.EqualTo(new[] { "arg_tag" }));
        }

        // ----- spelling -----

        /// <summary>
        /// A fractional value is formatted invariantly, so a Danish machine writes <c>1.5</c> and not
        /// <c>1,5</c>. The same rule the message binder already applies, reused rather than restated — a second
        /// formatter is how the same number ends up spelled two ways on one line.
        /// </summary>
        [Test]
        public void AFractionalNumberIsFormattedInvariantly()
        {
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("da-DK");
                string line = Line(
                    "scene-long-delay",
                    new ProblemArgument("seconds", 1.5),
                    new ProblemArgument("limit", 60.0));

                Assert.Multiple(() =>
                {
                    Assert.That(line, Does.Contain(" arg_seconds=\"1.5\""));
                    Assert.That(line, Does.Contain(" arg_limit=\"60\""));
                    Assert.That(line, Does.Not.Contain("1,5"), "a comma here would be the machine's locale leaking in");
                });
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        /// <summary>An integer argument keeps its plain digits.</summary>
        [Test]
        public void AnIntegerArgumentIsEmittedAsItsDigits()
        {
            string line = Line("id-duplicate-token",
                new ProblemArgument("id", "_0x2132"), new ProblemArgument("count", 2));

            Assert.That(line, Does.Contain(" arg_count=\"2\""));
        }

        /// <summary>
        /// An argument value is escaped exactly like any other attribute value — it is user data and can carry
        /// anything a name can.
        /// </summary>
        [Test]
        public void AnArgumentValueIsEscapedLikeEveryOtherAttribute()
        {
            string line = Line("struct-product-no-terminals",
                new ProblemArgument("product", "A&B <\"x\"> 'y' " + (char)0x20AC));

            Assert.That(line, Does.Contain(" arg_product=\"A&amp;B &lt;&quot;x&quot;&gt; 'y' &#8364;\""));
        }

        /// <summary>
        /// Every emitted argument value also appears inside the same line's <c>message</c>. The two are one
        /// datum rendered twice on purpose: the message is what a person reads and is never re-derived
        /// downstream, the arguments are what makes a line re-renderable and groupable. This is what keeps
        /// them from drifting apart.
        /// </summary>
        [Test]
        public void EveryEmittedArgumentValueAlsoAppearsInsideTheMessage()
        {
            ProblemCatalogEntry entry = ProblemCatalog.Current.Entries
                .First(e => e.Code.Value == "struct-product-no-terminals");
            Problem bound = new(
                entry.Code, string.Empty,
                EquatableArray.Create<ProblemArgument>([new ProblemArgument("product", "Stue")]));
            bound = bound with { Message = entry.BindTemplate(bound) };

            byte[] bytes = FindingExportWriter.Write(
                FindingExportProbe.Stamped(),
                [
                    new ValidationFinding(
                        bound, ValidationSeverity.Warning, ValidationCategory.ProjectStructure,
                        new FindingLocation("_0x2132", null, null), EquatableArray<FindingLocation>.Empty),
                ],
                ValidationProfile.Categorized, FindingExportOptions.Default, FindingExportProbe.Instant);
            string line = ProjectFile.Encoding.GetString(bytes).Split("\r\n").First(l => l.Contains("<finding "));

            Assert.Multiple(() =>
            {
                Assert.That(line, Does.Contain(" arg_product=\"Stue\""));
                Assert.That(line.Split(" message=\"")[1].Split("\" ")[0], Does.Contain("Stue"));
            });
        }

        /// <summary>
        /// A code with no catalogue row at all — a host's own, or the unexpected-failure problem — emits no
        /// arguments rather than throwing. The file stays writable for a finding the SDK's catalogue does not
        /// describe.
        /// </summary>
        [Test]
        public void ACodeWithNoCatalogueRowEmitsNoArgumentsRatherThanFailing()
        {
            string line = Line("app.openvisual.not-an-sdk-code", new ProblemArgument("whatever", "x"));

            Assert.That(ArgNames(line), Is.Empty);
        }
    }
}
