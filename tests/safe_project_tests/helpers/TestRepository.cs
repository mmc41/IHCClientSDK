#nullable enable
using System;
using System.IO;

namespace Ihc.Vis.Tests
{
    /// <summary>Locates repository files needed by source-level test gates.</summary>
    internal static class TestRepository
    {
        /// <summary>The repo root (the directory holding <c>IHCClientSDK.sln</c>), or null outside a checkout.</summary>
        public static string? FindRoot()
        {
            for (DirectoryInfo? dir = new(TestContext.CurrentContext.TestDirectory); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "IHCClientSDK.sln")))
                {
                    return dir.FullName;
                }
            }
            return null;
        }

        /// <summary>The repo root; throws when the test does not run under a checkout.</summary>
        public static string RequireRoot() =>
            FindRoot() ?? throw new InvalidOperationException(
                "repo root (IHCClientSDK.sln) not found above the test directory");
    }
}
