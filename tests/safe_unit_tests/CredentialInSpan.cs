using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// What a login span must say about the password, asserted the same way wherever a service takes one.
    ///
    /// Two services accept a credential directly - <see cref="AuthenticationService"/> and
    /// <see cref="OpenAPIService"/> - and each puts it on a span of its own. The contract they answer to is
    /// ONE contract: <c>LogSensitiveData</c> off redacts, <c>LogSensitiveData</c> on publishes, and no other
    /// setting decides either. It lives here so that tightening it tightens it for both, rather than for
    /// whichever fixture is remembered.
    /// </summary>
    internal static class CredentialInSpan
    {
        /// <summary>The password is absent and visibly redacted; the username, which is not a secret, is not.</summary>
        internal static void AssertRedacted(string tags, string password, string userName)
        {
            Assert.Multiple(() =>
            {
                Assert.That(tags, Does.Not.Contain(password));
                Assert.That(tags, Does.Contain(UserConstants.REDACTED_PASSWORD),
                    "the parameter stays observable in the trace - redacted, not omitted");
                Assert.That(tags, Does.Contain(userName), "the username is not the secret");
            });
        }

        /// <summary>The password is published, which is the whole of what the opt-in buys.</summary>
        internal static void AssertPublished(string tags, string password) =>
            Assert.That(tags, Does.Contain(password),
                "LogSensitiveData is the opt-in that permits a cleartext credential in traces");
    }
}
