namespace Ihc
{
    /// <summary>
    /// Base interface for all IHC services.
    /// </summary>
    public interface IIHCApiService
    {
        /// <summary>
        /// The IhcSettings used by this service.
        /// </summary>
        public IhcSettings IhcSettings { get; }
    }
}