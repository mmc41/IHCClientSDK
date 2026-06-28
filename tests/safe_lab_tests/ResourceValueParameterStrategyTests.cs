using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using NUnit.Framework;
using Ihc;
using IhcLab;
using IhcLab.ParameterControls.Strategies;

namespace Ihc.Tests
{
    /// <summary>
    /// US-A3: the ResourceValue union editor - a ValueKind dropdown (all 12 kinds) that swaps the payload editor,
    /// so ExtractValue builds a UnionValue with the correct kind and field(s). ENUM uses two numeric id fields (D5).
    /// </summary>
    [TestFixture]
    public class ResourceValueParameterStrategyTests : AvaloniaTestBase
    {
        private ResourceValueParameterStrategy strategy;
        private static readonly FieldMetaData Field = new("v", typeof(ResourceValue), [], "A resource value");

        [SetUp]
        public void SetUp() => strategy = new ResourceValueParameterStrategy();

        private static T Find<T>(Control root, string name) where T : Control =>
            root.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);

        private static void SelectKind(Control control, ResourceValue.ValueKind kind) =>
            Find<ComboBox>(control, $"{control.Name}.ValueKind").SelectedItem = kind.ToString();

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void CreateControl_OffersAllTwelveKinds()
        {
            var control = strategy.CreateControl(Field, "0");
            var combo = Find<ComboBox>(control, "0.ValueKind");

            Assert.That(combo.ItemsSource!.Cast<object>().Count(), Is.EqualTo(12));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_IntKind_BuildsIntUnion()
        {
            // Arrange
            var control = strategy.CreateControl(Field, "0");
            SelectKind(control, ResourceValue.ValueKind.INT);
            Find<NumericUpDown>(control, "0.ResourceID").Value = 100;
            Find<NumericUpDown>(control, "0.Payload.value").Value = 42;

            // Act
            var rv = strategy.ExtractValue(control, Field) as ResourceValue;

            // Assert
            Assert.That(rv, Is.Not.Null);
            Assert.That(rv!.ResourceID, Is.EqualTo(100));
            Assert.That(rv!.Value.ValueKind, Is.EqualTo(ResourceValue.ValueKind.INT));
            Assert.That(rv!.Value.IntValue, Is.EqualTo(42));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_BoolKind_BuildsBoolUnion()
        {
            // Arrange
            var control = strategy.CreateControl(Field, "0");
            SelectKind(control, ResourceValue.ValueKind.BOOL);
            Find<CheckBox>(control, "0.Payload.value").IsChecked = true;

            // Act
            var rv = strategy.ExtractValue(control, Field) as ResourceValue;

            // Assert
            Assert.That(rv!.Value.ValueKind, Is.EqualTo(ResourceValue.ValueKind.BOOL));
            Assert.That(rv!.Value.BoolValue, Is.EqualTo(true));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ChangingKind_RebuildsPayloadToOnlyTheValidField()
        {
            // Arrange - INT shows a numeric payload
            var control = strategy.CreateControl(Field, "0");
            SelectKind(control, ResourceValue.ValueKind.INT);
            Assert.That(control.GetLogicalDescendants().OfType<NumericUpDown>().Any(c => c.Name == "0.Payload.value"), Is.True);

            // Act - switch to TIME (an hh:mm:ss duration input)
            SelectKind(control, ResourceValue.ValueKind.TIME);

            // Assert - the numeric payload is gone, replaced by a DurationInput
            Assert.That(control.GetLogicalDescendants().OfType<NumericUpDown>().Any(c => c.Name == "0.Payload.value"), Is.False);
            Assert.That(control.GetLogicalDescendants().OfType<DurationInput>().Any(c => c.Name == "0.Payload.value"), Is.True);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_TimeKind_ValidText_BuildsTimeUnion()
        {
            // Arrange
            var control = strategy.CreateControl(Field, "0");
            SelectKind(control, ResourceValue.ValueKind.TIME);
            Find<DurationInput>(control, "0.Payload.value").Text = "01:30:00";

            // Act
            var rv = strategy.ExtractValue(control, Field) as ResourceValue;

            // Assert
            Assert.That(rv!.Value.ValueKind, Is.EqualTo(ResourceValue.ValueKind.TIME));
            Assert.That(rv!.Value.TimeValue, Is.EqualTo(new TimeSpan(1, 30, 0)));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_TimeKind_InvalidText_ThrowsInsteadOfCoercingToZero()
        {
            // Arrange - a mistyped TIME must be rejected, not silently sent as 00:00:00 (FOUND-02).
            var control = strategy.CreateControl(Field, "0");
            SelectKind(control, ResourceValue.ValueKind.TIME);
            Find<DurationInput>(control, "0.Payload.value").Text = "not-a-time";

            // Act & Assert
            Assert.Throws<FormatException>(() => strategy.ExtractValue(control, Field));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_EnumKind_BuildsEnumUnionFromTwoIdFields()
        {
            // Arrange - ENUM = two numeric id fields (D5)
            var control = strategy.CreateControl(Field, "0");
            SelectKind(control, ResourceValue.ValueKind.ENUM);
            Find<NumericUpDown>(control, "0.Payload.definitionTypeID").Value = 7;
            Find<NumericUpDown>(control, "0.Payload.enumValueID").Value = 3;

            // Act
            var rv = strategy.ExtractValue(control, Field) as ResourceValue;

            // Assert
            Assert.That(rv!.Value.ValueKind, Is.EqualTo(ResourceValue.ValueKind.ENUM));
            Assert.That(rv!.Value.EnumValue.DefinitionTypeID, Is.EqualTo(7));
            Assert.That(rv!.Value.EnumValue.EnumValueID, Is.EqualTo(3));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_SceneDimmerKind_BuildsCompositeUnion()
        {
            // Arrange - a composite (D10) kind with three numeric fields
            var control = strategy.CreateControl(Field, "0");
            SelectKind(control, ResourceValue.ValueKind.SceneDimmer);
            Find<NumericUpDown>(control, "0.Payload.dimmerPercentage").Value = 60;
            Find<NumericUpDown>(control, "0.Payload.delayTime").Value = 100;
            Find<NumericUpDown>(control, "0.Payload.rampTime").Value = 200;

            // Act
            var rv = strategy.ExtractValue(control, Field) as ResourceValue;

            // Assert
            Assert.That(rv!.Value.ValueKind, Is.EqualTo(ResourceValue.ValueKind.SceneDimmer));
            Assert.That(rv!.Value.DimmerPercentage, Is.EqualTo(60));
            Assert.That(rv!.Value.DimmerDelayTime, Is.EqualTo(100));
            Assert.That(rv!.Value.DimmerRampTime, Is.EqualTo(200));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_RestoresKindAndPayload()
        {
            // Arrange
            var control = strategy.CreateControl(Field, "0");
            var stored = new ResourceValue
            {
                ResourceID = 55,
                Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.INT, IntValue = 99 }
            };

            // Act - restore, then re-extract
            strategy.SetValue(control, stored, Field);
            var rv = strategy.ExtractValue(control, Field) as ResourceValue;

            // Assert - kind, resource id and payload round-trip
            Assert.That(Find<ComboBox>(control, "0.ValueKind").SelectedItem, Is.EqualTo("INT"));
            Assert.That(rv!.ResourceID, Is.EqualTo(55));
            Assert.That(rv!.Value.ValueKind, Is.EqualTo(ResourceValue.ValueKind.INT));
            Assert.That(rv!.Value.IntValue, Is.EqualTo(99));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_SameKind_ReusesPayloadControlAndUpdatesValue()
        {
            // A same-kind restore (the round-trip that fires on every payload edit, because ResourceValue's record
            // equality includes a ValueTime stamp that differs on every extract) must reuse the existing payload
            // editor, NOT rebuild it - otherwise the control the user is editing is destroyed mid-keystroke (US-A3).
            var control = strategy.CreateControl(Field, "0");
            strategy.SetValue(control, new ResourceValue { ResourceID = 1, Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.INT, IntValue = 5 } }, Field);
            var before = Find<NumericUpDown>(control, "0.Payload.value");

            // Act - restore another INT value
            strategy.SetValue(control, new ResourceValue { ResourceID = 1, Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.INT, IntValue = 8 } }, Field);

            // Assert - same payload control instance, value updated
            var after = Find<NumericUpDown>(control, "0.Payload.value");
            Assert.That(ReferenceEquals(before, after), Is.True, "same-kind restore must reuse the payload editor, not rebuild it");
            Assert.That(after.Value, Is.EqualTo(8));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void SetValue_DifferentKind_RebuildsPayload()
        {
            // Restoring a different kind DOES rebuild the payload to the new kind's editor.
            var control = strategy.CreateControl(Field, "0");
            strategy.SetValue(control, new ResourceValue { Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.INT, IntValue = 5 } }, Field);
            Assert.That(control.GetLogicalDescendants().OfType<NumericUpDown>().Any(c => c.Name == "0.Payload.value"), Is.True);

            // Act - restore a BOOL value (different kind)
            strategy.SetValue(control, new ResourceValue { Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.BOOL, BoolValue = true } }, Field);

            // Assert - the numeric payload is replaced by a checkbox
            Assert.That(control.GetLogicalDescendants().OfType<NumericUpDown>().Any(c => c.Name == "0.Payload.value"), Is.False);
            Assert.That(control.GetLogicalDescendants().OfType<CheckBox>().Any(c => c.Name == "0.Payload.value"), Is.True);
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_PhoneNumberKind_BuildsStringUnion()
        {
            var control = strategy.CreateControl(Field, "0");
            SelectKind(control, ResourceValue.ValueKind.PhoneNumber);
            Find<TextBox>(control, "0.Payload.number").Text = "+4512345678";

            var rv = strategy.ExtractValue(control, Field) as ResourceValue;

            Assert.That(rv!.Value.ValueKind, Is.EqualTo(ResourceValue.ValueKind.PhoneNumber));
            Assert.That(rv!.Value.PhoneNumberValue, Is.EqualTo("+4512345678"));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_SceneRelayKind_BuildsCompositeUnion()
        {
            var control = strategy.CreateControl(Field, "0");
            SelectKind(control, ResourceValue.ValueKind.SceneRelay);
            Find<NumericUpDown>(control, "0.Payload.delayTime").Value = 50;
            Find<CheckBox>(control, "0.Payload.relayValue").IsChecked = true;

            var rv = strategy.ExtractValue(control, Field) as ResourceValue;

            Assert.That(rv!.Value.ValueKind, Is.EqualTo(ResourceValue.ValueKind.SceneRelay));
            Assert.That(rv!.Value.RelayDelayTime, Is.EqualTo(50));
            Assert.That(rv!.Value.RelayValue, Is.EqualTo(true));
        }

        [AvaloniaTest]
        [CaptureScreenshotOnFailure]
        public void ExtractValue_SceneShutterKind_BuildsCompositeUnion()
        {
            var control = strategy.CreateControl(Field, "0");
            SelectKind(control, ResourceValue.ValueKind.SceneShutter);
            Find<CheckBox>(control, "0.Payload.shutterPositionIsUp").IsChecked = true;
            Find<NumericUpDown>(control, "0.Payload.delayTime").Value = 30;

            var rv = strategy.ExtractValue(control, Field) as ResourceValue;

            Assert.That(rv!.Value.ValueKind, Is.EqualTo(ResourceValue.ValueKind.SceneShutter));
            Assert.That(rv!.Value.ShutterPositionIsUp, Is.EqualTo(true));
            Assert.That(rv!.Value.ShutterDelayTime, Is.EqualTo(30));
        }
    }
}
