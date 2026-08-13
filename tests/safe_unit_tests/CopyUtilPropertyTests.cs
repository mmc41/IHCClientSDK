using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CsCheck;
using KellermanSoftware.CompareNetObjects;
using Ihc;

namespace Ihc.Tests
{
    /// <summary>
    /// Property-based tests for <see cref="CopyUtil.DeepCopyAndApply"/>, using CsCheck.
    ///
    /// The example-based <see cref="CopyUtilTests"/> exercises fixed shapes; this generalizes the two
    /// core laws over randomly generated nested record graphs (records + lists + dictionaries):
    /// under the identity transformer the copy is structurally equal to the source, and it shares no
    /// mutable references with it. The deep-copy engine underpins AdminAppService snapshotting.
    /// </summary>
    [TestFixture]
    public class CopyUtilPropertyTests
    {
        // Identity transformer: return each (deep-copied) value unchanged.
        private static readonly Func<PropertyInfo, object, object> Identity = (prop, value) => value;

        public record CopyLeaf
        {
            public int Id { get; init; }
            public string? Name { get; init; }
        }

        public record CopyGraph
        {
            public int Level { get; init; }
            public CopyLeaf Child { get; init; } = new CopyLeaf();
            public List<int> Numbers { get; init; } = new List<int>();
            public Dictionary<string, int> Scores { get; init; } = new Dictionary<string, int>();
        }

        private const string Alphabet =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        private static readonly Gen<string> ShortText =
            Gen.OneOfConst(Alphabet.ToCharArray()).Array[0, 12].Select(cs => new string(cs));

        private static readonly Gen<CopyLeaf> GenLeaf =
            from id in Gen.Int
            from name in ShortText
            select new CopyLeaf { Id = id, Name = name };

        private static readonly Gen<List<int>> GenIntList =
            Gen.Int.Array[0, 8].Select(a => new List<int>(a));

        private static readonly Gen<Dictionary<string, int>> GenScores =
            (from k in ShortText from v in Gen.Int select KeyValuePair.Create(k, v))
            .Array[0, 6]
            .Select(pairs =>
            {
                var d = new Dictionary<string, int>();
                foreach (var p in pairs) d[p.Key] = p.Value;
                return d;
            });

        private static readonly Gen<CopyGraph> GenGraph =
            from level in Gen.Int
            from child in GenLeaf
            from numbers in GenIntList
            from scores in GenScores
            select new CopyGraph { Level = level, Child = child, Numbers = numbers, Scores = scores };

        // A record whose members are CONCRETE collection types.
        public record CopyCollections
        {
            public List<string>? Names { get; init; }
            public HashSet<int>? UniqueIds { get; init; }
            public Dictionary<string, int>? Scores { get; init; }
        }

        // A record whose members are declared as INTERFACES. This is a genuinely different axis: the
        // copier picks the target container from the declared property type, so IList/ISet/IDictionary
        // members exercise code that concrete-typed members never reach.
        public record CopyInterfaceCollections
        {
            public IList<int>? Numbers { get; init; }
            public ISet<string>? Tags { get; init; }
            public IDictionary<string, string>? Metadata { get; init; }
        }

        private static readonly Gen<CopyCollections> GenCollections =
            from names in ShortText.Array[0, 5]
            from ids in Gen.Int.Array[0, 5]
            from scores in GenScores
            select new CopyCollections
            {
                Names = new List<string>(names),
                UniqueIds = new HashSet<int>(ids),
                Scores = scores,
            };

        private static readonly Gen<CopyInterfaceCollections> GenInterfaceCollections =
            from numbers in Gen.Int.Array[0, 5]
            from tags in ShortText.Array[0, 5]
            from keys in ShortText.Array[0, 4]
            select new CopyInterfaceCollections
            {
                Numbers = new List<int>(numbers),
                Tags = new HashSet<string>(tags),
                Metadata = keys.Distinct().ToDictionary(k => k, k => k + "-value"),
            };

        // NOTE: a LAZY sequence (a yield-return iterator) is deliberately NOT generated here.
        // DeepCopyAndApply cannot copy one at all - it tries to clone the compiler-generated iterator
        // class and throws "no parameterless constructor and no constructor matching all properties".
        // That is a real limitation of the copier, found by generalizing this property; it is recorded
        // in the backlog rather than worked around silently, and fixing it is out of this task's scope.
        // The deleted IEnumerableOfRecords example never reached it either: it assigned a List to an
        // IEnumerable local, and DeepCopyAndApply takes object, so only the RUNTIME type ever mattered.

