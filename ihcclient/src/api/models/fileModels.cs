using System;

namespace Ihc
{
    /// <summary>
    /// Common interface for all models containing a binary file.
    /// Note: All model implementations must also have a copy-constructor with this type as argument for Lab tool to work.
    /// </summary>
    public interface BinaryFile
    {
        public byte[] Data { get; }

        /// <summary>
        /// Name of the file, or null when the SDK has none - the segment models, whose names the SDK
        /// does not map from the reply, are the case in point, and the Lab names those after the operation
        /// instead.
        /// </summary>
        public string? Filename { get; }
    }

    /// <summary>
    /// Common interface for all models containing a text file.
    /// Note: All model implementations must also have a copy-constructor with this type as arg for Lab tool to work.
    /// </summary>
    public interface TextFile
    {
        public string Data { get; }

        /// <summary>
        /// Name of the file, or null when the reply carries none. The WSDL requires the controller's
        /// WSFile to carry a filename, but a reply that omits it is still read, so the Lab falls back to
        /// an operation-derived name rather than trusting this.
        /// </summary>
        public string? Filename { get; }
        public static System.Text.Encoding? Encoding { get; }
    }
}