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

        // The two halves of the shared host bootstrap, loaded separately because the whole point of the split
        // is that they differ: one may reach Avalonia, the other may not.
        private static readonly Lazy<Architecture> NeutralBootstrapModel = new(() => new ArchLoader()
            .LoadAssemblies(typeof(global::Ihc.Bootstrap.TelemetryBootstrap).Assembly)
            .Build());

        private static readonly Lazy<Architecture> AppBootstrapModel = new(() => new ArchLoader()
            .LoadAssemblies(typeof(global::Ihc.Bootstrap.AppTelemetryBootstrap).Assembly)
            .Build());

        public static Architecture Sdk => SdkModel.Value;

        public static Architecture Gui => GuiModel.Value;

        /// <summary>The toolkit-neutral bootstrap half, which a console utility references.</summary>
        public static Architecture NeutralBootstrap => NeutralBootstrapModel.Value;

        /// <summary>The Avalonia-only bootstrap half.</summary>
        public static Architecture AppBootstrap => AppBootstrapModel.Value;

        public static readonly Lazy<Architecture> ArchitectureTests = new(() =>
            new ArchLoader().LoadAssemblies(typeof(ArchitectureModels).Assembly).Build());
    }
}
