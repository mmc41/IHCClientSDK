using NUnit.Framework;
using System;
using System.Linq;
using System.IO;
using Ihc;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using FakeItEasy;

namespace Ihc.Tests
{
    /// <summary>
    /// Unit test for metadata handling.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ServiceAPIMetaDataTest
    {
        [Test]
        public void CheckSampleNormalAsyncOperation()
        {
            var service = A.Fake<IResourceInteractionService>();
               
            // Get metadata for all operations
            var operations = ServiceMetadata.GetOperations(service);

            // Find the WaitForResourceValueChanges operation and verify it exist
            const string operationName = nameof(ResourceInteractionService.WaitForResourceValueChanges);
            var waitForChangesOperation = operations.FirstOrDefault(op => op.Name == operationName);
            Assert.That(waitForChangesOperation, Is.Not.Null, operationName+" operation should be found in metadata");

            // Verify return type (should be IReadOnlyList<ResourceValue> unwrapped from Task<IReadOnlyList<ResourceValue>>)
            Assert.That(waitForChangesOperation.ReturnType, Is.EqualTo(typeof(IReadOnlyList<ResourceValue>)),
                "Return type should be IReadOnlyList<ResourceValue>");

            // Verify parameter types (should have one int parameter for timeout_seconds)
            Assert.That(waitForChangesOperation.Parameters.Length, Is.EqualTo(1), "Should have exactly one parameter");
            Assert.That(waitForChangesOperation.Parameters[0].Type, Is.EqualTo(typeof(int)),  "First parameter should be int (timeout_seconds)");

            // Verify operation type (should be AsyncFunction since it returns Task<ResourceValue[]>)
            Assert.That(waitForChangesOperation.Kind, Is.EqualTo(ServiceOperationKind.AsyncFunction),
                "Operation type should be AsyncFunction");
        }

        /// <summary>
        /// Regression test: ServiceMetadata caches operation metadata per service *type* in a static cache.
        /// Each cached entry must be re-bound to the live service instance on retrieval; otherwise Invoke()
        /// targets the (stale) instance that first populated the cache for that type - causing a second
        /// LabAppService/service instance of the same type to call operations on the wrong object.
        /// </summary>
        [Test]
        public async Task GetOperations_RebindsToLiveInstance_NotStaleCachedInstance()
        {
            // First instance of IConfigurationService, configured to report version "AAA".
            var serviceA = A.Fake<IConfigurationService>();
            A.CallTo(() => serviceA.GetSystemInfo()).Returns(Task.FromResult(new SystemInfo { Version = "AAA" }));
            var getInfoA = ServiceMetadata.GetOperations(serviceA)
                .First(op => op.Name == nameof(IConfigurationService.GetSystemInfo));
            var resultA = await (Task<SystemInfo>)getInfoA.Invoke(Array.Empty<object>());
            Assert.That(resultA.Version, Is.EqualTo("AAA"));

            // A second, distinct instance of the SAME service type, configured to report "BBB".
            var serviceB = A.Fake<IConfigurationService>();
            A.CallTo(() => serviceB.GetSystemInfo()).Returns(Task.FromResult(new SystemInfo { Version = "BBB" }));
            var getInfoB = ServiceMetadata.GetOperations(serviceB)
                .First(op => op.Name == nameof(IConfigurationService.GetSystemInfo));
            var resultB = await (Task<SystemInfo>)getInfoB.Invoke(Array.Empty<object>());

            // Before the fix this returned "AAA" (the stale cached instance bound into the cached metadata).
            Assert.That(resultB.Version, Is.EqualTo("BBB"),
                "GetOperations must bind metadata to the live service instance, not the stale cached one");
        }

        /// <summary>
        /// US-A1: CreateSubTypes must emit the element type for a generic collection parameter (like it does for
        /// arrays), so the collection control strategy can find the element FieldMetaData.
        /// </summary>
        [Test]
        public void GetOperations_GenericCollectionParameter_EmitsElementSubType()
        {
            var service = A.Fake<IResourceInteractionService>();

            var getRuntimeValues = ServiceMetadata.GetOperations(service)
                .First(op => op.Name == nameof(IResourceInteractionService.GetRuntimeValues));

            Assert.That(getRuntimeValues.Parameters.Length, Is.EqualTo(1));
            var param = getRuntimeValues.Parameters[0];
            Assert.That(param.Type, Is.EqualTo(typeof(IReadOnlyList<int>)));
            Assert.That(param.SubTypes.Length, Is.EqualTo(1), "Collection parameter should emit one element subtype");
            Assert.That(param.SubTypes[0].Type, Is.EqualTo(typeof(int)), "Element subtype should be int");
        }

        [Test]
        public void CheckAsyncEnumerableOperation()
        {
            // Create a ResourceInteractionService instance
            var service = A.Fake<IResourceInteractionService>();

            // Get metadata for all operations
            var operations = ServiceMetadata.GetOperations(service);

            // Find the GetResourceValueChanges operation and verify it exists
            const string operationName =  nameof(ResourceInteractionService.GetResourceValueChanges);
            var getChangesOperation = operations.FirstOrDefault(op => op.Name == operationName);
            Assert.That(getChangesOperation, Is.Not.Null, operationName+" operation should be found in metadata");

            // Verify operation type (should be AsyncEnumerable since it returns IAsyncEnumerable<ResourceValue>)
            Assert.That(getChangesOperation.Kind, Is.EqualTo(ServiceOperationKind.AsyncEnumerable), "Operation type should be AsyncEnumerable");

            // Verify return type (should be ResourceValue unwrapped from IAsyncEnumerable<ResourceValue>)
            Assert.That(getChangesOperation.ReturnType, Is.EqualTo(typeof(ResourceValue)), "Return type should be ResourceValue (unwrapped from IAsyncEnumerable<ResourceValue>)");
        }

        [Test]
        public void CheckOperationDescriptionsAreLoaded()
        {
            var service = A.Fake<IAuthenticationService>();

            // Get metadata for all operations
            var operations = ServiceMetadata.GetOperations(service);

            // Find the Authenticate operation
            const string operationName = nameof(IAuthenticationService.Authenticate);
            var authenticateOperation = operations.FirstOrDefault(op => op.Name == operationName);
            Assert.That(authenticateOperation, Is.Not.Null, operationName + " operation should be found in metadata");

            // Verify that description is populated
            Assert.That(authenticateOperation.Description, Is.Not.Null, "Description should not be null");
            Assert.That(authenticateOperation.Description, Is.Not.Empty, "Description should not be empty");

            // Verify description contains expected content
            Assert.That(authenticateOperation.Description.ToLower(), Does.Contain("login"),
                "Description should contain 'login' - actual: " + authenticateOperation.Description);
        }

        [Test]
        public void CheckInterenalOperationsRemoved()
        {
            // Create a ResourceInteractionService instance
            var service = A.Fake<IResourceInteractionService>();

            // Get metadata for all operations
            var operations = ServiceMetadata.GetOperations(service);
            var defaultObjectOperations = operations.FirstOrDefault(op => op.Name == nameof(Equals) || op.Name == nameof(GetHashCode) || op.Name == nameof(ToString) || op.Name == "Disponse"|| op.Name == "DisposeAsync" || op.Name == "GetCookieHandler");
            Assert.That(defaultObjectOperations, Is.Null, "Equals/GetHashCode/ToString operations should NOT be found in metadata");
        }

        [Test]
        public void CheckProjectFileIsFileDetection()
        {
            // Create a ControllerService instance
            var service = A.Fake<IControllerService>();

            // Get metadata for all operations
            var operations = ServiceMetadata.GetOperations(service);

            // Find the GetProject operation
            const string operationName = nameof(IControllerService.GetProject);
            var getProjectOperation = operations.FirstOrDefault(op => op.Name == operationName);
            Assert.That(getProjectOperation, Is.Not.Null, operationName + " operation should be found in metadata");

            // Verify return type is ProjectFile
            Assert.That(getProjectOperation.ReturnType, Is.EqualTo(typeof(ProjectFile)),
                "Return type should be ProjectFile");

            // Create FieldMetaData for ProjectFile type
            var projectFileType = typeof(ProjectFile);
            var projectFileMetaData = new FieldMetaData(
                name: "ProjectFile",
                type: projectFileType,
                subtypes: [],
                description: "Project file",
                attributeProvider: null);

            // Test IsFile - should be true because ProjectFile implements TextFile interface
            Assert.That(projectFileMetaData.IsFile, Is.True,
                "ProjectFile type should be identified as a file field because it implements TextFile");

            // Verify BackupFile is also detected as file
            var backupFileMetaData = new FieldMetaData(
                name: "BackupFile",
                type: typeof(BackupFile),
                subtypes: [],
                description: "Backup file",
                attributeProvider: null);

            Assert.That(backupFileMetaData.IsFile, Is.True,
                "BackupFile type should be identified as a file field because it implements BinaryFile");
        }

    }

}