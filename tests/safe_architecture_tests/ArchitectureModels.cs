using System;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

namespace Ihc.Tests
{
    /// <summary>
    /// Compiled assemblies loaded once for the complete architecture-test suite. Keeping the models here avoids
    /// re-reading the same IL when rules are split into focused fixtures.
    /// </summary>
    internal static class ArchitectureModels
    {
        private static readonly Lazy<Architecture> SdkModel = new(() => new ArchLoader()
            .LoadAssemblies(typeof(global::Ihc.IhcSettings).Assembly)
            .Build());

        private static readonly Lazy<Architecture> GuiModel = new(() => new ArchLoader()
            .LoadAssemblies(typeof(global::ihc_openvisual.App).Assembly)
            .Build());

        public static Architecture Sdk => SdkModel.Value;

        public static Architecture Gui => GuiModel.Value;

        public static readonly Lazy<Architecture> ArchitectureTests = new(() =>
            new ArchLoader().LoadAssemblies(typeof(ArchitectureModels).Assembly).Build());
    }
}
