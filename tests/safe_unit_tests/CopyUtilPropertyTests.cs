using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Law: a deep copy under the identity transformer is structurally equal to the source.
        /// </summary>
        [Test]
        public void DeepCopyAndApply_Identity_IsStructurallyEqual()
        {
            GenGraph.Sample(original =>
            {
                var copy = (CopyGraph)CopyUtil.DeepCopyAndApply(original, Identity);
                return new CompareLogic().Compare(copy, original).AreEqual;
            });
        }

        /// <summary>
        /// Law: the copy shares no mutable references with the source (mutating one cannot affect the
        /// other), for the root and every reference-typed member.
        /// </summary>
        [Test]
        public void DeepCopyAndApply_Identity_SharesNoMutableReferences()
        {
            GenGraph.Sample(original =>
            {
                var copy = (CopyGraph)CopyUtil.DeepCopyAndApply(original, Identity);
                return !ReferenceEquals(copy, original)
                    && !ReferenceEquals(copy.Child, original.Child)
                    && !ReferenceEquals(copy.Numbers, original.Numbers)
                    && !ReferenceEquals(copy.Scores, original.Scores);
            });
        }
    }
}
