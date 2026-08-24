using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Declared argument slots, and the property that makes a wrong argument a BUILD failure rather than a
    /// formatting-test failure.
    ///
    /// <para><b>There is no analyzer here, and that is the point.</b> A code that needs data declares its slots on
    /// its catalogue entry and gets a factory whose PARAMETERS are those slots. A caller passing two arguments to
    /// a three-slot code, or a string where an integer is declared, does not compile — so arity and type are
    /// enforced by the compiler, at the call site, for free. The build-time gate this task once specified would
    /// have re-implemented, later and worse, what the language already does.</para>
    ///
    /// <para><b>What is left to prove, and how it is kept non-vacuous.</b> Two things: that a factory's parameters
    /// really do follow its entry's slots, and that the check saying so can fail. The shipped catalogue now
    /// declares slots on 71 of its entries, so the checker runs live over real data — but it is still exercised
    /// first against a FIXTURE catalogue defined here, because the real catalogue has no factory to reflect: its
    /// rules bind arguments at the raise site. The fixture is what keeps the parameters-follow-the-slots half from
    /// being vacuous, and it stays whatever the shipped rows do.</para>
    ///
    /// <para>The compile-failure half was demonstrated by breaking <see cref="Fixture.AddressTaken"/> — dropping a
    /// parameter, then passing a string where the <see cref="ProblemArgumentType.Integer"/> slot is declared — and
    /// observing the build fail on the call sites below, which is the evidence recorded for this task.</para>
    /// </summary>
    [TestFixture]
    public sealed class ProblemArgumentArityTests
    {
        /// <summary>
        /// A slotted code and its typed factory, in the shape every slotted row follows: the entry declares the
        /// slots, and the factory beside it takes exactly those slots as real parameters, in order.
        /// </summary>
        internal static class Fixture
        {
            internal static ProblemCatalogEntry AddressTakenEntry =>
                new(
                    new ProblemCode("fixture-address-taken"),
                    ProblemCatalogSection.ProjectFindings,
                    ValidationCategory.Addressing,
                    CatalogDisposition.Error,
                    RuleKind.UserContentRule,
                    RuleFaces.WholeProject,
                    new RuleTarget("dataline_input", "address_dataline"),
                    FindingShape.PrimaryWithRelated,
                    EquatableArray.Create<ProblemArgumentSlot>(
                    [
                        new ProblemArgumentSlot("address", ProblemArgumentType.Integer),
                        new ProblemArgumentSlot("owner", ProblemArgumentType.AuthoredName),
                    ]),
                    "Adresse {address} er allerede brugt af {owner}");

            /// <summary>The typed factory: one parameter per declared slot, in declared order.</summary>
            internal static Problem AddressTaken(int address, string owner) =>
                new(AddressTakenEntry.Code, AddressTakenEntry.MessageTemplate,
                    EquatableArray.Create<ProblemArgument>(
                    [
                        new ProblemArgument("address", address),
                        new ProblemArgument("owner", owner),
                    ]));
        }

        /// <summary>The CLR type a declared slot kind accepts. The one place the mapping is written down.</summary>
        private static readonly Dictionary<ProblemArgumentType, Type> SlotTypes = new()
        {
            [ProblemArgumentType.ElementIdentity] = typeof(string),
            [ProblemArgumentType.SchemaName] = typeof(string),
            [ProblemArgumentType.AuthoredName] = typeof(string),
            [ProblemArgumentType.Integer] = typeof(int),
            [ProblemArgumentType.Number] = typeof(double),
            [ProblemArgumentType.AttributeValue] = typeof(string),
            [ProblemArgumentType.Path] = typeof(string),
        };

        [Test]
        public void EverySlotKindHasOneDeclaredClrType()
        {
            Assert.That(SlotTypes.Keys, Is.EquivalentTo(Enum.GetValues<ProblemArgumentType>()),
                "a slot kind with no CLR type could not be checked, and would silently accept anything");
        }

        [Test]
        public void TheFactoryParametersFollowTheDeclaredSlotsInOrder()
        {
            MethodInfo factory = typeof(Fixture).GetMethod(nameof(Fixture.AddressTaken),
                BindingFlags.NonPublic | BindingFlags.Static)!;
            ProblemArgumentSlot[] slots = [.. Fixture.AddressTakenEntry.Slots];
            ParameterInfo[] parameters = factory.GetParameters();

            Assert.Multiple(() =>
            {
                Assert.That(parameters, Has.Length.EqualTo(slots.Length),
                    "a wrong parameter COUNT is a compile error at every call site; this asserts the declaration agrees");
                for (int i = 0; i < slots.Length; i++)
                {
                    Assert.That(parameters[i].Name, Is.EqualTo(slots[i].Name), $"slot {i}");
                    Assert.That(parameters[i].ParameterType, Is.EqualTo(SlotTypes[slots[i].Type]), $"slot {i}");
                }
            });
        }

        [Test]
        public void AProblemFromTheFactoryCarriesExactlyTheDeclaredSlotsAndBindsItsTemplate()
        {
            Problem problem = Fixture.AddressTaken(42, "Stue loft");

            Assert.Multiple(() =>
            {
                Assert.That(problem.Arguments.Select(a => a.Name),
                    Is.EqualTo(Fixture.AddressTakenEntry.Slots.Select(s => s.Name)).AsCollection);
                Assert.That(problem.Arguments.Select(a => a.Value.GetType()),
                    Is.EqualTo(new[] { typeof(int), typeof(string) }).AsCollection,
                    "the declared TYPE is the rarer and more valuable half: arity stops the wrong NUMBER of "
                    + "values, types stop the wrong ones");
                Assert.That(Fixture.AddressTakenEntry.BindTemplate(problem),
                    Is.EqualTo("Adresse 42 er allerede brugt af Stue loft"));
            });
        }

        /// <summary>
        /// The checker, run over the FIXTURE first so it is exercised against slots that exist, then over the real
        /// catalogue — where it is live: 71 shipped entries declare slots, and every one of their templates has to
        /// bind each declared slot rather than leave a visible <c>{placeholder}</c>.
        /// </summary>
        [Test]
        public void EveryEntryDeclaringSlotsBindsThemWithoutLeavingAPlaceholder()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Unbound(Fixture.AddressTakenEntry, Fixture.AddressTaken(1, "x")), Is.Empty,
                    "the fixture proves this check can be satisfied");

                foreach (ProblemCatalogEntry entry in ProblemCatalog.Current.Entries.Where(e => !e.Slots.IsEmpty))
                {
                    Problem sample = new(entry.Code, entry.MessageTemplate,
                        EquatableArray.CreateRange(entry.Slots.Select(s =>
                            new ProblemArgument(s.Name, Sample(s.Type)))));
                    Assert.That(Unbound(entry, sample), Is.Empty, entry.Code.Value);
                }
            });
        }

        /// <summary>
        /// BOTH DIRECTIONS, over the whole SDK catalogue: every placeholder in a template is a declared slot, and
        /// every declared slot appears in its template.
        ///
        /// <para><b>Neither direction was checked before, and both were violated.</b>
        /// <see cref="EveryEntryDeclaringSlotsBindsThemWithoutLeavingAPlaceholder"/> filters on
        /// <c>!e.Slots.IsEmpty</c>, so the twelve <c>edit.*</c> entries that carried placeholders and declared NO
        /// slots were skipped entirely; and nothing anywhere read template → slot, so three wiring rows declared a
        /// count no sentence used. Fifteen mismatches in all. The HOST family has had exactly this check since it
        /// was minted (<c>HostPhrasingStandardTests.EveryHostPlaceholderIsADeclaredSlotAndEverySlotIsUsed</c>);
        /// this is the SDK family being held to its own standard.</para>
        ///
        /// <para>An entry with neither placeholders nor slots passes trivially and is the common case — the
        /// non-vacuity guard is <see cref="TheSlotComparisonSeesRealDeclarations"/> below.</para>
        /// </summary>
        [Test]
        public void EverySdkPlaceholderIsADeclaredSlotAndEverySlotIsUsed()
        {
            Assert.Multiple(() =>
            {
                foreach (ProblemCatalogEntry entry in ProblemCatalog.Current.Entries)
                {
                    Assert.That(Placeholders(entry.MessageTemplate),
                        Is.EquivalentTo(entry.Slots.Select(s => s.Name)),
                        $"{entry.Code.Value}: template placeholders and declared slots must be the same set");
                }
            });
        }

        /// <summary>
        /// Non-vacuity for the check above: the catalogue really does declare slots and really does use them, so
        /// the two-way comparison is comparing something.
        /// </summary>
        [Test]
        public void TheSlotComparisonSeesRealDeclarations()
        {
            ProblemCatalogEntry[] slotted = [.. ProblemCatalog.Current.Entries.Where(e => !e.Slots.IsEmpty)];

            Assert.Multiple(() =>
            {
                Assert.That(slotted, Has.Length.GreaterThan(50),
                    "the shipped catalogue declares slots on most of its data-carrying rows");
                Assert.That(slotted.Sum(e => e.Slots.Length), Is.GreaterThan(slotted.Length),
                    "and some rows declare more than one, so the set comparison is not a length check in disguise");
                Assert.That(Placeholders("Adresse {address} er brugt af {owner}"),
                    Is.EquivalentTo(new[] { "address", "owner" }),
                    "and the placeholder reader finds both, so a template's half of the comparison is real");
            });
        }

        /// <summary>The placeholder names in a template, in the order they appear.</summary>
        /// <param name="template">The Danish message template to read.</param>
        private static IReadOnlyList<string> Placeholders(string template)
        {
            List<string> names = [];
            for (int open = template.IndexOf('{'); open >= 0; open = template.IndexOf('{', open + 1))
            {
                int close = template.IndexOf('}', open);
                if (close > open + 1)
                {
                    names.Add(template[(open + 1)..close]);
                }
            }

            return names;
        }

        /// <summary>
        /// A template naming a slot the entry does not declare is the defect declared slots exist to stop: it
        /// renders as a visible <c>{placeholder}</c> instead of silently dropping a value.
        /// </summary>
        [Test]
        public void ATemplateNamingAnUndeclaredSlotLeavesItVisible()
        {
            ProblemCatalogEntry mistyped = Fixture.AddressTakenEntry with
            {
                MessageTemplate = "Adresse {address} er allerede brugt af {ownr}",
            };

            Assert.That(mistyped.BindTemplate(Fixture.AddressTaken(42, "Stue loft")),
                Is.EqualTo("Adresse 42 er allerede brugt af {ownr}"));
        }

        private static IReadOnlyList<string> Unbound(ProblemCatalogEntry entry, Problem problem)
        {
            string bound = entry.BindTemplate(problem);
            return [.. entry.Slots.Select(s => "{" + s.Name + "}").Where(p => bound.Contains(p, StringComparison.Ordinal))];
        }

        private static object Sample(ProblemArgumentType type) => type switch
        {
            ProblemArgumentType.Integer => 1,
            ProblemArgumentType.Number => 1.5d,
            _ => "sample",
        };
    }
}
