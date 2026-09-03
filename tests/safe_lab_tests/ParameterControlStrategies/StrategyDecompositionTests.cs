using System.Collections.Generic;
using NUnit.Framework;
using Ihc;
using IhcLab.ParameterControls;
using IhcLab.ParameterControls.Strategies;

namespace Ihc.Tests
{
    /// <summary>
    /// US-D: the operation filter derives renderability from the registry recursively. These tests cover the
    /// mechanism that replaced the hand-coded per-type gates: each strategy declares the sub-fields it renders
    /// via the registry (containers return their constituents; leaves return none), and the registry exposes a
    /// non-throwing TryGetStrategy lookup.
    /// </summary>
    [TestFixture]
    public class StrategyDecompositionTests
    {
        [Test]
        public void ComplexType_GetRenderedSubFields_ReturnsProperties()
        {
            var props = new[]
            {
                new FieldMetaData("a", typeof(int), [], ""),
                new FieldMetaData("b", typeof(string), [], "")
            };
            var field = new FieldMetaData("complex", typeof(object), props, "");

            Assert.That(new ComplexTypeParameterStrategy().GetRenderedSubFields(field), Is.EqualTo(props));
        }

        [Test]
        public void Array_GetRenderedSubFields_ReturnsElement()
        {
            var element = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("values", typeof(int[]), [element], "");

            Assert.That(new ArrayParameterStrategy().GetRenderedSubFields(field), Is.EqualTo(new[] { element }));
        }

        [Test]
        public void NumericLeaf_GetRenderedSubFields_ReturnsEmpty()
        {
            var field = new FieldMetaData("n", typeof(int), [], "");
            Assert.That(new NumericParameterStrategy().GetRenderedSubFields(field), Is.Empty);
        }

        [Test]
        public void FileLeaf_GetRenderedSubFields_ReturnsEmpty()
        {
            // A file is rendered wholesale - the filter must not recurse into its byte[] Data.
            var dataSub = new FieldMetaData("Data", typeof(byte[]), [], "");
            var field = new FieldMetaData("project", typeof(SceneProject), [dataSub], "");
            Assert.That(new FileParameterStrategy().GetRenderedSubFields(field), Is.Empty);
        }

        [Test]
        public void ResourceValueLeaf_GetRenderedSubFields_ReturnsEmpty()
        {
            // The ResourceValue editor renders wholesale - the filter must not recurse into its UnionValue.
            var field = new FieldMetaData("rv", typeof(ResourceValue), [], "");
            Assert.That(new ResourceValueParameterStrategy().GetRenderedSubFields(field), Is.Empty);
        }

        [Test]
        public void Registry_TryGetStrategy_KnownType_ReturnsStrategy()
        {
            var field = new FieldMetaData("n", typeof(int), [], "");
            Assert.That(ParameterControlRegistry.Instance.TryGetStrategy(field), Is.InstanceOf<NumericParameterStrategy>());
        }

        [Test]
        public void Registry_TryGetStrategy_UnrenderableField_ReturnsNull()
        {
            // A complex record with no sub-types (the one-level metadata limit) has no strategy.
            var field = new FieldMetaData("inner", typeof(NetworkSettings), [], "");
            Assert.That(ParameterControlRegistry.Instance.TryGetStrategy(field), Is.Null);
        }
    }
}
