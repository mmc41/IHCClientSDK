using System;
using Ihc;
using Ihc.App;

namespace Safe_Unit_Tests.Configuration;

/// <summary>
/// Equivalence-partition tests for <see cref="OperationFilterConfiguration.ContainsUnsupportedType"/>.
/// Documents the deliberate, deferred contract: operations whose parameters contain arrays or
/// ResourceValue (directly or nested) are excluded from the GUI, while scalar and complex-of-scalar
/// parameters are allowed.
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
}
