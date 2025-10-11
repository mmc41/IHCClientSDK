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
            var service = new ResourceInteractionService(new AuthenticationService(Setup.logger, Setup.settings));

            // Get metadata for all operations
            var operations = service.GetOperations();

            // Find the WaitForResourceValueChanges operation and verify it exist
            const string operationName = nameof(ResourceInteractionService.WaitForResourceValueChanges);
            var waitForChangesOperation = operations.FirstOrDefault(op => op.Name == operationName);
            Assert.That(waitForChangesOperation, Is.Not.Null, operationName+" operation should be found in metadata");

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
            var service = new ResourceInteractionService(new AuthenticationService(Setup.logger, Setup.settings));

            // Get metadata for all operations
            var operations = service.GetOperations();

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
            // Create an AuthenticationService instance
            var service = new AuthenticationService(Setup.logger, Setup.settings);

            // Get metadata for all operations
            var operations = service.GetOperations();

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
    }

}