using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Seeded;
using Type = System.Type;

namespace Ihc.Tests
{
    /// <summary>
    /// The value-collection convention: a <b>stored ordered sequence</b> on a public <c>Ihc.Vis</c> record is an
    /// <see cref="EquatableArray{T}"/>, never a raw <c>ImmutableArray&lt;&gt;</c>, array or
    /// <see cref="IReadOnlyList{T}"/>.
    /// <para>Why this needs a test rather than a review habit: those raw types compare by <i>reference</i>, so a
    /// record holding one silently loses structural equality. The symptom is invisible at the declaration — the
    /// code compiles, the record looks like a value type, and equality quietly stops working. The historical fix
    /// was a handwritten <c>Equals</c>/<c>GetHashCode</c> pair listing every member, which then had to be updated
    /// by hand for every member added afterwards; the omission of <c>DialogDescriptorField.ColumnSpan</c> from
    /// exactly such a list is what prompted this convention.</para>
    /// <para>This is a <b>test-time reflection check</b>, not a build-time analyzer. It inspects declared instance
    /// backing fields, so a computed collection property has nothing to flag, and set/map members are out of
    /// scope — an ordered wrapper is the wrong semantics for them.</para>
    /// </summary>
    public class ValueCollectionArchitectureTests
    {
        /// <summary>Read off a public anchor type rather than typed as a literal, so a namespace rename is
        /// followed automatically instead of silently matching nothing.</summary>
        private static readonly string VisRoot = typeof(ProjectAppService).Namespace!;

        /// <summary>The ordered-sequence shapes that do NOT carry value equality. Sets, maps and
        /// <see cref="EquatableArray{T}"/>/<see cref="EquatableSet{T}"/> are deliberately absent.</summary>
        private static readonly Type[] RawOrderedSequences =
        [
            typeof(ImmutableArray<>),
            typeof(ImmutableList<>),
            typeof(IReadOnlyList<>),
            typeof(IList<>),
            typeof(List<>),
        ];

        private static bool IsRawOrderedSequence(Type type) =>
            type.IsArray
            || (type.IsGenericType && RawOrderedSequences.Contains(type.GetGenericTypeDefinition()));

        /// <summary>A record is identified by the clone method the compiler synthesizes for <c>with</c>; there is
        /// no <c>IsRecord</c> in reflection.</summary>
        private static bool IsRecord(Type type) =>
            type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is not null;

        /// <summary>
        /// The rule itself, as a reusable query so the production scan and the seeded controls exercise the exact
        /// same predicate — a control that tested a different code path would prove nothing about the real scan.
        /// Instance fields only, which is what excludes static lookup tables, parameters and locals for free.
        /// </summary>
        private static IEnumerable<string> RawOrderedSequenceFields(IEnumerable<Type> types) =>
            types
                .Where(IsRecord)
                .SelectMany(t => t
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(f => IsRawOrderedSequence(f.FieldType))
                    .Select(f => $"{t.FullName}.{f.Name} : {f.FieldType.Name}"));

        private static IEnumerable<Type> PublicVisRecords() =>
            typeof(ProjectAppService).Assembly.GetTypes()
                .Where(t => t.IsVisible)   // true only when the type AND every enclosing type are public
                .Where(t => t.Namespace is { } ns
                            && (ns == VisRoot || ns.StartsWith(VisRoot + ".", StringComparison.Ordinal)));

        /// <summary>
        /// The rule. The exemption list is empty by design: no public <c>Ihc.Vis</c> record needs one, so a
        /// failure here is a finding to explain rather than a list to append to.
        /// </summary>
        [Test]
        public void PublicVisRecords_StoreOrderedSequencesAsEquatableArray()
        {
            string[] violations = [.. RawOrderedSequenceFields(PublicVisRecords()).Order(StringComparer.Ordinal)];

            Assert.That(violations, Is.Empty,
                "a stored ordered sequence on a public Ihc.Vis record must be EquatableArray<T>, or the record " +
                "silently compares that member by reference");
        }

