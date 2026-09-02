namespace Ihc
{
    /// <summary>
    /// High-level interface for LK / Schneider production-test operations.
    /// INTERNAL / potentially DANGEROUS (manufacturing use) - treat like <see cref="IInternalTestService"/>.
    /// The controller WSDL currently defines no operations, so this is a placeholder that establishes the
    /// wrapper for future use. When operations are added, dangerous calls must be gated on
    /// <see cref="IhcSettings.AllowDangerousInternTestCalls"/>.
    /// </summary>
    public interface IProductionTestService : IIHCApiService
    {
    }

    /// <summary>
    /// Production-test service wrapper. See <see cref="IProductionTestService"/>.
    /// Currently exposes no operations (the controller WSDL defines none).
    /// </summary>
    public class ProductionTestService : ServiceBase, IProductionTestService
    {
        /// <summary>
        /// Create a ProductionTestService instance for access to the IHC production-test API.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public ProductionTestService(IAuthenticationService authService)
            : base(SettingsOf(authService))
        {
            // No operations yet. When the controller WSDL gains operations, add a private nested
            //   SoapImpl : ServiceBaseImpl, Ihc.Soap.Productiontest.ProductionTestService
            // constructed with base(authService.GetCookieHandler(), settings, "ProductionTestService"),
            // and gate any dangerous operations on settings.AllowDangerousInternTestCalls.
        }
    }
}
