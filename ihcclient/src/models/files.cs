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

        public string Filename { get; }
    }

    /// <summary>
    /// Common interface for all models containing a text file.
    /// Note: All model implementations must also have a copy-constructor with this type as arg for Lab tool to work.
    /// </summary>
    public interface TextFile
    {
        public string Data { get; }

        public string Filename { get; }
        public static System.Text.Encoding Encoding { get; }
    }
}