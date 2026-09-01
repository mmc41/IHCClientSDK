using System;
using System.IO;

using NUnit.Framework;

namespace Ihc.Tests.Shared
{
    /// <summary>
    /// Locates the SOURCE checkout, for the tests that must reach it rather than the build output.
    /// </summary>
    /// <remarks>
    /// Almost nothing should want this: a fixture belongs beside the test binary, which is what
    /// <c>tests/TestData.props</c> is for, and a test that walks into the checkout hardcodes the repository
    /// layout in C#. The exceptions are the things that have no build-output form — a source-level gate reading
    /// the product's own <c>.cs</c> files, a checked-in document a test reads as the SUBJECT under test, and the
    /// driver script the end-to-end suite invokes.
    /// <para>Here rather than per suite because three of them wanted it: two had grown their own copy of this
    /// walk, and jscpd named the pair the moment a third appeared. A copy is how two of them come to disagree
    /// about where the root is.</para>
    /// </remarks>
    internal static class TestRepository
    {
        /// <summary>
        /// The repo root — the directory holding <c>IHCClientSDK.sln</c>, found by walking up from the test
        /// assembly.
        /// </summary>
        /// <exception cref="InvalidOperationException">The test does not run under a checkout.</exception>
        public static string RequireRoot()
        {
            for (DirectoryInfo? dir = new(TestContext.CurrentContext.TestDirectory); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "IHCClientSDK.sln")))
                {
                    return dir.FullName;
                }
            }

            throw new InvalidOperationException("repo root (IHCClientSDK.sln) not found above the test directory");
        }
    }
}
