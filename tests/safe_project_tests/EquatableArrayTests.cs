using System.Collections.Immutable;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The <see cref="EquatableArray{T}"/> contract: ordered structural equality, hash consistency,
    /// <c>default</c>-is-empty normalization, snapshotting, and the read surface.
    /// </summary>
    /// <remarks>
    /// These tests deliberately call <c>Equals</c>/<c>==</c> directly rather than asserting
    /// <c>Is.EqualTo</c> between two <see cref="EquatableArray{T}"/> values. NUnit's equality constraint
    /// compares two <see cref="System.Collections.IEnumerable"/> operands element-by-element with its own
    /// comparer, so it would never invoke the equality under test — every assertion here would pass even if
    /// <c>Equals</c> were deleted. <see cref="Is.EqualTo"/> is used only on records that <i>contain</i> an
    /// <see cref="EquatableArray{T}"/> (a record is not enumerable, so NUnit calls its <c>Equals</c>, which is
    /// exactly the compiler-generated equality this type exists to enable).
    /// </remarks>
    public class EquatableArrayTests
    {
        /// <summary>A nested value record: its own equality must recurse through the wrapper into child records.</summary>
        private sealed record Node(string Name, EquatableArray<Node> Children);

        private static Node Leaf(string name) => new Node(name, []);

        /// <summary>The full equality contract for a pair expected to be equal.</summary>
        private static void AssertEqual<T>(EquatableArray<T> a, EquatableArray<T> b)
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

        private static void AssertNotEqual<T>(EquatableArray<T> a, EquatableArray<T> b)
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
            EquatableArray<string> viaDefault = default;
            EquatableArray<string> viaEmpty = EquatableArray<string>.Empty;
            EquatableArray<string> viaExpression = [];
            EquatableArray<string> viaImmutableArray = ImmutableArray<string>.Empty;

            Assert.Multiple(() =>
            {
                AssertEqual(viaDefault, viaEmpty);
                AssertEqual(viaDefault, viaExpression);
                AssertEqual(viaDefault, viaImmutableArray);
            });
        }

        [Test]
        public void IndependentlyBuilt_SameContent_AreEqual()
        {
            // Three different construction routes, three different backing arrays: a reference-equality
            // model reports all of these unequal.
            EquatableArray<string> viaExpression = ["Stue", "Entré"];
            EquatableArray<string> viaImmutableArray = ImmutableArray.Create("Stue", "Entré");
            EquatableArray<string> viaRange = EquatableArray.CreateRange(new List<string> { "Stue", "Entré" });

            Assert.Multiple(() =>
            {
                AssertEqual(viaExpression, viaImmutableArray);
                AssertEqual(viaExpression, viaRange);
            });
        }

        [Test]
        public void DifferentOrder_IsNotEqual()
        {
            EquatableArray<int> ascending = [1, 2, 3];
            EquatableArray<int> descending = [3, 2, 1];

            AssertNotEqual(ascending, descending);
        }

        [Test]
        public void DifferentElement_IsNotEqual()
        {
            EquatableArray<string> a = ["Stue", "Entré"];
            EquatableArray<string> b = ["Stue", "Kontor"];

            AssertNotEqual(a, b);
        }

        [Test]
        public void PrefixOfLongerSequence_IsNotEqual()
        {
            EquatableArray<int> prefix = [1, 2];
            EquatableArray<int> longer = [1, 2, 3];

            Assert.Multiple(() =>
            {
                AssertNotEqual(prefix, longer);
                AssertNotEqual(longer, prefix);
            });
        }

        [Test]
        public void EmptyAndNonEmpty_AreNotEqual()
        {
            EquatableArray<int> empty = default;
            EquatableArray<int> one = [0];

            AssertNotEqual(empty, one);
        }

        [Test]
        public void NestedRecordElements_RecurseThroughValueEquality()
        {
            Node a = new Node("root", [Leaf("Stue"), new Node("Entré", [Leaf("Loft")])]);
            Node b = new Node("root", [Leaf("Stue"), new Node("Entré", [Leaf("Loft")])]);

            // Is.EqualTo is correct here: Node is a record, not an enumerable, so NUnit calls its
            // compiler-generated Equals — the very thing EquatableArray<T> exists to make work.
            Assert.Multiple(() =>
            {
                Assert.That(a, Is.EqualTo(b));
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            });
        }

        [Test]
        public void NestedRecordElements_DifferingDeepInTheTree_AreNotEqual()
        {
            Node a = new Node("root", [new Node("Entré", [Leaf("Loft")])]);
            Node b = new Node("root", [new Node("Entré", [Leaf("Kælder")])]);

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void RecordMembers_AreCoveredWithoutAHandwrittenMemberList()
        {
            Node baseline = new Node("root", [Leaf("Stue")]);
            Node differentScalar = new Node("other", [Leaf("Stue")]);
            Node differentSequence = new Node("root", [Leaf("Kontor")]);

            // Neither difference is spelled out in any Equals implementation; the compiler covers both.
            Assert.Multiple(() =>
            {
                Assert.That(baseline, Is.Not.EqualTo(differentScalar), "scalar member");
                Assert.That(baseline, Is.Not.EqualTo(differentSequence), "sequence member");
            });
        }

        [Test]
        public void CreateRange_SnapshotsCallerOwnedList()
        {
            List<string> callerOwned = new List<string> { "Stue", "Entré" };
            EquatableArray<string> snapshot = EquatableArray.CreateRange(callerOwned);
            EquatableArray<string> expected = ["Stue", "Entré"];

            callerOwned.Add("Kontor");
            callerOwned[0] = "mutated";

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Count, Is.EqualTo(2), "later Add must not reach the snapshot");
                AssertEqual(snapshot, expected);
            });
        }

        [Test]
        public void ReadSurface_OnDefault_BehavesAsEmpty()
        {
            EquatableArray<string> value = default;
            int enumerated = 0;
            foreach (string _ in value)
            {
                enumerated++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(value.Count, Is.Zero, nameof(value.Count));
                Assert.That(value.Length, Is.Zero, nameof(value.Length));
                Assert.That(value.IsEmpty, Is.True, nameof(value.IsEmpty));
                Assert.That(value.Contains("Stue"), Is.False, nameof(value.Contains));
                Assert.That(value.IndexOf("Stue"), Is.EqualTo(-1), nameof(value.IndexOf));
                Assert.That(enumerated, Is.Zero, "foreach over default yields nothing");
            });
        }

        [Test]
        public void ReadSurface_OnPopulated_ReportsContentInOrder()
        {
            EquatableArray<string> value = ["Stue", "Entré", "Kontor"];
            List<string> enumerated = new List<string>();
            foreach (string item in value)
            {
                enumerated.Add(item);
            }

            Assert.Multiple(() =>
            {
                Assert.That(value.Count, Is.EqualTo(3), nameof(value.Count));
                Assert.That(value.Length, Is.EqualTo(3), nameof(value.Length));
                Assert.That(value.IsEmpty, Is.False, nameof(value.IsEmpty));
                Assert.That(value[1], Is.EqualTo("Entré"), "indexer");
                Assert.That(value.Contains("Kontor"), Is.True, nameof(value.Contains));
                Assert.That(value.IndexOf("Kontor"), Is.EqualTo(2), nameof(value.IndexOf));
                Assert.That(value.IndexOf("Loft"), Is.EqualTo(-1), "IndexOf of an absent item");
                Assert.That(enumerated, Is.EqualTo(new[] { "Stue", "Entré", "Kontor" }), "foreach order");
            });
        }

        [Test]
        public void ForeachBindsToAConcreteStructEnumerator_SoEnumerationDoesNotBoxOrAllocate()
        {
            // foreach prefers the public pattern method over IEnumerable<T>. Asserting the declared return
            // type is the stable way to pin "no interface boxing" without a brittle allocation threshold.
            System.Reflection.MethodInfo pattern = typeof(EquatableArray<int>)
                .GetMethod(nameof(EquatableArray<int>.GetEnumerator))!;

            Assert.Multiple(() =>
            {
                Assert.That(pattern.ReturnType, Is.EqualTo(typeof(ImmutableArray<int>.Enumerator)));
                Assert.That(pattern.ReturnType.IsValueType, Is.True, "a struct enumerator is not boxed");
            });
        }

        [Test]
        public void AsImmutableArray_NormalizesDefaultToEmpty()
        {
            ImmutableArray<string> fromDefault = default(EquatableArray<string>).AsImmutableArray();
            ImmutableArray<string> roundTripped = ((EquatableArray<string>)ImmutableArray.Create("Stue")).AsImmutableArray();

            Assert.Multiple(() =>
            {
                Assert.That(fromDefault.IsDefault, Is.False, "must hand back empty, never a default array");
                Assert.That(fromDefault.IsEmpty, Is.True);
                Assert.That(roundTripped, Is.EqualTo(new[] { "Stue" }), "content survives the round trip");
            });
        }
    }
}
