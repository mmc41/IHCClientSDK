using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using static Ihc.Tests.ArchRuleHelpers;
using Type = System.Type;
using Attribute = System.Attribute;

namespace Ihc.Tests
{
    public partial class OpenVisualArchitectureTests
    {
        // The MVVM-toolkit attributes are matched by NAME, not typeof: the toolkit is a transitive package here, and
        // naming it would add a compile dependency purely to spell two strings. The name binding is not taken on
        // trust — CommandEnablementAttributeScan_IsArmed asserts the real RelayCommandAttribute is actually found in
        // the GUI assembly, which simultaneously proves the names are right and that the toolkit's attributes
        // survive compilation into metadata at all.
        private const string RelayCommandAttributeName = "RelayCommandAttribute";
        private const string NotifyCanExecuteChangedForAttributeName = "NotifyCanExecuteChangedForAttribute";
        private const string CanExecuteArgumentName = "CanExecute";

        /// <summary>
        /// The registry row's Gate is the intended command-enablement source. This rule enforces the two compiled
        /// CommunityToolkit declarations that would compete with it:
        /// <c>[NotifyCanExecuteChangedFor]</c> (a property that re-queries some OTHER command's CanExecute, so
        /// invalidation no longer flows solely from <c>OnContextChanged</c>) and <c>[RelayCommand(CanExecute = …)]</c>
        /// (a per-command predicate competing with the row's Gate). Its scope is intentionally precise: it does not
        /// claim to detect arbitrary hand-written <c>ICommand.CanExecute</c> implementations.
        /// Armed by <see cref="CommandEnablementAttributeScan_IsArmed"/>.
        /// </summary>
        [Test]
        public void Gui_DoesNotUseToolkitCommandEnablementAttributes() =>
            Assert.That(EnablementAttributeOffences(typeof(global::ihc_openvisual.App).Assembly.GetTypes()), Is.Empty,
                "GUI members must not use [NotifyCanExecuteChangedFor] or [RelayCommand(CanExecute = ...)] beside the registry row's Gate");

        /// <summary>Positive control for <see cref="Gui_DoesNotUseToolkitCommandEnablementAttributes"/>, and
        /// records the detector's feasibility evidence. Three claims: the real toolkit attributes survive into
        /// the compiled GUI metadata (otherwise the ban above would be unenforceable and would have to become a
        /// documented convention instead); the scan reports both forbidden shapes when they are present; and it does
        /// not report a plain <c>[RelayCommand]</c>, which is the sanctioned form.</summary>
        [Test]
        public void CommandEnablementAttributeScan_IsArmed() =>
            Assert.Multiple(() =>
            {
                var guiRelayCommands = typeof(global::ihc_openvisual.App).Assembly.GetTypes()
                    .SelectMany(DeclaredMembers)
                    .SelectMany(member => member.GetCustomAttributesData())
                    .Where(attribute => attribute.AttributeType.Name == RelayCommandAttributeName)
                    .ToList();

                Assert.That(guiRelayCommands, Is.Not.Empty,
                    "the toolkit's [RelayCommand] must be observable in compiled GUI metadata — if it is ever stripped, this ban silently stops enforcing and must be replaced by a documented convention");

                Assert.That(EnablementAttributeOffences(new[] { typeof(SeededEnablementAttributeUser) }), Has.Count.EqualTo(2),
                    "the scan must report BOTH forbidden shapes: the NotifyCanExecuteChangedFor member and the CanExecute-carrying RelayCommand");
                Assert.That(EnablementAttributeOffences(new[] { typeof(SeededPlainRelayCommandUser) }), Is.Empty,
                    "a plain [RelayCommand] is the sanctioned form and must NOT be reported");
            });

        // Look-alike attributes for the controls. Matching is by attribute NAME, so these exercise the real predicate
        // without pulling the MVVM toolkit (and its source generator, which would demand partial ObservableObject
        // hosts and emit real commands) into this test assembly just to seed two violations.
        [AttributeUsage(AttributeTargets.All)]
        private sealed class RelayCommandAttribute : Attribute
        {
            public string? CanExecute { get; set; }
        }

