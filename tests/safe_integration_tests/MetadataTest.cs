using NUnit.Framework;
using System;
using System.Linq;
using System.IO;
using Ihc;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

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
            // Create a ResourceInteractionService instance
            var service = new ResourceInteractionService(new AuthenticationService(Setup.logger, Setup.endpoint));

            // Get metadata for all operations
            var operations = service.GetOperations();

            // Find the WaitForResourceValueChanges operation and verify it exist
            var waitForChangesOperation = operations.FirstOrDefault(op => op.Name == "WaitForResourceValueChanges");
            Assert.That(waitForChangesOperation, Is.Not.Null, "WaitForResourceValueChanges operation should be found in metadata");

            // Verify return type (should be ResourceValue[] unwrapped from Task<ResourceValue[]>)
            Assert.That(waitForChangesOperation.ReturnType, Is.EqualTo(typeof(ResourceValue[])),
                "Return type should be ResourceValue[]");

            // Verify parameter types (should have one int parameter for timeout_seconds)
            Assert.That(waitForChangesOperation.ParameterTypes.Length, Is.EqualTo(1), "Should have exactly one parameter");
            Assert.That(waitForChangesOperation.ParameterTypes[0], Is.EqualTo(typeof(int)),  "First parameter should be int (timeout_seconds)");

            // Verify operation type (should be AsyncFunction since it returns Task<ResourceValue[]>)
            Assert.That(waitForChangesOperation.Kind, Is.EqualTo(ServiceOperationKind.AsyncFunction),
                "Operation type should be AsyncFunction");
        }

        [Test]
        public void CheckAsyncEnumerableOperation()
        {
            // Create a ResourceInteractionService instance
            var service = new ResourceInteractionService(new AuthenticationService(Setup.logger, Setup.endpoint));

            // Get metadata for all operations
            var operations = service.GetOperations();

            // Find the GetResourceValueChanges operation and verify it exists
            var getChangesOperation = operations.FirstOrDefault(op => op.Name == "GetResourceValueChanges");
            Assert.That(getChangesOperation, Is.Not.Null, "GetResourceValueChanges operation should be found in metadata");

            // Verify operation type (should be AsyncEnumerable since it returns IAsyncEnumerable<ResourceValue>)
            Assert.That(getChangesOperation.Kind, Is.EqualTo(ServiceOperationKind.AsyncEnumerable), "Operation type should be AsyncEnumerable");

            // Verify return type (should be ResourceValue unwrapped from IAsyncEnumerable<ResourceValue>)
            Assert.That(getChangesOperation.ReturnType, Is.EqualTo(typeof(ResourceValue)), "Return type should be ResourceValue (unwrapped from IAsyncEnumerable<ResourceValue>)");
        }
    }

}