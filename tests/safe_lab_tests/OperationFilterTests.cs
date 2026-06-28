using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FakeItEasy;
using NUnit.Framework;
using Ihc;
using Ihc.App;
using IhcLab.ParameterControls;

namespace Ihc.Tests
{
    /// <summary>
    /// Tests for <see cref="OperationFilterConfiguration"/>.
    ///
    /// The GUI only lists operations it can fully two-way sync: it must be able to build an editable control
    /// for every parameter. These tests guard that contract - in particular that operations whose parameters
    /// are generic collection types (e.g. <c>IReadOnlyList&lt;int&gt;</c>) are filtered out, because no
    /// <see cref="ParameterControlRegistry"/> strategy can render them and selecting such an operation would
    /// otherwise throw "No strategy found".
    /// </summary>
    [TestFixture]
    public class OperationFilterTests
    {
        /// <summary>
        /// Recursively determines whether the registry can build a control for a field and all of its
        /// sub-fields (complex types build sub-controls recursively via the registry).
        /// </summary>
        private static bool IsRenderable(ParameterControlRegistry registry, FieldMetaData field)
        {
            if (!registry.CanHandle(field))
                return false;

            // Leaf-rendered types (file pickers, the ResourceValue union editor) build their own internal
            // controls and are not decomposed via the registry, so do not recurse into their sub-fields.
            if (field.IsFile || field.Type == typeof(ResourceValue))
                return true;

            return field.SubTypes.All(sub => IsRenderable(registry, sub));
        }

        /// <summary>
        /// The full set of IHC service fakes - one per service the Lab exposes - used by the cross-service guards.
        /// Reflection-based metadata reads the service interface, so a fake yields the same operation shapes as the
        /// real service.
        /// </summary>
        private static IIHCApiService[] AllServiceFakes() => new IIHCApiService[]
        {
            A.Fake<IAuthenticationService>(), A.Fake<IControllerService>(), A.Fake<IResourceInteractionService>(),
            A.Fake<IConfigurationService>(), A.Fake<IOpenAPIService>(), A.Fake<INotificationManagerService>(),
            A.Fake<IMessageControlLogService>(), A.Fake<IModuleService>(), A.Fake<ITimeManagerService>(),
            A.Fake<IUserManagerService>(), A.Fake<IAirlinkManagementService>(), A.Fake<ISmsModemService>(),
            A.Fake<IInternalTestService>()
        };

        /// <summary>The IHC service interface name behind a fake proxy, for readable warning messages.</summary>
        private static string ServiceName(IIHCApiService service)
            => service.GetType().GetInterfaces()
                .Where(i => i != typeof(IIHCApiService) && typeof(IIHCApiService).IsAssignableFrom(i))
                .Select(i => i.Name)
                .FirstOrDefault() ?? service.GetType().Name;

        /// <summary>
        /// Describes why a field is un-renderable, walking the same registry path the filter uses: a field no
        /// strategy can handle is the culprit; otherwise recurse into the first un-renderable sub-field the
        /// strategy decomposes into. Used only to make the warning message actionable.
        /// </summary>
        private static string FirstUnsupportedReason(FieldMetaData field)
        {
            var strategy = ParameterControlRegistry.Instance.TryGetStrategy(field);
            if (strategy == null)
                return $"no control strategy for '{field.Type.Name}' (likely a complex/collection type left with no sub-fields by the one-level metadata limit)";

            foreach (var sub in strategy.GetRenderedSubFields(field))
            {
                if (OperationFilterConfiguration.ContainsUnsupportedType(sub))
                {
                    string subName = string.IsNullOrEmpty(sub.Name) ? "<element>" : sub.Name;
                    return $"sub-field '{subName}' ({sub.Type.Name}): {FirstUnsupportedReason(sub)}";
                }
            }

            return "unknown";
        }

        [Test]
        public void DefaultFilter_ExposesOperationWithGenericCollectionParameter()
        {
            // US-A1: DisableInitialValueNotifactions(IReadOnlyList<int>) and the other 8 IReadOnlyList<int>
            // operations are now rendered by the generalized ArrayParameterStrategy, so the filter must expose them.
            var fake = A.Fake<IResourceInteractionService>();
            var filter = OperationFilterConfiguration.CreateDefaultFilter();

            var operation = ServiceMetadata.GetOperations(fake)
                .Single(o => o.Name == nameof(IResourceInteractionService.DisableInitialValueNotifactions));

            Assert.That(filter(operation), Is.True,
                "Operations whose parameter is a generic collection of a supported element (IReadOnlyList<int>) " +
                "must be exposed now that the collection control strategy renders them.");
        }

        [Test]
        public void DefaultFilter_ExposesStreamingOperation()
        {
            // US-B1: the AsyncEnumerable GetResourceValueChanges is now exposed (streamed via Start/Stop); its
            // CancellationToken parameter is harness-injected and skipped by the filter.
            var fake = A.Fake<IResourceInteractionService>();
            var filter = OperationFilterConfiguration.CreateDefaultFilter();

            var op = ServiceMetadata.GetOperations(fake)
                .Single(o => o.Name == nameof(IResourceInteractionService.GetResourceValueChanges));

            Assert.That(filter(op), Is.True,
                "AsyncEnumerable GetResourceValueChanges must be exposed now that streaming is supported.");
        }