        /// <summary>
        /// Proves the scan actually looks at something. A namespace/visibility/record predicate that matched
        /// nothing would make the rule above pass vacuously forever — the exact failure mode the SDK's other
        /// architecture rules guard against with anchored namespaces.
        /// </summary>
        [Test]
        public void PublicVisRecordScan_IsArmed()
        {
            Type[] scanned = [.. PublicVisRecords().Where(IsRecord)];

            Assert.Multiple(() =>
            {
                Assert.That(scanned, Is.Not.Empty, "the scan must find public Ihc.Vis records at all");
                Assert.That(scanned, Has.Length.GreaterThan(20), "it must reach the whole public value model");
                // Types the migration actually touched, so the scan is proven to cover the real subjects.
                Assert.That(scanned, Does.Contain(typeof(ProjectElement)));
                Assert.That(scanned, Does.Contain(typeof(Ihc.Vis.Products.DialogDescriptorField)));
                Assert.That(scanned, Does.Contain(typeof(Ihc.Vis.Validation.ProjectValidationResult)));
            });
        }

        /// <summary>
        /// The seeded positive control: the same predicate must flag a record that stores a raw ordered sequence,
        /// in each of the three banned shapes. Without this, "no violations" could mean "the detector is broken".
        /// </summary>
        [Test]
        public void TheDetector_FlagsASeededRawSequenceRecord()
        {
            string[] flagged =
            [
                .. RawOrderedSequenceFields([
                    typeof(SeededRawImmutableArrayRecord),
                    typeof(SeededRawArrayRecord),
                    typeof(SeededRawReadOnlyListRecord),
                ])
            ];

            Assert.Multiple(() =>
            {
                Assert.That(flagged, Has.Length.EqualTo(3), "every banned shape must be detected");
                Assert.That(flagged, Has.Some.Contains(nameof(SeededRawImmutableArrayRecord)));
                Assert.That(flagged, Has.Some.Contains(nameof(SeededRawArrayRecord)));
                Assert.That(flagged, Has.Some.Contains(nameof(SeededRawReadOnlyListRecord)));
            });
        }

        /// <summary>
        /// The seeded negative controls: the rule must stay silent on the migrated form, on set and map members
        /// (an ordered wrapper is the wrong semantics for both), and on a computed collection view — which has no
        /// backing field, and is the shape <c>ProjectValidationResult.Warnings</c> and
        /// <c>FunctionBlockDefinition.Inputs</c> use in production. An over-broad rule would fail here.
        /// </summary>
        [Test]
        public void TheDetector_IgnoresWrappedSetMapAndComputedMembers()
        {
            string[] flagged =
            [
                .. RawOrderedSequenceFields([
                    typeof(SeededWrappedRecord),
                    typeof(SeededSetRecord),
                    typeof(SeededMapRecord),
                    typeof(SeededComputedViewRecord),
                ])
            ];

            Assert.That(flagged, Is.Empty,
                "EquatableArray, sets, maps, computed views, static lookups, parameters and locals are all out of scope");
        }

        /// <summary>
        /// The two deliberate survivors, pinned so a later cleanup does not mistake them for leftover duplication.
        /// <para><c>DefinitionDocumentation</c> is MAP equality and <c>Project</c> deliberately excludes
        /// serialization provenance from its logical equality; neither is an ordered sequence, so neither is
        /// something the wrapper could have replaced.</para>
        /// </summary>
        [Test]
        public void TheDeliberateCustomEqualityImplementations_Survive()
        {
            MethodInfo? documentation = typeof(DefinitionDocumentation)
                .GetMethod(nameof(object.GetHashCode), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            MethodInfo? project = typeof(Ihc.Vis.Projects.Project)
                .GetMethod(nameof(object.GetHashCode), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

            Assert.Multiple(() =>
            {
                Assert.That(documentation, Is.Not.Null, "DefinitionDocumentation keeps map equality by design");
                Assert.That(project, Is.Not.Null, "Project deliberately omits serialization provenance");
            });
        }
    }
}