        /// <summary>
        /// Every container shape, paired with the concrete type the copy is expected to materialize as.
        /// This is the axis the example tests varied and the original generator did not: the old
        /// <c>GenGraph</c> ranged over VALUES inside one fixed shape, so it subsumed none of them.
        /// </summary>
        private static readonly Gen<(object Original, Type ExpectedCopyType)> GenAnyShape =
            Gen.OneOf(
                GenLeaf.Select(v => ((object)v, typeof(CopyLeaf))),
                GenGraph.Select(v => ((object)v, typeof(CopyGraph))),
                GenCollections.Select(v => ((object)v, typeof(CopyCollections))),
                GenInterfaceCollections.Select(v => ((object)v, typeof(CopyInterfaceCollections))),
                GenLeaf.Array[0, 4].Select(v => ((object)v, typeof(CopyLeaf[]))),
                GenLeaf.Array[0, 4].Select(v => ((object)new List<CopyLeaf>(v), typeof(List<CopyLeaf>))),
                ShortText.Array[0, 5].Select(v => ((object)new HashSet<string>(v), typeof(HashSet<string>))));

        /// <summary>
        /// Law: a deep copy under the identity transformer is structurally equal to the source, and
        /// materializes as the expected concrete container type (an interface-typed or lazy source is
        /// normalized to a concrete collection).
        /// </summary>
        [Test]
        public void DeepCopyAndApply_Identity_IsStructurallyEqual()
        {
            GenAnyShape.Sample(testCase =>
            {
                object copy = CopyUtil.DeepCopyAndApply(testCase.Original, Identity);

                Assert.That(copy, Is.InstanceOf(testCase.ExpectedCopyType),
                    "copy materializes as " + testCase.ExpectedCopyType.Name);
                Assert.That(new CompareLogic().Compare(copy, testCase.Original).AreEqual,
                    Is.True, "structurally equal to the source");
            });
        }

        /// <summary>
        /// Law: the copy shares no mutable reference with the source anywhere in the graph. Stated by
        /// walking both graphs and requiring the two reference sets to be disjoint, rather than by
        /// naming members one by one - so it holds for every shape above, including ones nobody wrote
        /// an example for.
        /// </summary>
        [Test]
        public void DeepCopyAndApply_Identity_SharesNoMutableReferences()
        {
            GenAnyShape.Sample(testCase =>
            {
                object source = testCase.Original;
                object copy = CopyUtil.DeepCopyAndApply(source, Identity);

                var inSource = new HashSet<object>(ReferenceEqualityComparer.Instance);
                var inCopy = new HashSet<object>(ReferenceEqualityComparer.Instance);
                CollectReferences(source, inSource);
                CollectReferences(copy, inCopy);

                inCopy.IntersectWith(inSource);
                Assert.That(inCopy, Is.Empty, "copy and source share no mutable reference");
            });
        }

        /// <summary>
        /// Collects every mutable reference reachable from <paramref name="node"/>.
        ///
        /// <para>Strings and value types are skipped: they are immutable (or copied by value), so
        /// sharing them is harmless and would produce false failures - interned string literals are
        /// shared by the runtime itself. Framework objects are not walked property-by-property either,
        /// because collections legitimately hold SHARED singletons such as
        /// <c>EqualityComparer&lt;T&gt;.Default</c>; their contents are walked structurally instead.</para>
        /// </summary>
        private static void CollectReferences(object? node, HashSet<object> into)
        {
            if (node == null || node is string || node.GetType().IsValueType)
                return;
            if (!into.Add(node))
                return;

            if (node is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    CollectReferences(entry.Key, into);
                    CollectReferences(entry.Value, into);
                }
                return;
            }

            if (node is IEnumerable sequence)
            {
                foreach (object? item in sequence)
                    CollectReferences(item, into);
                return;
            }

            // Only OUR record types are walked by property; anything else is a leaf for this purpose.
            if (node.GetType().Assembly == typeof(CopyUtilPropertyTests).Assembly)
            {
                foreach (var property in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (property.CanRead && property.GetIndexParameters().Length == 0)
                        CollectReferences(property.GetValue(node), into);
                }
            }
        }
    }
}
