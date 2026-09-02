using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Type = System.Type;

namespace Ihc.Tests
{
    public partial class OpenVisualArchitectureTests
    {
        // The peer method the platform bridge actually asks, and the namespace holding the UIA pattern interfaces.
        private const string GetProviderCoreName = "GetProviderCore";
        private static readonly string ProviderNamespace = typeof(IInvokeProvider).Namespace!; // Avalonia.Automation.Provider

        /// <summary>
        /// A peer that implements a UIA pattern interface its base peer does not must ALSO override
        /// <c>GetProviderCore</c> whenever some base peer overrides it — because that method, not the CLR interface
        /// list, is what the platform bridge asks. A base override that answers only its own patterns swallows the
        /// added one, so the peer advertises Invoke to C# and nothing to the driver: the exact defect that left every
        /// menu item in this app reporting one pattern (ScrollItem) while <c>OperableMenuItemAutomationPeer</c>
        /// appeared to implement two.
        ///
        /// The condition is deliberately narrow — required only when a base other than <c>AutomationPeer</c> declares
        /// the method — because <c>AutomationPeer</c>'s own default resolves providers off the interface list, so a
        /// peer over a non-overriding base (<c>ExpandCollapseTreeViewItemAutomationPeer</c> today) is correct without
        /// one. That makes this primarily an AVALONIA-UPGRADE tripwire: in 12.1 only <c>MenuItemAutomationPeer</c>
        /// overrides <c>GetProviderCore</c>, and the day a release adds the override to <c>TreeViewItemAutomationPeer</c>
        /// the tree's ExpandCollapse would go dark with no source change and no behavioural test to catch it on the
        /// old version. Armed by <see cref="ProviderSurfacingScan_IsArmed"/>.
        /// </summary>
        [Test]
        public void AutomationPeers_SurfaceAddedProvidersThroughGetProviderCore()
        {
            var peers = AuthoredGuiTypes().Where(type => typeof(AutomationPeer).IsAssignableFrom(type)).ToList();
            Assert.That(peers, Is.Not.Empty, "sanity: the GUI declares automation peers — otherwise this watches nothing");

            Assert.That(PeersHidingAddedProviders(peers), Is.Empty,
                $"a peer adding a UIA pattern must override {GetProviderCoreName} when a base peer overrides it — the bridge resolves providers through that method, so an interface the base does not answer for reaches no automation client");
        }

        /// <summary>Positive control for <see cref="AutomationPeers_SurfaceAddedProvidersThroughGetProviderCore"/>,
        /// plus the two facts that give the rule its shape: <c>MenuItemAutomationPeer</c> DOES override
        /// <c>GetProviderCore</c> (so the app's override is load-bearing, not decorative) and
        /// <c>TreeViewItemAutomationPeer</c> does NOT (so the tree peer is legitimately exempt — and this assertion is
        /// what turns a future Avalonia release adding it into a visible, explained failure).</summary>
        [Test]
        public void ProviderSurfacingScan_IsArmed() =>
            Assert.Multiple(() =>
            {
                Assert.That(DeclaresGetProviderCore(typeof(MenuItemAutomationPeer)), Is.True,
                    $"premise: Avalonia's menu-item peer overrides {GetProviderCoreName}, which is why a peer adding Invoke must too");
                Assert.That(DeclaresGetProviderCore(typeof(TreeViewItemAutomationPeer)), Is.False,
                    $"premise: Avalonia's tree-item peer does NOT override {GetProviderCoreName} — if a release adds it, ExpandCollapseTreeViewItemAutomationPeer starts being reported, which is the intended alarm, not a false positive");

                Assert.That(PeersHidingAddedProviders(new[] { typeof(SeededSwallowedProviderPeer) }), Is.Not.Empty,
                    "the scan must report a peer that adds Invoke over an overriding base without overriding itself");
                Assert.That(
                    PeersHidingAddedProviders(new[] { typeof(global::ihc_openvisual.Controls.OperableMenuItemAutomationPeer) }),
                    Is.Empty,
                    "and must NOT report the real menu peer, which does override it — otherwise the rule is indiscriminate");
                Assert.That(
                    PeersHidingAddedProviders(new[] { typeof(global::ihc_openvisual.Controls.ExpandCollapseTreeViewItemAutomationPeer) }),
                    Is.Empty,
                    "nor the tree peer, whose base leaves AutomationPeer's interface-based default in place");
            });

        // Seeded violator: adds Invoke over a base that overrides GetProviderCore, without overriding it — the exact
        // shape that compiles, reads correctly, and reaches no automation client.
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The seed exists to hide a base virtual with an INSTANCE member. Static is a different construct, so a "
            + "static seed would stop reproducing the defect the rule detects.")]
        private sealed class SeededSwallowedProviderPeer : MenuItemAutomationPeer, IInvokeProvider
        {
            public SeededSwallowedProviderPeer(MenuItem owner) : base(owner)
            {
            }

            public void Invoke()
            {
            }

            public object? GetProviderCore(string providerType) => null;
        }

        // Peers whose added pattern interfaces are swallowed by an overriding base, reported as "Peer : Base adds …".
        private static IReadOnlyList<string> PeersHidingAddedProviders(IEnumerable<Type> types) =>
            types
                .Where(type => typeof(AutomationPeer).IsAssignableFrom(type) && type.BaseType is not null)
                .Select(type => (Type: type, Added: AddedProviderInterfaces(type), Swallower: OverridingBase(type.BaseType!)))
                .Where(hit => hit.Added.Count > 0 && hit.Swallower is not null && !DeclaresGetProviderCore(hit.Type))
                .Select(hit => $"{hit.Type.Name} : {hit.Swallower!.Name} adds {string.Join(", ", hit.Added.Select(i => i.Name))}")
                .ToList();

        // The UIA pattern interfaces a peer adds beyond what its base already implements.
        private static IReadOnlyList<Type> AddedProviderInterfaces(Type peer) =>
            peer.GetInterfaces()
                .Except(peer.BaseType?.GetInterfaces() ?? Array.Empty<Type>())
                .Where(contract => contract.Namespace == ProviderNamespace)
                .ToList();

        // The nearest base that overrides GetProviderCore itself. AutomationPeer's own declaration is the DEFAULT
        // (it answers off the interface list), so the walk stops there rather than counting it.
        private static Type? OverridingBase(Type baseType)
        {
            for (Type? type = baseType; type is not null && type != typeof(AutomationPeer); type = type.BaseType)
                if (DeclaresGetProviderCore(type))
                    return type;
            return null;
        }

        private static bool DeclaresGetProviderCore(Type type) =>
            DeclaredOverride(type, GetProviderCoreName, typeof(object), typeof(Type)) is not null;

    }
}
