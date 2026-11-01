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
        public static IhcSettings? settings { get; private set; }

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

    /// <summary>
    /// This suite's own proof that its tests run latched; see <see cref="Ihc.Tests.Shared.ControllerLatchProbe"/>.
    /// A fixture of its own rather than a member of <see cref="Setup"/>, because the reach guard names an admitted
    /// site by type and member, and the Lab suite declares an <c>Ihc.Tests.Setup</c> too.
    /// </summary>
    [SetUpFixture]
    public class UnitRunLatchProbe
    {
        [OneTimeSetUp]
        public void ProveTheRunIsLatched() =>
            Ihc.Tests.Shared.ControllerLatchProbe.RequireRefused(
                new AuthenticationService(new IhcSettings { Endpoint = Ihc.Tests.Shared.ControllerLatchProbe.Endpoint }));
    }
}