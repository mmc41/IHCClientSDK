using Ihc.Stubbing;
using Ihc.Tests.Shared;
using NUnit.Framework;

// Declared OUTSIDE any namespace, and that is the whole point. NUnit applies a [SetUpFixture] to the namespace it is
// declared in, and to the whole assembly only when it is declared in none. The suites that link this file keep their
// tests in different namespaces, so a fixture in the shared helpers' namespace would latch the tests of one suite and
// leave every test in the others green and unlatched.

/// <summary>
/// Holds a controller-stub registration for the whole test run, so the SDK's latch refuses every real endpoint
/// before any socket exists: no test in a suite that links this can reach a controller, including through a
/// service a product factory builds where the reach guard cannot see it. A test's explicit transport still works,
/// because it comes before the latch.
/// </summary>
[SetUpFixture]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1050:Declare types in namespaces",
    Justification = "Only a SetUpFixture in no namespace applies to every test in the assembly.")]
public sealed class ControllerStubLatchFixture
{
    private ControllerStubRegistration? latch;

    [OneTimeSetUp]
    public void HoldTheLatch() => latch = ControllerStubs.Register(new FailAllControllerStub());

    [OneTimeTearDown]
    public void ReleaseTheLatch() => latch?.Dispose();
}
