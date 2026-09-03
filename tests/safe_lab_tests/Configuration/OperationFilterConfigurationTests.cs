using System;
using System.Collections.Generic;
using Ihc;
using Ihc.App;

namespace Ihc.Tests
{
    /// <summary>
    /// Equivalence-partition tests for <see cref="OperationFilterConfiguration.ContainsUnsupportedType"/>.
    /// Documents the deliberate, deferred contract: operations whose parameters contain arrays, ResourceValue,
    /// or non-array generic collections (directly or nested) are excluded from the GUI, while scalar and
    /// complex-of-scalar parameters are allowed.
    /// </summary>
    [TestFixture]
    public class OperationFilterConfigurationTests
    {
        [Test]
        public void ContainsUnsupportedType_ArrayOfSupportedElement_ReturnsFalse()
        {
            // US-A2: arrays are now supported (rendered as a dynamic list); an array of a supported element is allowed.
            var elementSubField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("values", typeof(int[]), [elementSubField], "An int array field");
            Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.False);
        }

        [Test]
        public void ContainsUnsupportedType_ResourceValueField_ReturnsFalse()
        {
            // US-A3: ResourceValue is now rendered by the union editor (a supported leaf), so it is not filtered.
            var field = new FieldMetaData("resource", typeof(ResourceValue), [], "A ResourceValue field");
            Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.False);
        }

        [Test]
        public void ContainsUnsupportedType_NestedArrayOfSupportedElementInComplexType_ReturnsFalse()
        {
            // US-A2: a complex field whose sub-field is an array of a supported element is now allowed.
            var elementSubField = new FieldMetaData("", typeof(int), [], "");
            var arraySubField = new FieldMetaData("tags", typeof(int[]), [elementSubField], "");
            var field = new FieldMetaData("complex", typeof(object), [arraySubField], "Complex with a supported array sub-field");
            Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.False);
        }

        [Test]
        public void ContainsUnsupportedType_ScalarField_ReturnsFalse()
        {
            var field = new FieldMetaData("count", typeof(int), [], "A scalar field");
            Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.False);
        }

        [Test]
        public void ContainsUnsupportedType_ComplexOfScalars_ReturnsFalse()
        {
            // A complex field whose sub-fields are all scalars is supported.
            var subFields = new[]
            {
                new FieldMetaData("name", typeof(string), [], ""),
                new FieldMetaData("port", typeof(int), [], "")
            };
            var field = new FieldMetaData("settings", typeof(object), subFields, "A complex field of scalars");
            Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.False);
        }

        [Test]
        public void ContainsUnsupportedType_NestedComplexWithNoSubTypes_ReturnsTrue()
        {
            // US-A7: a complex parameter whose sub-field is itself a complex type that the one-level metadata
            // expansion left with no sub-types cannot be rendered, so the operation must be filtered (rather
            // than crash at selection with "No strategy found"). NetworkSettings is a complex record with no leaf
            // control strategy; here it appears as a sub-field carrying no sub-types (the one-level limit).
            var nestedComplexSubField = new FieldMetaData("inner", typeof(NetworkSettings), [], "");
            var field = new FieldMetaData("outer", typeof(object), [nestedComplexSubField], "Outer with a nested complex property");
            Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.True);
        }

        [Test]
        public void ContainsUnsupportedType_UnrenderableLeafField_ReturnsTrue()
        {
            // A standalone field whose type has no control strategy and no sub-types (e.g. a bare complex record
            // surfaced with the one-level metadata limit) is un-renderable and must be filtered.
            var field = new FieldMetaData("inner", typeof(NetworkSettings), [], "A complex record with no sub-types");
            Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.True);
        }

        [Test]
        public void ContainsUnsupportedType_CollectionOfSupportedElement_ReturnsFalse()
        {
            // US-A1: generic collections (IReadOnlyList<int>, IList<T>, ...) are now supported when their element is.
            var elementSubField = new FieldMetaData("", typeof(int), [], "");
            var field = new FieldMetaData("resourceIds", typeof(IReadOnlyList<int>), [elementSubField], "A generic collection field");
            Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.False);
        }

        [Test]
        public void ContainsUnsupportedType_CollectionOfUnrenderableElement_ReturnsTrue()
        {
            // The recursion still excludes a collection whose element is un-renderable: a complex record left with
            // no sub-types (the one-level metadata limit) has no element control, so it stays filtered.
            var elementSubField = new FieldMetaData("", typeof(NetworkSettings), [], "");
            var field = new FieldMetaData("settingsList", typeof(IReadOnlyList<NetworkSettings>), [elementSubField], "A collection of complex records");
            Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.True);
        }

        [Test]
        public void ContainsUnsupportedType_ComplexTypeWithCollectionProperty_ReturnsTrue()
        {
            // US-A7: a complex parameter whose property is a collection is left by the one-level metadata
            // expansion with NO element sub-field. The collection strategy must not claim such an element-less
            // field (it cannot build the list), so the operation is filtered rather than crashing at selection.
            var collectionProperty = new FieldMetaData("Ids", typeof(IReadOnlyList<int>), [], "");
            var field = new FieldMetaData("complex", typeof(object), [collectionProperty], "Complex with a collection property carrying no element sub-type");
            Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.True);
        }

        [Test]
        public void ContainsUnsupportedType_StringField_ReturnsFalse()
        {
            // string is IEnumerable<char> but is a fully supported simple type, never treated as a collection.
            var field = new FieldMetaData("name", typeof(string), [], "A string field");
            Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.False);
        }

        [Test]
        public void ContainsUnsupportedType_FileBackedTypeWithByteArrayData_ReturnsFalse()
        {
            // US-A6: a BinaryFile/TextFile-backed type is rendered as a single file picker by
            // FileParameterStrategy, so the filter must treat it as a supported leaf and NOT recurse into
            // its raw byte[] Data sub-field. BackupFile : BinaryFile has exactly that shape.
            var subFields = new[]
            {
                new FieldMetaData("Data", typeof(byte[]), [], ""),
                new FieldMetaData("Filename", typeof(string), [], "")
            };
            var field = new FieldMetaData("backup", typeof(BackupFile), subFields, "A binary-file-backed parameter");
            Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.False);
        }
    }
}
