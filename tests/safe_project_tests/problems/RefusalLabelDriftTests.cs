using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

using Ihc.Vis.Problems;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T063's drift gate: EVERY Danish sentence a refusing site carries is the same words as its catalogue entry's
    /// template — all of them, reflected, rather than the handful each family's own suite happened to sample.
    ///
    /// <para><b>The duplication is deliberate and cannot be removed.</b> A refusing site below the validation engine
    /// may not read the catalogue — the layer rules forbid it — so the words are necessarily written twice: once on
    /// the entry that governs the code, once on the <see cref="RefusalIdentity"/> the site raises. What CAN be
    /// removed is the risk: reflect every identity every registry exposes and require agreement, so an edit to
    /// either side fails the gate instead of shipping two spellings of one sentence.</para>
    ///
    /// <para><b>Why this replaces sampling.</b> The load family pinned four of its causes, the save family three,
    /// the mint factories three — roughly a quarter of the identities in the SDK. The rest were duplicated Danish
    /// text with nothing holding the two copies together, which is exactly the defect T063 sweeps: a string the
    /// master artifact now owns, living a second life where nothing checks it.</para>
    /// </summary>
    [TestFixture]
    public sealed class RefusalLabelDriftTests
    {
        /// <summary>Every refusal identity the SDK's registries expose, with the member it came from.</summary>
        private static ImmutableArray<(string Member, RefusalIdentity Identity)> Identities()
        {
            var found = ImmutableArray.CreateBuilder<(string, RefusalIdentity)>();
            foreach (Type registry in typeof(RefusalIdentity).Assembly.GetTypes()
                .Where(t => t.IsClass && t.IsAbstract && t.IsSealed))
            {
                foreach (PropertyInfo property in registry
                    .GetProperties(BindingFlags.Public | BindingFlags.Static))
                {
                    object? value = property.PropertyType == typeof(RefusalIdentity)
                        || typeof(IEnumerable<RefusalIdentity>).IsAssignableFrom(property.PropertyType)
                            ? property.GetValue(null)
                            : null;

                    switch (value)
                    {
                        case RefusalIdentity identity:
                            found.Add(($"{registry.Name}.{property.Name}", identity));
                            break;

                        // A registry that also exposes an `All` collection is read through it too, so an identity
                        // reachable only from the collection is still governed and still checked.
                        case IEnumerable<RefusalIdentity> many:
                            found.AddRange(many.Select(i => ($"{registry.Name}.{property.Name}", i)));
                            break;

                        default:
                            break;
                    }
                }
            }

            return found.ToImmutable();
        }

        [Test]
        public void EveryRefusalIdentityIsGovernedByAnEntry()
        {
            ImmutableArray<(string Member, RefusalIdentity Identity)> identities = Identities();

            Assert.Multiple(() =>
            {
                // A COUNT would be a magic number; what matters is that every family a registry declares is
                // reachable by reflection. The LOAD family used to be absent here — its registry exposed bare
                // codes and its Danish labels were hand-typed at thirteen sites in `ProjectReader`, so nothing
                // reflective could reach them and its half had to be carried by LoadRefusalTests alone. It now
                // exposes whole identities like its three siblings, so the gate is universal.
                Assert.That(identities.Select(pair => pair.Identity.Operation.Value).Distinct(),
                    Is.SupersetOf(new[] { "io.load", "io.save", "import.catalog", "bridge.download", "bridge.upload" }),
                    "every family that declares whole identities must be reachable by reflection; a family that "
                    + "stopped being found would make this suite quiet rather than failing");
                foreach ((string member, RefusalIdentity identity) in identities)
                {
                    Assert.That(ProblemCatalog.Current.TryGet(identity.Cause, out _), Is.True,
                        $"{member} raises {identity.Cause.Value}, which needs an entry");
                    Assert.That(ProblemCatalog.Current.TryGet(identity.Operation, out _), Is.True,
                        $"{member}'s operation head {identity.Operation.Value} needs an entry too");
                }
            });
        }

        /// <summary>
        /// The ACKNOWLEDGED two-faced set: every cause a refusal registry declares whose catalogue entry is not a
        /// <see cref="CatalogDisposition.Refusal"/> row — a row that REPORTS at validate and REFUSES at some
        /// operation boundary. Each line carries the reason and the test that executes it, and the set is asserted
        /// exactly, so a cause that becomes two-faced fails this gate until someone writes down why.
        /// </summary>
        private static readonly (string Code, string Why)[] TwoFacedCauses =
        [
            ("attr-latin1",
                "Reports at validate as an Error finding; SaveRefusalCodes.AttrLatin1 refuses the save and the "
                + "export, because bytes outside Latin-1 cannot be written in the encoding the file declares. "
                + "Executed by SaveRefusalTests.ANonLatin1AttributeIsRefusedAsAttrLatin1."),
            ("attr-required",
                "Reports at validate as an Error finding; SaveRefusalCodes.AttrRequired refuses the save and the "
                + "export, because the written file would violate the DTD it declares inline. Executed by "
                + "SaveRefusalTests.AMissingRequiredAttributeIsRefusedAsAttrRequired."),
            ("attr-undeclared",
                "Reports at validate as an Error finding, and is the one cause refused on BOTH boundaries: "
                + "SaveRefusalCodes.AttrUndeclared at save and export, EditOpenRefusalCodes.AttrUndeclared at "
                + "edit-open — one row with two operations, not two rows. Executed by "
                + "SaveRefusalTests.AnUndeclaredAttributeIsRefusedAsAttrUndeclared and "
                + "EditOpenRefusalTests.OpeningForEditRefusesWithTheAttrUndeclaredCauseUnderEditOpen."),
            ("id-duplicate-token",
                "Reports at validate as an Error finding, and refuses EDIT-OPEN only — the one row here with no "
                + "save-side sibling: the bytes are perfectly writable and a save tolerates them, but editing "
                + "addresses elements by id, so ambiguous ids would target the wrong element. Refused by "
                + "EditOpenRefusalCodes.IdDuplicateToken; executed by "
                + "EditOpenRefusalTests.TheSessionReportsTheDuplicateIdOpenAsARefusalWithItsCode and "
                + "EditorGuardTests.Edit_DuplicateIds_AsLeadingZeroTokenVariants_AreRejected."),
            ("element-undeclared",
                "Reports at validate as an Error finding; SaveRefusalCodes.ElementUndeclared refuses the save and "
                + "the export, for the same inline-DTD reason as its attribute siblings. Executed by "
                + "SaveRefusalTests.AnUndeclaredElementTypeIsRefusedAsElementUndeclared."),
        ];

        /// <summary>
        /// The reverse direction of <see cref="EveryRefusalIdentityIsGovernedByAnEntry"/>: that test requires an
        /// entry to EXIST for every declared refusal; this one requires every disagreement between the two to be
        /// ACKNOWLEDGED. A cause whose entry says Error or Warning while a registry declares a refusal for it has
        /// two faces — legitimate, and the SDK has several such rows — but it is exactly the shape that hides a
        /// mistake, so it may not appear silently.
        ///
        /// <para><b>The derivation is deliberately broad.</b> The retired gate this replaces reflected over a
        /// hard-coded roster of five registry types; this reads <see cref="Identities()"/>, which walks EVERY
        /// sealed static class in the <see cref="RefusalIdentity"/> assembly. A registry added tomorrow is picked
        /// up with no roster to edit — and can only ADD to the derived side, which the exact-set assert turns into
        /// a visible failure rather than a silent gap.</para>
        ///
        /// <para><b><c>EditRefusalCodes</c> is out of scope by construction</b>, not by omission: it exposes bare
        /// <see cref="ProblemCode"/>s (the dotted <c>edit.*</c> rows, all Refusal-disposition), never whole
        /// identities, so <see cref="Identities()"/> cannot see it and nothing here needs to exclude it.</para>
        /// </summary>
        [Test]
        public void TheCausesRefusedUnderANonRefusalEntryAreExactlyTheAcknowledgedSet()
        {
            ImmutableArray<string> derived =
            [
                .. Identities()
                    .Select(pair => pair.Identity.Cause.Value)
                    .Distinct(StringComparer.Ordinal)
                    // A cause with NO entry is the other test's failure, not this one's: it would otherwise be
                    // counted as "not a Refusal row" and reported here as a two-faced row it is not.
                    .Where(cause => ProblemCatalog.Current.TryGet(new ProblemCode(cause), out ProblemCatalogEntry entry)
                        && entry.Disposition != CatalogDisposition.Refusal)
                    .OrderBy(cause => cause, StringComparer.Ordinal),
            ];

            Assert.Multiple(() =>
            {
                Assert.That(derived,
                    Is.EqualTo(TwoFacedCauses.Select(row => row.Code).OrderBy(c => c, StringComparer.Ordinal))
                        .AsCollection,
                    "every cause that a registry refuses while its entry reports has to be acknowledged here with "
                    + "its reason. If this fails, either a refusal identity was added for an Error or Warning row "
                    + "— write the line, that is the acknowledgment — or a row was reclassified to Refusal and its "
                    + "line should go.");

                foreach ((string code, string why) in TwoFacedCauses)
                {
                    Assert.That(why, Has.Length.GreaterThan(80),
                        $"{code}'s line has to carry its reasoning, or the list becomes a mute allow-list");
                    Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry),
                        Is.True, code);
                    Assert.That(entry.Disposition, Is.Not.EqualTo(CatalogDisposition.Refusal),
                        $"{code} is listed as two-faced, so its entry must still be the REPORTING half; a Refusal "
                        + "disposition means the disagreement is gone and the line should go with it");
                }
            });
        }

        /// <summary>
        /// The second drift gate over the same identities: what a row DECLARES it refuses must be what the
        /// registries actually raise under it — a bijection, checked both ways.
        /// <para>
        /// This is what stops <c>RefusedOperations</c> becoming a hand-maintained copy of §4's prose column, which
        /// is the exact failure mode the field exists to end. A registry gaining an identity for a cause whose
        /// entry does not name that operation fails here, and so does an entry claiming a refusal nothing raises
        /// — the second being precisely what §4 published for <c>root-version</c> for as long as the column was
        /// written by hand.
        /// </para>
        /// <para>
        /// The two directions are NOT symmetric, and the asymmetry is the point. Every raised refusal must be
        /// declared — no exceptions, or the declaration stops being a complete record. A declared refusal that
        /// nothing raises is allowed only on the named list below, which carries its reason, because a Refusal
        /// row can legitimately be declared before it is wired — or, for a ruled-out row, instead of ever being
        /// wired: the disposition already asserts that the operation cannot proceed, and this only names which.
        /// </para>
        /// <para>
        /// The acknowledgment is per <b>(code, operation)</b> rather than per code, because a row can refuse
        /// several heads and be wired for only some of them. Comparing codes alone would let a second, unraised
        /// head slip in under a line written about the first.
        /// </para>
        /// <para>
        /// <c>root-version</c> is on neither side and needs no line: it refuses nothing and declares nothing.
        /// </para>
        /// </summary>
        [Test]
        public void EveryRowDeclaresExactlyTheOperationsItsRegistriesRefuse()
        {
            ImmutableArray<(string Cause, string Operation)> raised =
            [
                .. Identities()
                    .Select(pair => (pair.Identity.Cause.Value, pair.Identity.Operation.Value))
                    .Distinct()
                    .OrderBy(pair => pair.Item1, StringComparer.Ordinal)
                    .ThenBy(pair => pair.Item2, StringComparer.Ordinal),
            ];

            ImmutableArray<(string Cause, string Operation)> declared =
            [
                .. ProblemCatalog.Current.Entries
                    .SelectMany(e => e.RefusedOperations.Select(op => (e.Code.Value, op.Value)))
                    .OrderBy(pair => pair.Item1, StringComparer.Ordinal)
                    .ThenBy(pair => pair.Item2, StringComparer.Ordinal),
            ];

            Assert.Multiple(() =>
            {
                Assert.That(raised, Is.Not.Empty, "the registries are the evidence; an empty read proves nothing");

                Assert.That(raised.Except(declared), Is.Empty,
                    "a registry raises a refusal its cause's entry does not declare. The declaration has to be "
                    + "the complete record of what a row refuses, so this direction admits no exception");

                Assert.That(declared.Except(raised),
                    Is.EquivalentTo(DeclaredAheadOfTheirRaiser.Select(row => (row.Code, row.Operation))),
                    "an entry claims a refusal no site raises. That is legal only for a Refusal row not yet "
                    + "wired, and only with its reason written down beside it");

                foreach ((string code, string operation, string why) in DeclaredAheadOfTheirRaiser)
                {
                    Assert.That(why, Has.Length.GreaterThan(80),
                        $"{code}'s line has to carry its reasoning, or the list becomes a mute allow-list");
                    Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry),
                        Is.True, code);
                    Assert.That(entry.Disposition, Is.EqualTo(CatalogDisposition.Refusal),
                        $"{code} declares a refusal nothing raises, which is only defensible while the row IS a "
                        + "refusal by disposition; a reporting row must not claim one");
                    Assert.That(entry.RefusedOperations.Select(op => op.Value), Does.Contain(operation),
                        $"{code}'s line names {operation}, which its entry no longer declares");
                }
            });
        }

        /// <summary>
        /// The (row, head) pairs whose refusal is declared but not raised anywhere — each with the reason it is
        /// legal.
        /// </summary>
        private static readonly (string Code, string Operation, string Why)[] DeclaredAheadOfTheirRaiser =
        [
            ("import-catalog-wrong-kind", "import.catalog",
                "A Refusal row with no raise site yet: reading a definition file as the wrong catalog kind still "
                + "succeeds today, a gap recorded by CatalogCompletenessTests.KnownUnimplemented and executed by "
                + "ImportBridgeRefusalTests.ReadingAFileAsTheWrongCatalogKindStillSucceedsToday. The disposition "
                + "already asserts the import cannot proceed; naming import.catalog only spells which operation "
                + "that is, and asserts no raiser. Its Danish template stays empty for the opposite reason — "
                + "words are read by a user, and this condition never reaches one."),
            ("load-truncated", "io.load",
                "A RuledOut Refusal row, so it is declared INSTEAD of ever being raised rather than ahead of it: "
                + "an XML parser refuses a truncated document as load-not-xml before truncation can be named, "
                + "which is the finding §6 records. The head is a property of the condition — a truncated file "
                + "stops an open and nothing else — and every Refusal row names the operation it stops, so "
                + "leaving it empty would read as 'not a refusal'. Executed by "
                + "ProblemCatalogTests.TheInvestigatedRowsAreKeptAsRuledOutEntries."),
        ];

        /// <summary>
        /// The drift gate itself: cause label against cause template, operation label against operation template,
        /// for every identity in the SDK.
        /// </summary>
        [Test]
        public void EveryRefusalLabelIsItsEntrysTemplate()
        {
            Assert.Multiple(() =>
            {
                foreach ((string member, RefusalIdentity identity) in Identities())
                {
                    AssertAgrees(member, identity.Cause, identity.CauseLabel, "cause");
                    AssertAgrees(member, identity.Operation, identity.OperationLabel, "operation");
                }
            });
        }

        /// <summary>
        /// The armed control: the gate must FAIL a drifted spelling. It takes a real identity, drifts its Danish
        /// sentence by one character, and requires <see cref="Agrees"/> — the SAME predicate
        /// <see cref="EveryRefusalLabelIsItsEntrysTemplate"/> asserts through — to come out false for it.
        ///
        /// <para>Running the real predicate is the point. An earlier version of this control selected an entry
        /// with the predicate <c>e.MessageTemplate != identity.CauseLabel</c> and then asserted that those two
        /// were unequal, which is a tautology: it could only fail by <c>First</c> throwing, so it proved the
        /// registry was non-empty and nothing about the gate.</para>
        /// </summary>
        [Test]
        public void TheGateIsArmed()
        {
            (string member, RefusalIdentity identity) = Identities()
                .First(pair => pair.Identity.CauseLabel.Length > 0
                    && ProblemCatalog.Current.TryGet(pair.Identity.Cause, out ProblemCatalogEntry e)
                    && e.MessageTemplate.Length > 0);

            RefusalIdentity drifted = identity with { CauseLabel = identity.CauseLabel + " x" };

            Assert.Multiple(() =>
            {
                Assert.That(Agrees(identity.Cause, identity.CauseLabel), Is.True,
                    $"{member}: the undrifted identity must agree, or the control is measuring the wrong thing");
                Assert.That(Agrees(drifted.Cause, drifted.CauseLabel), Is.False,
                    $"{member}: a one-character drift must fail the same comparison the gate makes");
            });
        }

        private static void AssertAgrees(string member, ProblemCode code, string label, string half)
        {
            Assert.That(Agrees(code, label), Is.True,
                $"{member}: the {half} label and its catalogue template must be the same words — the raising site "
                + "cannot read the catalogue, so nothing but this test keeps them in step");
        }

        /// <summary>
        /// The gate's one comparison, as a predicate so the armed control can NEGATE it rather than restate it.
        /// </summary>
        /// <param name="code">The code whose entry governs the sentence.</param>
        /// <param name="label">The sentence the raising site carries.</param>
        private static bool Agrees(ProblemCode code, string label)
        {
            if (!ProblemCatalog.Current.TryGet(code, out ProblemCatalogEntry entry))
            {
                return true;   // the governance half is the other test's assertion
            }

            // An entry with no template of its own has nothing to drift from: the operation heads that carry only
            // a diagnostic are governed, not rendered.
            return entry.MessageTemplate.Length == 0
                || string.Equals(label, entry.MessageTemplate, StringComparison.Ordinal);
        }
    }
}
