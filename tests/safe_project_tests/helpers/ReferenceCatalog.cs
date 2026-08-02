#nullable enable
using System;

using Ihc.Vis.Catalog;
using Ihc.Vis.Model;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Opens the reference catalog configured through <see cref="IhcSettings.IhcVisualInstallDir"/> and provides
    /// structural comparison diagnostics for the disk-backed catalog differential tests.
    /// </summary>
    internal static class ReferenceCatalog
    {
        /// <summary>
        /// Returns the configured reference-catalog directory, or skips the comparison when the option is unset.
        /// A configured but invalid directory is returned so the comparison runs and reports the configuration error.
        /// </summary>
        public static string DirectoryOrIgnore(string comparisonDescription)
        {
            string dir = TestSetup.Settings.IhcVisualInstallDir;
            if (string.IsNullOrWhiteSpace(dir))
            {
                Assert.Ignore(
                    $"No reference catalog configured through {nameof(IhcSettings.IhcVisualInstallDir)}; " +
                    $"skipping {comparisonDescription}.");
            }
            return dir;
        }

        /// <summary>Opens the configured reference catalog, or skips when the option is unset.</summary>
        public static ICatalog OpenOrIgnore(string comparisonDescription) =>
            CatalogDiscovery.FromInstallDir(DirectoryOrIgnore(comparisonDescription));

        /// <summary>Fails with a normalized structural diff when the generated and reference elements differ.</summary>
        public static void AssertStructural(
            string mismatchDescription,
            ProjectElement expected,
            ProjectElement actual)
        {
            if (!expected.Equals(actual))
            {
                Assert.Fail(mismatchDescription + "\n"
                            + "EXPECTED (reference catalog):\n" + DefinitionNormalizer.Dump(expected)
                            + "\nACTUAL (generated catalog):\n" + DefinitionNormalizer.Dump(actual));
            }
        }
    }
}
