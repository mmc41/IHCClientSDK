using System.IO;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Ihc;
using System.Reflection;

namespace Ihc.Tests
{
    /**
    * Setup globals for configuration that is run before any tests and provide tests with needed config data.
    **/
    [SetUpFixture]
    public class Setup
  {
        public static IhcSettings? settings;

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            settings = new IhcSettings()
            {
                Endpoint = SpecialEndpoints.MockedPrefix,
                UserName = "test",
                Password = "test",
                Application = Application.openapi,
                LogSensitiveData = true,
                AsyncContinueOnCapturedContext = false
            };
        }
    }
}