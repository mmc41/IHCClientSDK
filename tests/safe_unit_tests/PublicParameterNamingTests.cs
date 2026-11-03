using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// Parameter names are part of the public contract: a caller passing named arguments binds to them, and Lab
    /// labels its argument editors with them. Where two services mirror one operation they take the same names, and
    /// a name says what the value is, not the wire part it travels in.
    /// </summary>
    [TestFixture]
    public class PublicParameterNamingTests
    {
        private static string[] ParameterNames(Type service, string method) =>
            [.. service.GetMethod(method, BindingFlags.Public | BindingFlags.Instance)!.GetParameters().Select(parameter => parameter.Name!)];

        /// <summary>The OpenAPI segment download names its revision arguments as the controller service does, after
        /// the <see cref="ProjectInfo"/> members they are read from.</summary>
        [TestCase(typeof(IControllerService))]
        [TestCase(typeof(IOpenAPIService))]
        [TestCase(typeof(ControllerService))]
        [TestCase(typeof(OpenAPIService))]
        public void GetIHCProjectSegment_NamesItsRevisionsAfterProjectInfo(Type service)
        {
            Assert.That(ParameterNames(service, nameof(IControllerService.GetIHCProjectSegment)),
                Is.EqualTo(new[] { "index", "projectMajorRevision", "projectMinorRevision" }));
        }

        /// <summary>
        /// Every long poll names the time the controller may hold it <c>holdSeconds</c>: it is the hold of each call,
        /// not a pause between calls nor a client time-out.
        /// </summary>
        [TestCase(typeof(IResourceInteractionService), typeof(ResourceInteractionService), nameof(IResourceInteractionService.WaitForResourceValueChanges))]
        [TestCase(typeof(IResourceInteractionService), typeof(ResourceInteractionService), nameof(IResourceInteractionService.GetResourceValueChanges))]
        [TestCase(typeof(IOpenAPIService), typeof(OpenAPIService), nameof(IOpenAPIService.GetResourceValueChanges))]
        [TestCase(typeof(IOpenAPIService), typeof(OpenAPIService), nameof(IOpenAPIService.WaitForEvents))]
        [TestCase(typeof(IAirlinkManagementService), typeof(AirlinkManagementService), nameof(IAirlinkManagementService.WaitForDeviceDetected))]
        [TestCase(typeof(IAirlinkManagementService), typeof(AirlinkManagementService), nameof(IAirlinkManagementService.WaitForDeviceTestResult))]
        [TestCase(typeof(ILedDimmerManagementService), typeof(LedDimmerManagementService), nameof(ILedDimmerManagementService.WaitForDeviceDetected))]
        [TestCase(typeof(IControllerService), typeof(ControllerService), nameof(IControllerService.WaitForControllerStateChange))]
        public void ALongPoll_NamesItsHoldSeconds(Type contract, Type implementation, string method)
        {
            Assert.Multiple(() =>
            {
                foreach (Type service in new[] { contract, implementation })
                {
                    Assert.That(ParameterNames(service, method).Where(name => name is not "resourceIds" and not "cancellationToken" and not "waitState"),
                        Is.EqualTo(new[] { "holdSeconds" }), service.Name);
                }
            });
        }

        /// <summary>The resource-interaction service spells a resource's id one way: <c>resourceId</c>, and
        /// <c>resourceIds</c> for a list, never after the value read nor with a capital D.</summary>
        [TestCase(nameof(IResourceInteractionService.GetInitialValue), "resourceId")]
        [TestCase(nameof(IResourceInteractionService.GetRuntimeValue), "resourceId")]
        [TestCase(nameof(IResourceInteractionService.GetResourceType), "resourceId")]
        [TestCase(nameof(IResourceInteractionService.GetLoggedData), "resourceId")]
        [TestCase(nameof(IResourceInteractionService.GetInitialValues), "resourceIds")]
        [TestCase(nameof(IResourceInteractionService.GetRuntimeValues), "resourceIds")]
        [TestCase(nameof(IResourceInteractionService.EnableInitialValueNotifications), "resourceIds")]
        [TestCase(nameof(IResourceInteractionService.EnableRuntimeValueNotifications), "resourceIds")]
        [TestCase(nameof(IResourceInteractionService.GetSceneGroupResourceIdAndPositions), "sceneGroupResourceId")]
        [TestCase(nameof(IResourceInteractionService.GetScenePositionsForSceneValueResource), "sceneMemberResourceId")]
        public void AResourceInteractionRead_NamesItsResourceIdOneWay(string method, string expected)
        {
            Assert.Multiple(() =>
            {
                Assert.That(ParameterNames(typeof(IResourceInteractionService), method), Is.EqualTo(new[] { expected }));
                Assert.That(ParameterNames(typeof(ResourceInteractionService), method), Is.EqualTo(new[] { expected }));
            });
        }
    }
}
