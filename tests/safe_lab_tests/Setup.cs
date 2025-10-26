using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Ihc;
using System.Reflection;
using IhcLab;

namespace Ihc.Tests
{
    /**
    * Setup globals for configuration that is run before any tests and provide tests with needed config data.
    **/
    [SetUpFixture]
    public class Setup
    {
        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            var settings = new IhcSettings()
            {
                Endpoint = SpecialEndpoints.MockedPrefix,
                UserName = "test",
                Password = "test",
                Application = "administrator",
                LogSensitiveData = true,
                AsyncContinueOnCapturedContext = false
            };

            IhcLab.Program.config = new Configuration(settings);
        }
    }
}