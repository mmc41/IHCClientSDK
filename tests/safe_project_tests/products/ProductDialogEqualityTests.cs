using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Equality of the product-dialog metadata records, now that all five carry compiler-generated equality over
    /// <see cref="EquatableArray{T}"/> members instead of a handwritten <c>Equals</c>/<c>GetHashCode</c> pair.
    /// <para>The point of these tests is the <b>init members</b>. A handwritten pair covers exactly the members
    /// someone remembered to list, and this family already proved it: <c>DialogDescriptorField.ColumnSpan</c> was
    /// omitted from its own <c>Equals</c> for a while. Each member below is asserted to affect equality on its own,
    /// so a member that stops counting fails a test rather than waiting to be noticed in review.</para>
    /// </summary>
    public class ProductDialogEqualityTests
    {
        private static DialogFieldModel Field() =>
            new DialogFieldModel("navn", "Navn", DialogControlKind.Text, new DialogBinding.RootAttribute("name"));

        private static DialogGroupModel Group() =>
            new DialogGroupModel("identitet", "Produkt egenskaber", 1, [Field()]);

        private static DialogDescriptorField DescriptorField() =>
            new DialogDescriptorField("dlg.identitet.navn", "Navn", DialogControlKind.Text,
                new ElementId(1, 2), "name", "Stue", ReadOnly: false, Rule: null, Minimum: null, Maximum: null);

        private static DialogDescriptorGroup DescriptorGroup() =>
            new DialogDescriptorGroup("identitet", "Produkt egenskaber", 1, [DescriptorField()], []);

        // ---- Independently built equal values compare equal (the reference-equality trap) ----

        [Test]
        public void IndependentlyBuiltDescriptors_WithEqualContent_AreEqual()
        {
            ProductDialogDescriptor a = new ProductDialogDescriptor("SMS modem", [DescriptorGroup()]);
            ProductDialogDescriptor b = new ProductDialogDescriptor("SMS modem", [DescriptorGroup()]);

            Assert.Multiple(() =>
            {
                Assert.That(a, Is.EqualTo(b), "no backing array is shared between these two");
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            });
        }

        [Test]
        public void DescriptorField_DefaultSuggestions_EqualsExplicitlyEmptySuggestions()
        {
            // The wrapper's default-is-empty rule, reaching the record: this is why SuggestionsOrEmpty could go.
            DialogDescriptorField unspecified = DescriptorField();
            DialogDescriptorField explicitlyEmpty = DescriptorField() with { Suggestions = [] };

            Assert.Multiple(() =>
            {
                Assert.That(unspecified, Is.EqualTo(explicitlyEmpty));
                Assert.That(unspecified.GetHashCode(), Is.EqualTo(explicitlyEmpty.GetHashCode()));
                Assert.That(unspecified.Suggestions.IsEmpty, Is.True);
            });
        }

        [Test]
        public void DescriptorField_IndependentlyBuiltEqualSuggestions_AreEqual()
        {
            DialogDescriptorField a = DescriptorField() with { Suggestions = ["I loft", "Ved dør"] };
            DialogDescriptorField b = DescriptorField() with { Suggestions = ["I loft", "Ved dør"] };

            Assert.Multiple(() =>
            {
                Assert.That(a, Is.EqualTo(b));
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            });
        }

        // ---- Every stored init member affects equality, with no member list to maintain ----

        [Test]
        public void ProductDialogModel_TitleSuffix_AffectsEquality()
        {
            ProductDialogModel bare = new ProductDialogModel([Group()]);
            ProductDialogModel suffixed = bare with { TitleSuffix = " Egenskaber" };

            Assert.That(bare, Is.Not.EqualTo(suffixed), "only the modem titles itself this way");
        }

        [Test]
        public void DialogGroupModel_ColumnMajor_AffectsEquality()
        {
            DialogGroupModel across = Group();
            DialogGroupModel down = Group() with { ColumnMajor = true };

            Assert.That(across, Is.Not.EqualTo(down), "reading order is content, not a rendering afterthought");
        }

        [Test]
        public void DialogGroupModel_Presence_AffectsEquality()
        {
            DialogGroupModel always = Group();
            DialogGroupModel conditional = Group() with { Presence = new DialogPresence.DescendantTag("jalousi") };

            Assert.That(always, Is.Not.EqualTo(conditional), "a family-optional group is not the same group");
        }

        [Test]
        public void DialogFieldModel_ColumnSpan_AffectsEquality()
        {
            DialogFieldModel single = Field();
            DialogFieldModel wholeRow = Field() with { ColumnSpan = 2 };

            Assert.That(single, Is.Not.EqualTo(wholeRow), "the member whose omission started this");
        }

        [Test]
        public void DialogFieldModel_HidesUnresolvedResourceKey_AffectsEquality()
        {
            DialogFieldModel shown = Field();
            DialogFieldModel blanked = Field() with { HidesUnresolvedResourceKey = true };

            Assert.That(shown, Is.Not.EqualTo(blanked),
                "the newest init member — covered without anyone editing an Equals");
        }

        [Test]
        public void DialogDescriptorGroup_ColumnMajor_AffectsEquality()
        {
            DialogDescriptorGroup across = DescriptorGroup();
            DialogDescriptorGroup down = DescriptorGroup() with { ColumnMajor = true };

            Assert.That(across, Is.Not.EqualTo(down));
        }

        [Test]
        public void DialogDescriptorGroup_Widgets_AffectEquality()
        {
            DialogDescriptorGroup plain = DescriptorGroup();
            DialogDescriptorGroup withGrids = DescriptorGroup() with { Widgets = [DialogWidgetKind.TerminalGrids] };

            Assert.That(plain, Is.Not.EqualTo(withGrids));
        }

        [Test]
        public void DialogDescriptorField_ColumnSpan_AffectsEquality()
        {
            DialogDescriptorField single = DescriptorField();
            DialogDescriptorField wholeRow = DescriptorField() with { ColumnSpan = 2 };

            Assert.That(single, Is.Not.EqualTo(wholeRow));
        }

        [Test]
        public void DialogDescriptorField_Suggestions_AffectEquality()
        {
            DialogDescriptorField a = DescriptorField() with { Suggestions = ["I loft"] };
            DialogDescriptorField b = DescriptorField() with { Suggestions = ["Ved dør"] };

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void DescriptorField_DifferingOnlyDeepInsideTheDescriptor_BreaksEquality()
        {
            ProductDialogDescriptor a = new ProductDialogDescriptor("SMS modem", [DescriptorGroup()]);
            ProductDialogDescriptor b = new ProductDialogDescriptor("SMS modem",
                [DescriptorGroup() with { Fields = [DescriptorField() with { Value = "Kontor" }] }]);

            Assert.That(a, Is.Not.EqualTo(b), "equality recurses two collection levels down");
        }

        // ---- Step 4's completion criterion, asserted rather than eyeballed ----

        /// <summary>
        /// None of the five dialog records may declare its own <c>Equals</c>/<c>GetHashCode</c> any more.
        /// <para>Records always <i>have</i> both, so their mere presence proves nothing; what separates a
        /// synthesized member from a handwritten one is <see cref="CompilerGeneratedAttribute"/>, which Roslyn
        /// stamps on the members it emits and cannot appear on hand-authored source. A handwritten pair
        /// reintroduced here — the drift this whole convention exists to prevent — fails this test.</para>
        /// <para>Scoped to equality: <c>ToString()</c> stays deliberately handwritten on two of these types.</para>
        /// </summary>
        [Test]
        public void TheDialogRecords_DeclareNoHandwrittenEqualityMemberList()
        {
            System.Type[] dialogRecords =
            [
                typeof(ProductDialogModel),
                typeof(DialogGroupModel),
                typeof(ProductDialogDescriptor),
                typeof(DialogDescriptorGroup),
                typeof(DialogDescriptorField),
            ];

            string[] handwritten = dialogRecords
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(m => m.Name is "Equals" or "GetHashCode")
                .Where(m => !m.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
                .ToArray();

            Assert.That(handwritten, Is.Empty,
                "equality must stay compiler-generated so new members are covered automatically");
        }

        /// <summary>
        /// The detector above must be able to fail. A vacuous version — one whose attribute test never matches
        /// anything — would pass for a type family that had gone back to handwritten equality, so this pins that
        /// the same query DOES flag a known handwritten implementation.
        /// </summary>
        [Test]
        public void TheHandwrittenEqualityDetector_IsArmed()
        {
            MethodInfo handwritten = typeof(HandwrittenEqualityProbe)
                .GetMethod(nameof(HandwrittenEqualityProbe.GetHashCode), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
            MethodInfo synthesized = typeof(SynthesizedEqualityProbe)
                .GetMethod(nameof(SynthesizedEqualityProbe.GetHashCode), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;

            Assert.Multiple(() =>
            {
                Assert.That(handwritten.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false), Is.False,
                    "a hand-authored GetHashCode must be detected as handwritten");
                Assert.That(synthesized.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false), Is.True,
                    "a record's own GetHashCode must be detected as generated");
            });
        }

        private sealed record HandwrittenEqualityProbe(int Value)
        {
            public override int GetHashCode() => Value;
        }

        private sealed record SynthesizedEqualityProbe(int Value);
    }
}