        [AttributeUsage(AttributeTargets.All)]
        private sealed class NotifyCanExecuteChangedForAttribute(string commandName) : Attribute
        {
            public string CommandName { get; } = commandName;
        }

        private sealed class SeededEnablementAttributeUser
        {
            [NotifyCanExecuteChangedFor("SaveCommand")]
            public bool Dirty => false;

            [RelayCommand(CanExecute = nameof(Dirty))]
            public void Save() { }
        }

        private sealed class SeededPlainRelayCommandUser
        {
            [RelayCommand]
            public void Save() { }
        }

        // Every member a type declares itself, across visibilities — attributes can sit on the private field or the
        // partial property behind an [ObservableProperty] as readily as on a public method.
        private static IEnumerable<MemberInfo> DeclaredMembers(Type type) =>
            type.GetMembers(BindingFlags.Instance | BindingFlags.Static
                            | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        // The two competing-enablement declarations, reported as "Type.Member: shape".
        private static IReadOnlyList<string> EnablementAttributeOffences(IEnumerable<Type> types) =>
            types.SelectMany(type => DeclaredMembers(type).Select(member => (type, member)))
                .SelectMany(hit => hit.member.GetCustomAttributesData().Select(attribute => (hit.type, hit.member, attribute)))
                .Where(hit =>
                    hit.attribute.AttributeType.Name == NotifyCanExecuteChangedForAttributeName
                    || (hit.attribute.AttributeType.Name == RelayCommandAttributeName
                        && hit.attribute.NamedArguments.Any(argument => argument.MemberName == CanExecuteArgumentName)))
                .Select(hit => $"{hit.type.Name}.{hit.member.Name}: {hit.attribute.AttributeType.Name}")
                .ToList();

        // The immutable availability-context value zone. These are
        // VALUE snapshots the registry evaluates against; a live reference in any of them would let a context in
        // hand drift while the tree mutates, which is stale enablement — the exact bug the explicit context model
        // replaced. CommandRegistry is deliberately NOT here: it is a live object with observable state, so it is
        // held to the narrower registry-purity rule below instead.
        private static readonly string CommandContextValueAttributeFullName =
            ViewModels + ".CommandContextValueAttribute";

        private static IReadOnlyCollection<Type> ContextValueZone => AuthoredGuiTypes()
            .Where(type => type.GetCustomAttributesData()
                .Any(attribute => attribute.AttributeType.FullName == CommandContextValueAttributeFullName))
            .ToList();

        /// <summary>
        /// Every type marked as a command-context value is an immutable snapshot and holds no observable live
        /// view-model. The assembly-wide <see cref="ViewModels_DoNotDependOn_Avalonia"/> and
        /// <see cref="Gui_DoesNotDirectlyRetainProjectSnapshots"/> rules own the Avalonia and project-snapshot
        /// checks, so they are deliberately not repeated here. Delegate members remain opaque because a callback
        /// that obtains current state is not itself the state. Armed by <see cref="PurityZoneDetectors_AreArmed"/>.
        /// </summary>
        [Test]
        public void ContextSnapshots_AreImmutableAndHoldNoLiveObjects() =>
            Assert.Multiple(() =>
            {
                Assert.That(ContextValueZone,
                    Is.SupersetOf(new[]
                    {
                        typeof(global::ihc_openvisual.ViewModels.ShellContext),
                        typeof(global::ihc_openvisual.ViewModels.NodeContext),
                        typeof(global::ihc_openvisual.ViewModels.ClipboardContext),
                        typeof(global::ihc_openvisual.ViewModels.Availability),
                        typeof(global::ihc_openvisual.ViewModels.CommandSpec),
                    }),
                    "the semantic marker must cover every current command-context value type");

                foreach (Type zoneType in ContextValueZone)
                {
                    Assert.That(LiveReferenceHeldBy(zoneType, IsLiveContextObject), Is.Empty,
                        $"{zoneType.Name} is a value snapshot — holding a live object lets a context in hand drift while the tree mutates");
                    Assert.That(MutableMembersOf(zoneType), Is.Empty,
                        $"{zoneType.Name} must be immutable — a mutable context snapshot can be edited after the availability it explains was computed");
                }
            });

        [Test]
        public void CommandRegistry_DoesNotRetainLiveTreeState() =>
            Assert.That(
                LiveReferenceHeldBy(typeof(global::ihc_openvisual.ViewModels.CommandRegistry), IsLiveTreeState),
                Is.Empty,
                "the registry evaluates rows against ShellContext and must not retain a live TreeNodeViewModel");

        /// <summary>Positive control for <see cref="ContextSnapshots_AreImmutableAndHoldNoLiveObjects"/> and
        /// <see cref="CommandRegistry_DoesNotRetainLiveTreeState"/>: both detectors must
        /// report against seeded violations, and must NOT report against the sanctioned shapes (a value context and
        /// a snapshot-obtaining callback) — otherwise they would be either blind or indiscriminate.</summary>
        [Test]
        public void PurityZoneDetectors_AreArmed() =>
            Assert.Multiple(() =>
            {
                Assert.That(LiveReferenceHeldBy(typeof(SeededImpureContext), IsLiveContextObject), Is.Not.Empty,
                    "the live-reference detector must flag a held view-model");
                Assert.That(LiveReferenceHeldBy(typeof(SeededImpureContext), IsLiveTreeState), Is.Not.Empty,
                    "the live-tree-state detector must flag a held TreeNodeViewModel");
                Assert.That(MutableMembersOf(typeof(SeededMutableContext)), Is.Not.Empty,
                    "the immutability detector must flag a settable property and a non-readonly field");

                Assert.That(LiveReferenceHeldBy(typeof(global::ihc_openvisual.ViewModels.ShellContext), IsLiveContextObject), Is.Empty,
                    "the real context must not trip the detector — otherwise the rule above is passing for the wrong reason");
                Assert.That(MutableMembersOf(typeof(global::ihc_openvisual.ViewModels.ShellContext)), Is.Empty,
                    "the real context is immutable");
            });

        // Seeded violators for the purity-zone controls.
        private sealed record SeededImpureContext(
            global::ihc_openvisual.ViewModels.TreeNodeViewModel Node,
            global::Ihc.Vis.Projects.Project Project);

        private sealed class SeededMutableContext
        {
            public bool Mutable { get; set; }
#pragma warning disable CS0649 // never assigned: the field exists only so the detector has a non-readonly one to find
            internal int Field;
#pragma warning restore CS0649
        }

        // A live object a value snapshot must not hold: the two mutable model roots, or anything that raises
        // property-change notifications (the mechanical stand-in for "a view-model").
        private static bool IsLiveContextObject(Type type) =>
            typeof(INotifyPropertyChanged).IsAssignableFrom(type);

        private static bool IsLiveTreeState(Type type) =>
            type == typeof(global::ihc_openvisual.ViewModels.TreeNodeViewModel);

        // Every member of the type that holds something matching the predicate, reported as "Type.Member : Held".
        private static IReadOnlyList<string> LiveReferenceHeldBy(Type owner, Func<Type, bool> forbidden) =>
            owner.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(f => (Member: f.Name, Held: FirstReferenced(f.FieldType, forbidden)))
                .Concat(owner.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Select(p => (Member: p.Name, Held: FirstReferenced(p.PropertyType, forbidden))))
                .Where(hit => hit.Held is not null)
                .Select(hit => $"{owner.Name}.{hit.Member} : {hit.Held!.Name}")
                .Distinct()
                .ToList();

        // Instance state that can change after construction: a non-readonly field, or a property with a setter that
        // is not init-only. Records satisfy this by construction; the rule exists so a later hand-written member
        // cannot quietly reintroduce mutability into a snapshot type.
        private static IReadOnlyList<string> MutableMembersOf(Type owner) =>
            owner.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(f => !f.IsInitOnly)
                .Select(f => $"{owner.Name}.{f.Name} (settable field)")
                .Concat(owner.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(p => p.SetMethod is { } setter && !IsInitOnly(setter))
                    .Select(p => $"{owner.Name}.{p.Name} (settable property)"))
                .ToList();

        private static bool IsInitOnly(MethodInfo setter) =>
            setter.ReturnParameter.GetRequiredCustomModifiers()
                .Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");

    }
}
