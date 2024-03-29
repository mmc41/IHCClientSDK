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
    /// System tests against live IHC system. Requires use of user name/password and test input/outputs specified in configuration file.
    /// </summary>
    [NonParallelizable]
    public class ResourceTest
    {   
        private AuthenticationService authService;
        private ResourceInteractionService resourceInteractionService;

        [SetUp]
        public async Task SetupMethod()
        {
            authService = new AuthenticationService(Setup.logger, Setup.endpoint);
            resourceInteractionService = new ResourceInteractionService(authService);
         
            await authService.Authenticate(Setup.userName, Setup.password, Setup.application);
        }

        [TearDown]
        public async Task BaseTearDown() { 
            await authService.Disconnect();
        }

        [Test]
        public async Task ToggleOutputTest()
        {
          var orgOutput = await resourceInteractionService.GetRuntimeValue(Setup.boolOutput1);
          Assert.IsTrue(orgOutput.IsValueRuntime);
          Assert.That(TypeStrings.DatalineOutput, Is.EqualTo(orgOutput.TypeString));
          Assert.IsNotNull(orgOutput.Value.BoolValue);
          Assert.That(orgOutput.ResourceID, Is.EqualTo(Setup.boolOutput1));

          var toggledOutput = await resourceInteractionService.SetResourceValue(ResourceValue.ToogleBool(orgOutput));
          Assert.IsTrue(toggledOutput.Value);

          var newOutput = await resourceInteractionService.GetRuntimeValue(Setup.boolOutput1);
          Assert.IsTrue(orgOutput.IsValueRuntime);
          Assert.That(TypeStrings.DatalineOutput, Is.EqualTo(orgOutput.TypeString));
          Assert.IsNotNull(newOutput.Value.BoolValue);
          Assert.That(newOutput.ResourceID, Is.EqualTo(Setup.boolOutput1));

          Assert.That(newOutput.Value.BoolValue, Is.Not.EqualTo(orgOutput.Value.BoolValue));
        }
 

        [Test]
        public async Task WatchChangingInputsTest()
        {
           CancellationTokenSource cts = new CancellationTokenSource();
           cts.CancelAfter(5000); // Allow max 5s for this test to complete.

           // Keep an eye on our output.
           var resourceChanges = resourceInteractionService.GetResourceValueChanges(new int[] {
                                    Setup.boolOutput1
                                 }, cts.Token);

           int resourceValueChanges = 0; // Track changes
           await foreach (ResourceValue r in resourceChanges) {
                Assert.IsTrue(r.IsValueRuntime);
                Assert.IsNotNull(r.Value.BoolValue);
                Assert.That(r.ResourceID, Is.EqualTo(Setup.boolOutput1));
                // Note: No check for TypeString as it is empty on my controller (IHC bug).

                if (resourceValueChanges++==0) { // Initial value
                    // 1st time we get an output change it is just the initial value
                    // we than introduce a new value expecting to get a new change. 
                    await resourceInteractionService.SetResourceValue(ResourceValue.ToogleBool(r));                    
                } else { // Change.
                    cts.Cancel(); // We can stop now.
                }
           }

           // Check that we at least got inital value + change.
           Assert.GreaterOrEqual(resourceValueChanges, 2);
        }
    }
}