using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// What the stub host prints in a manual run, where nothing else reads the stub's lists: a line per call, and a
    /// line of its own kind for an operation the stub does not model and for a defect in the stub itself, naming the
    /// operation and never a payload.
    /// </summary>
    [TestFixture]
    public class ConsoleReportingStubTests
    {
        private const string Controller = "ControllerService";
        private const string GetState = "getState";

        [Test]
        public async Task AnUnmodelledOperation_GetsALineOfItsOwn()
        {
            using StringWriter output = new();
            ConsoleReportingStub stub = new(new ControllerStub(), output);

            await stub.HandleAsync(new ControllerStubRequest(Controller, "noSuchOperation", "<payload-secret/>", "session=cookie-secret"),
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(output.ToString(), Does.Contain($"unmodelled {Controller}.noSuchOperation"));
                Assert.That(output.ToString(), Does.Not.Contain("payload-secret").And.Not.Contain("cookie-secret"));
            });
        }

        [Test]
        public void ADefect_GetsALineWithItsExceptionType_AndNotItsMessage()
        {
            ControllerStub inner = new();
            inner.OverrideRaw(Controller, GetState, (_, _) => throw new NotSupportedException("message-secret"));
            using StringWriter output = new();
            ConsoleReportingStub stub = new(inner, output);

            Assert.That(() => stub.HandleAsync(new ControllerStubRequest(Controller, GetState, string.Empty, null), CancellationToken.None),
                Throws.Exception, "the host turns the throw into a fault the SDK sees");

            Assert.Multiple(() =>
            {
                Assert.That(output.ToString(), Does.Contain($"defect {Controller}.{GetState}: NotSupportedException"));
                Assert.That(output.ToString(), Does.Not.Contain("message-secret"));
            });
        }
    }
}
