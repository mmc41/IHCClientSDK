using System.Linq;
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

            return field.SubTypes.All(sub => IsRenderable(registry, sub));
        }

        [Test]
        public void DefaultFilter_ExcludesOperationWithGenericCollectionParameter()
        {
            // DisableInitialValueNotifactions(IReadOnlyList<int> resourceIds) is a normal async operation
            // (Task<bool>), so it is not excluded as a stream - but its IReadOnlyList<int> parameter has no
            // control strategy. The default filter must exclude it.
            var fake = A.Fake<IResourceInteractionService>();
            var filter = OperationFilterConfiguration.CreateDefaultFilter();

            var operation = ServiceMetadata.GetOperations(fake)
                .Single(o => o.Name == nameof(IResourceInteractionService.DisableInitialValueNotifactions));

            Assert.That(filter(operation), Is.False,
                "Operations whose parameters are generic collections (IReadOnlyList<int>) must be filtered out " +
                "because the GUI has no control strategy able to render them.");
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
                    Assert.That(IsRenderable(registry, parameter), Is.True,
                        $"Operation '{operation.Name}' exposes parameter '{parameter.Name}' of type " +
                        $"'{parameter.Type}' that no control strategy can render.");
                }
            }
        }
    }
}
