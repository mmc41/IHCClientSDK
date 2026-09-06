using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        /// <summary>The element model's own namespace, anchored the same way <see cref="VisRoot"/> is.</summary>
        private static readonly string ModelNamespace = typeof(ProjectElement).Namespace!;

        /// <summary>
        /// The element model is IMMUTABLE, and this is where that stops being an assumption.
        ///
        /// <para>Two mechanisms rest their correctness on it. <c>Project</c> memoizes its lookup index against the
        /// ROOT REFERENCE — a given <see cref="ProjectElement"/> reference's subtree can never change, so a memo
        /// that still matches the root is still right — and <c>ProjectChangeSet.Diff</c> skips a reference-equal
        /// pair outright, which decides that nothing below it changed. One settable member anywhere in the model
        /// makes both of those answer a stale question with confidence, and neither would fail loudly.</para>
        ///
        /// <para>Init-only counts as immutable: an <c>init</c> setter runs during construction (including
        /// <c>with</c>, which mints a new instance) and is closed afterwards, which is exactly the guarantee the
        /// reference key needs. So the check is "no writable instance field, no non-init setter", not "no setter".
        /// Static state is out of scope — it is not part of an instance's identity.</para>
        /// </summary>
        [Test]
        public void ModelRecords_ExposeNoSettableState()
        {
            Type[] scanned = [.. ModelRecords()];
            string[] settable = [.. scanned.SelectMany(SettableInstanceMembers).Order(StringComparer.Ordinal)];

            Assert.Multiple(() =>
            {
                Assert.That(scanned, Is.Not.Empty, "the scan must find records in the element model at all");
                Assert.That(scanned, Does.Contain(typeof(ProjectElement)),
                    "and reach the type the reference-keyed memo and the diff's reference skip are about");
                Assert.That(settable, Is.Empty,
                    "a settable member on an element-model record would let a subtree change under a reference "
                    + "that a memo or a diff already decided was unchanged");
            });
        }

        /// <summary>
        /// The seeded controls for the rule above: an empty roster and a broken scan look identical, and the two
        /// non-vacuity assertions there only prove the SCAN reaches the model — not that a settable member in it
        /// would be reported. These prove the detector itself, on both shapes it must catch and on the immutable
        /// forms the model is actually built from.
        /// </summary>
        [Test]
        public void TheDetector_FlagsASeededMutableRecordAndNotAnImmutableOne()
        {
            string[] flagged = [.. SettableInstanceMembers(typeof(SeededMutableRecord)).Order(StringComparer.Ordinal)];

            Assert.Multiple(() =>
            {
                Assert.That(flagged, Has.Some.Contains($"{nameof(SeededMutableRecord.Label)} : settable property"));
                Assert.That(flagged, Has.Some.Contains($"{nameof(SeededMutableRecord.Counter)} : writable field"));
                Assert.That(SettableInstanceMembers(typeof(SeededImmutableRecord)), Is.Empty,
                    "positional, init-only, computed and static members are all closed after construction");
            });
        }

        private static IEnumerable<Type> ModelRecords() =>
            typeof(ProjectAppService).Assembly.GetTypes()
                .Where(t => t.Namespace == ModelNamespace)
                .Where(IsRecord);

        /// <summary>Writable instance fields and non-init property setters — the two ways an instance can change
        /// after construction. A record's auto-property backing field is <c>initonly</c>, so a <c>{ get; set; }</c>
        /// is caught by both halves; both are reported because a hand-written setter over other storage is not.</summary>
        private static IEnumerable<string> SettableInstanceMembers(Type type)
        {
            const BindingFlags Declared =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            foreach (FieldInfo field in type.GetFields(Declared).Where(f => !f.IsInitOnly))
            {
                yield return $"{type.FullName}.{field.Name} : writable field";
            }
            foreach (PropertyInfo property in type.GetProperties(Declared))
            {
                if (property.SetMethod is { } setter && !IsInitOnly(setter))
                {
                    yield return $"{type.FullName}.{property.Name} : settable property";
                }
            }
        }

        /// <summary>An <c>init</c> accessor is a plain setter carrying an <see cref="IsExternalInit"/> modreq on its
        /// return type — the only thing that distinguishes the two in metadata.</summary>
        private static bool IsInitOnly(MethodInfo setter) =>
            setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

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
