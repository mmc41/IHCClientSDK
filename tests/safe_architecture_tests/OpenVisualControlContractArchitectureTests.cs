using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Type = System.Type;

namespace Ihc.Tests
{
    public partial class OpenVisualArchitectureTests
    {
        private const string CreateContainerOverrideName = "CreateContainerForItemOverride";
        private const string NeedsContainerOverrideName = "NeedsContainerOverride";

        /// <summary>
        /// An items control that builds its own containers must also state which items NEED one. Avalonia's default
        /// rule passes a <see cref="Separator"/> (and an already-authored <see cref="MenuItem"/>) through untouched;
        /// a subclass that overrides only the factory generates a container for everything, so every separator in
        /// every menu becomes a real, nameless, invokable row — an automation client counting the File menu finds
        /// eleven commands instead of seven, four of which do nothing, and a screen reader reads the blanks out.
        ///
        /// Reflection verifies the exact protected signatures and a genuine base override; an unrelated overload or
        /// hidden member with the same name cannot satisfy the rule. Armed by
        /// <see cref="ContainerRulePairingScan_IsArmed"/>.
        /// </summary>
        [Test]
        public void ContainerFactories_AlsoStateWhichItemsNeedAContainer()
        {
            var factories = AuthoredGuiTypes().Where(DeclaresContainerFactoryOverride).ToList();
            Assert.That(factories, Is.Not.Empty,
                $"sanity: some GUI control overrides {CreateContainerOverrideName} — otherwise this rule watches nothing");

            Assert.That(ContainerFactoriesWithoutAContainerRule(factories), Is.Empty,
                $"a control overriding {CreateContainerOverrideName} must also override {NeedsContainerOverrideName} — otherwise it wraps items that are already their own container (a wrapped Separator reaches automation as a nameless, invokable command)");
        }

        /// <summary>Positive control for <see cref="ContainerFactories_AlsoStateWhichItemsNeedAContainer"/>: the scan
        /// must report a factory-only type and must not report the real accessible controls.</summary>
        [Test]
        public void ContainerRulePairingScan_IsArmed() =>
            Assert.Multiple(() =>
            {
                Assert.That(ContainerFactoriesWithoutAContainerRule(new[] { typeof(SeededContainerFactory) }), Is.Not.Empty,
                    "the scan must report a container factory that states no container rule");
                Assert.That(ContainerFactoriesWithoutAContainerRule(AccessibleControlTypes), Is.Empty,
                    "and must not report the real accessible controls, which override both");
            });

