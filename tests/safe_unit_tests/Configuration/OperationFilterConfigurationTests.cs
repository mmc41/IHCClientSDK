using System;
using System.Collections.Generic;
using Ihc;
using Ihc.App;

namespace Safe_Unit_Tests.Configuration;

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
    public void ContainsUnsupportedType_ArrayField_ReturnsTrue()
    {
        var field = new FieldMetaData("values", typeof(int[]), [], "An array field");
        Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.True);
    }

    [Test]
    public void ContainsUnsupportedType_ResourceValueField_ReturnsTrue()
    {
        var field = new FieldMetaData("resource", typeof(ResourceValue), [], "A ResourceValue field");
        Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.True);
    }

    [Test]
    public void ContainsUnsupportedType_NestedArrayInComplexType_ReturnsTrue()
    {
        // A complex field whose sub-field is an array must be excluded (recursive check).
        var arraySubField = new FieldMetaData("tags", typeof(string[]), [], "");
        var field = new FieldMetaData("complex", typeof(object), [arraySubField], "Complex with an array sub-field");
        Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.True);
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
    public void ContainsUnsupportedType_GenericCollectionInterfaceField_ReturnsTrue()
    {
        // Non-array generic collection interfaces (IReadOnlyList<int>, IList<T>, ...) have no control strategy.
        var field = new FieldMetaData("resourceIds", typeof(IReadOnlyList<int>), [], "A generic collection field");
        Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.True);
    }

    [Test]
    public void ContainsUnsupportedType_ConcreteListField_ReturnsTrue()
    {
        var field = new FieldMetaData("names", typeof(List<string>), [], "A concrete list field");
        Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.True);
    }

    [Test]
    public void ContainsUnsupportedType_StringField_ReturnsFalse()
    {
        // string is IEnumerable<char> but is a fully supported simple type, never treated as a collection.
        var field = new FieldMetaData("name", typeof(string), [], "A string field");
        Assert.That(OperationFilterConfiguration.ContainsUnsupportedType(field), Is.False);
    }
}