        [Test]
        public void DefaultFilter_ExposesResourceValueOperations()
        {
            // US-A3: SetResourceValue(ResourceValue) and SetResourceValues(IReadOnlyList<ResourceValue>) are now
            // rendered by the ResourceValue union editor (+ collection strategy for the list), so both are exposed.
            var fake = A.Fake<IResourceInteractionService>();
            var filter = OperationFilterConfiguration.CreateDefaultFilter();
            var operations = ServiceMetadata.GetOperations(fake).ToList();

            var setOne = operations.Single(o => o.Name == nameof(IResourceInteractionService.SetResourceValue));
            var setMany = operations.Single(o => o.Name == nameof(IResourceInteractionService.SetResourceValues));

            Assert.That(filter(setOne), Is.True, "SetResourceValue(ResourceValue) must be exposed once the union editor lands.");
            Assert.That(filter(setMany), Is.True, "SetResourceValues(IReadOnlyList<ResourceValue>) must be exposed once the union editor lands.");
        }

        [Test]
        public void DefaultFilter_ExposesStoreSceneProjectOperations()
        {
            // US-A6: StoreSceneProject(SceneProject) and StoreSceneProjectSegment(SceneProject, bool, bool) were
            // filtered because SceneProject carries a byte[] Data. Now that SceneProject implements BinaryFile -
            // rendered wholesale as a file picker by FileParameterStrategy - the filter must expose them.
            var fake = A.Fake<IModuleService>();
            var filter = OperationFilterConfiguration.CreateDefaultFilter();
            var operations = ServiceMetadata.GetOperations(fake).ToList();

            var storeSceneProject = operations.Single(o => o.Name == nameof(IModuleService.StoreSceneProject));
            var storeSceneProjectSegment = operations.Single(o => o.Name == nameof(IModuleService.StoreSceneProjectSegment));

            Assert.That(filter(storeSceneProject), Is.True,
                "StoreSceneProject must be exposed once SceneProject is a BinaryFile (file-picker leaf).");
            Assert.That(filter(storeSceneProjectSegment), Is.True,
                "StoreSceneProjectSegment must be exposed once SceneProject is a BinaryFile (file-picker leaf).");
        }

        [Test]
        public void DefaultFilter_EveryExposedOperation_AcrossAllServices_IsRenderable()
        {
            // US-A7 MUST: every operation the filter exposes (for ANY service) must be fully renderable by the
            // registry, so selecting it never throws "No strategy found" at control-creation time.
            var filter = OperationFilterConfiguration.CreateDefaultFilter();
            var registry = ParameterControlRegistry.Instance;

            foreach (var service in AllServiceFakes())
            {
                var exposed = ServiceMetadata.GetOperations(service).Where(filter);
                foreach (var operation in exposed)
                {
                    foreach (var parameter in operation.Parameters)
                    {
                        // CancellationToken is harness-injected (no control built), so it is not required to render.
                        if (parameter.Type == typeof(CancellationToken))
                            continue;

                        Assert.That(IsRenderable(registry, parameter), Is.True,
                            $"{service.GetType().Name}: operation '{operation.Name}' exposes un-renderable parameter " +
                            $"'{parameter.Name}' of type '{parameter.Type}' - it would crash at selection.");
                    }
                }
            }
        }

        [Test]
        public void DefaultFilter_HidesNoOperation_AcrossAllServices_WarnOnly()
        {
            // Guard (WARNING, not failure): hiding an operation the GUI cannot two-way-sync is a deliberate
            // fail-safe, so it must not fail the build. But today every SDK operation across all services is
            // renderable, so nothing should be hidden. If a new or changed SDK parameter shape (e.g. a nested
            // complex record, a collection-of-complex, or a complex record gaining a collection property) starts
            // tripping the one-level renderability limit, its operation silently disappears from the Lab GUI.
            // This surfaces that as an NUnit warning so it is noticed - and a control strategy added or the
            // metadata widened - rather than going unseen.
            var filter = OperationFilterConfiguration.CreateDefaultFilter();

            var hidden = new List<string>();
            foreach (var service in AllServiceFakes())
            {
                foreach (var op in ServiceMetadata.GetOperations(service).Where(o => !filter(o)))
                {
                    var culprits = op.Parameters
                        .Where(p => p.Type != typeof(CancellationToken) && OperationFilterConfiguration.ContainsUnsupportedType(p))
                        .Select(p => $"{p.Name}:{p.Type.Name} ({FirstUnsupportedReason(p)})");
                    hidden.Add($"  {ServiceName(service)}.{op.Name} -> {string.Join("; ", culprits)}");
                }
            }

            if (hidden.Count > 0)
            {
                Assert.Warn(
                    $"{hidden.Count} SDK operation(s) are now HIDDEN by the GUI operation filter. Hiding is a " +
                    $"deliberate fail-safe (the build is intentionally not failed), but it means these operations " +
                    $"vanished from the Lab GUI - add a control strategy or widen the metadata so they render again:\n" +
                    string.Join("\n", hidden));
            }
        }

        [Test]
        public void DefaultFilter_EveryExposedResourceInteractionParameter_IsRenderableByRegistry()
        {
            // Stronger invariant: no operation surfaced by the default filter may have a parameter the GUI
            // cannot build a control for. Before the fix, the IReadOnlyList<int>/IReadOnlyList<ResourceValue>
            // operations slipped through and would throw "No strategy found" on selection.
            var fake = A.Fake<IResourceInteractionService>();
            var filter = OperationFilterConfiguration.CreateDefaultFilter();
            var registry = ParameterControlRegistry.Instance;

            var exposed = ServiceMetadata.GetOperations(fake).Where(filter);

            foreach (var operation in exposed)
            {
                foreach (var parameter in operation.Parameters)
                {
                    // CancellationToken is harness-injected (no control built), so it is not required to render.
                    if (parameter.Type == typeof(CancellationToken))
                        continue;

                    Assert.That(IsRenderable(registry, parameter), Is.True,
                        $"Operation '{operation.Name}' exposes parameter '{parameter.Name}' of type " +
                        $"'{parameter.Type}' that no control strategy can render.");
                }
            }
        }
    }
}
