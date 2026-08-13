using System.Collections.Immutable;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The <see cref="EquatableSet{T}"/> contract: order-<i>independent</i> structural equality, hash
    /// consistency across insertion orders, <c>default</c>-is-empty normalization, and snapshotting.
    /// </summary>
    /// <remarks>
    /// As in <see cref="EquatableArrayTests"/>, equality is asserted by calling <c>Equals</c>/<c>==</c>
    /// directly: NUnit's <c>Is.EqualTo</c> would compare two enumerables element-wise with its own comparer
    /// and never reach the equality under test.
    /// </remarks>
    public class EquatableSetTests
    {
        /// <summary>Stands in for <c>FunctionBlockDefinition</c>: scalars plus one set member.</summary>
        private sealed record Definition(string Name, EquatableSet<int> CloseIds);

        private static void AssertEqual<T>(EquatableSet<T> a, EquatableSet<T> b)
        {
            bool equals = a.Equals(b);
            bool symmetric = b.Equals(a);
            bool equalOperator = a == b;
            bool notEqualOperator = a != b;
            Assert.Multiple(() =>
            {
                Assert.That(equals, Is.True, "Equals");
                Assert.That(symmetric, Is.True, "Equals is symmetric");
                Assert.That(equalOperator, Is.True, "operator ==");
                Assert.That(notEqualOperator, Is.False, "operator !=");
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()), "equal values must hash equally");
            });
        }

        private static void AssertNotEqual<T>(EquatableSet<T> a, EquatableSet<T> b)
        {
            bool equals = a.Equals(b);
            bool equalOperator = a == b;
            Assert.Multiple(() =>
            {
                Assert.That(equals, Is.False, "Equals");
                Assert.That(equalOperator, Is.False, "operator ==");
            });
        }

        [Test]
        public void Default_Empty_And_EmptyCollectionExpression_AreTheSameValue()
        {
            EquatableSet<string> viaDefault = default;
            EquatableSet<string> viaEmpty = EquatableSet<string>.Empty;
            EquatableSet<string> viaExpression = [];
            EquatableSet<string> viaImmutableSet = ImmutableHashSet<string>.Empty;

            Assert.Multiple(() =>
            {
                AssertEqual(viaDefault, viaEmpty);
                AssertEqual(viaDefault, viaExpression);
                AssertEqual(viaDefault, viaImmutableSet);
            });
        }

        [Test]
        public void DifferentInsertionOrder_IsEqualAndHashesEqually()
        {
            // The defining difference from EquatableArray<T>: order carries no meaning, and because a set's
            // enumeration order is unspecified, an order-sensitive hash would break the equal-hash contract.
            EquatableSet<int> ascending = [1, 2, 3];
            EquatableSet<int> shuffled = [3, 1, 2];

            AssertEqual(ascending, shuffled);
        }

        [Test]
        public void RepeatedElements_CollapseToOneMember()
        {
            EquatableSet<string> withDuplicates = ["Stue", "Stue", "Entré"];
            EquatableSet<string> distinct = ["Entré", "Stue"];

            Assert.Multiple(() =>
            {
                Assert.That(withDuplicates.Count, Is.EqualTo(2));
                AssertEqual(withDuplicates, distinct);
            });
        }

        [Test]
        public void DifferentMembers_IsNotEqual()
        {
            EquatableSet<string> a = ["Stue", "Entré"];
            EquatableSet<string> b = ["Stue", "Kontor"];

            AssertNotEqual(a, b);
        }

        [Test]
        public void ProperSubset_IsNotEqual()
        {
            EquatableSet<int> subset = [1, 2];
            EquatableSet<int> superset = [1, 2, 3];

            Assert.Multiple(() =>
            {
                AssertNotEqual(subset, superset);
                AssertNotEqual(superset, subset);
            });
        }

        [Test]
        public void EmptyAndNonEmpty_AreNotEqual()
        {
            EquatableSet<int> empty = default;
            EquatableSet<int> one = [0];

            AssertNotEqual(empty, one);
        }

        [Test]
        public void RecordMembers_AreCoveredWithoutAHandwrittenMemberList()
        {
            // The shape that forced FunctionBlockDefinition's eleven-member handwritten list: one set member
            // among otherwise already-value-equal members.
            Definition baseline = new Definition("fb", [7, 9]);
            Definition sameSetOtherOrder = new Definition("fb", [9, 7]);
            Definition differentScalar = new Definition("other", [7, 9]);
            Definition differentSet = new Definition("fb", [7, 10]);

            Assert.Multiple(() =>
            {
                Assert.That(baseline, Is.EqualTo(sameSetOtherOrder), "set order must not affect record equality");
                Assert.That(baseline.GetHashCode(), Is.EqualTo(sameSetOtherOrder.GetHashCode()));
                Assert.That(baseline, Is.Not.EqualTo(differentScalar), "scalar member");
                Assert.That(baseline, Is.Not.EqualTo(differentSet), "set member");
            });
        }

        [Test]
        public void CreateRange_SnapshotsCallerOwnedList()
        {
            List<int> callerOwned = new List<int> { 7, 9 };
            EquatableSet<int> snapshot = EquatableSet.CreateRange(callerOwned);
            EquatableSet<int> expected = [7, 9];

            callerOwned.Add(11);
            callerOwned[0] = 99;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Count, Is.EqualTo(2), "later Add must not reach the snapshot");
                AssertEqual(snapshot, expected);
            });
        }

        [Test]
        public void ReadSurface_OnDefault_BehavesAsEmpty()
        {
            EquatableSet<string> value = default;
            int enumerated = 0;
            foreach (string _ in value)
            {
                enumerated++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(value.Count, Is.Zero, nameof(value.Count));
                Assert.That(value.IsEmpty, Is.True, nameof(value.IsEmpty));
                Assert.That(value.Contains("Stue"), Is.False, nameof(value.Contains));
                Assert.That(enumerated, Is.Zero, "foreach over default yields nothing");
            });
        }

        [Test]
        public void ReadSurface_OnPopulated_ReportsMembership()
        {
            EquatableSet<string> value = ["Stue", "Entré"];

            Assert.Multiple(() =>
            {
                Assert.That(value.Count, Is.EqualTo(2), nameof(value.Count));
                Assert.That(value.IsEmpty, Is.False, nameof(value.IsEmpty));
                Assert.That(value.Contains("Entré"), Is.True, "present member");
                Assert.That(value.Contains("Kontor"), Is.False, "absent member");
            });
        }

        [Test]
        public void ForeachBindsToAConcreteStructEnumerator_SoEnumerationDoesNotBoxOrAllocate()
        {
            System.Reflection.MethodInfo pattern = typeof(EquatableSet<int>)
                .GetMethod(nameof(EquatableSet<int>.GetEnumerator))!;

            Assert.Multiple(() =>
            {
                Assert.That(pattern.ReturnType, Is.EqualTo(typeof(ImmutableHashSet<int>.Enumerator)));
                Assert.That(pattern.ReturnType.IsValueType, Is.True, "a struct enumerator is not boxed");
            });
        }

        [Test]
        public void AsImmutableHashSet_NormalizesDefaultToEmpty()
        {
            ImmutableHashSet<string> fromDefault = default(EquatableSet<string>).AsImmutableHashSet();
            ImmutableHashSet<string> roundTripped =
                ((EquatableSet<string>)ImmutableHashSet.Create("Stue")).AsImmutableHashSet();

            Assert.Multiple(() =>
            {
                Assert.That(fromDefault, Is.Not.Null, "must hand back empty, never null");
                Assert.That(fromDefault.IsEmpty, Is.True);
                Assert.That(roundTripped.Contains("Stue"), Is.True, "content survives the round trip");
            });
        }
    }
}