        // Seeded violator: genuinely overrides the factory but offers only a wrong-signature rule overload.
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Deliberately the right name with the wrong signature. Making it static changes the signature further and "
            + "stops it seeding the override contract the rule checks.")]
        private sealed class SeededContainerFactory : Menu
        {
            protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
                new MenuItem();

            // Deliberately the right name with the wrong signature: it must not satisfy the override contract.
            public bool NeedsContainerOverride(object? item, int index, object? recycleKey) => true;
        }

        private static IReadOnlyList<string> ContainerFactoriesWithoutAContainerRule(IEnumerable<Type> types) =>
            types
                .Where(type => DeclaresContainerFactoryOverride(type) && !DeclaresContainerRuleOverride(type))
                .Select(type => $"{type.Name} builds containers but never says which items need one")
                .ToList();

        private const string StyleKeyOverrideName = "StyleKeyOverride";

        /// <summary>
        /// A custom control that extends a THEMED framework control must keep that control's theme: Avalonia resolves
        /// a control theme by exact type, so a subclass without <c>StyleKeyOverride</c> finds none and renders as an
        /// untemplated blank — the menu bar disappears rather than misbehaving. That is an automation regression as
        /// much as a visual one (an unrealized template has no peer subtree to walk), and it is exactly the trap the
        /// Accessible* controls exist inside: replacing a stock control is the ONLY reason to subclass one here.
        ///
        /// Scoped to the Controls namespace subtree and direct subclasses of the four stock menu/tree controls this
        /// app replaces. Windows, UserControls, panels, and unrelated custom controls are outside this rule's claim.
        /// Armed by <see cref="ThemeKeyScan_IsArmed"/>.
        /// </summary>
        [Test]
        public void CustomControls_KeepTheThemeOfTheControlTheyReplace()
        {
            var customControls = AuthoredGuiTypes()
                .Where(type => type.Namespace == Controls
                               || type.Namespace?.StartsWith(Controls + ".", StringComparison.Ordinal) == true)
                .ToList();
            Assert.That(customControls, Is.Not.Empty, "sanity: the Controls layer declares types");

            Assert.That(ThemelessControlSubclasses(customControls), Is.Empty,
                $"a replacement for a stock menu/tree control must genuinely override {StyleKeyOverrideName} with the exact property signature");
        }

        /// <summary>Positive control for <see cref="CustomControls_KeepTheThemeOfTheControlTheyReplace"/>: reports a
        /// stock-control subclass with only a wrong-signature namesake, and stays quiet for the real replacements.
        /// A Grid-derived data row is outside the semantic source set.</summary>
        [Test]
        public void ThemeKeyScan_IsArmed() =>
            Assert.Multiple(() =>
            {
                Assert.That(ThemelessControlSubclasses(new[] { typeof(SeededUnthemedControl) }), Is.Not.Empty,
                    "the scan must report a themed-control subclass with no StyleKeyOverride");
                Assert.That(ThemelessControlSubclasses(AccessibleControlTypes), Is.Empty,
                    "and must not report the real accessible controls, which declare it");
                Assert.That(ThemelessControlSubclasses(new[] { typeof(global::ihc_openvisual.Controls.AccessibleDataRow) }), Is.Empty,
                    "nor the Grid-derived data row — a panel has no control theme to lose");
            });

        // Seeded violator: a themed framework control extended without restating its style key.
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "A `new` instance member is what shadows the style key; `static new` is a different construct entirely.")]
        private sealed class SeededUnthemedControl : Menu
        {
            public new Type StyleKeyOverride(int ignored) => typeof(Menu);
        }

        private static IReadOnlyList<string> ThemelessControlSubclasses(IEnumerable<Type> types) =>
            types
                .Where(type => type.BaseType is { } theBase
                               && StockMenuAndTreeControlTypeNames().Contains(theBase.FullName!)
                               && !DeclaresStyleKeyOverride(type))
                .Select(type => $"{type.Name} : {type.BaseType!.Name} has no {StyleKeyOverrideName}")
                .ToList();

        private static MethodInfo? DeclaredOverride(Type type, string name, Type returnType,
            params Type[] parameterTypes)
        {
            MethodInfo? method = type.GetMethod(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                binder: null, types: parameterTypes, modifiers: null);
            return method is { IsVirtual: true }
                   && method.ReturnType == returnType
                   && method.GetBaseDefinition().DeclaringType != type
                ? method
                : null;
        }

        private static bool DeclaresContainerFactoryOverride(Type type) =>
            DeclaredOverride(type, CreateContainerOverrideName, typeof(Control),
                typeof(object), typeof(int), typeof(object)) is not null;

        private static bool DeclaresContainerRuleOverride(Type type) =>
            DeclaredOverride(type, NeedsContainerOverrideName, typeof(bool),
                typeof(object), typeof(int), typeof(object).MakeByRefType()) is { } method
            && method.GetParameters()[2].IsOut;

        private static bool DeclaresStyleKeyOverride(Type type)
        {
            PropertyInfo? property = type.GetProperty(StyleKeyOverrideName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            return property is not null
                   && property.PropertyType == typeof(Type)
                   && property.GetMethod is { IsVirtual: true } getter
                   && getter.GetBaseDefinition().DeclaringType != type;
        }

    }
}
