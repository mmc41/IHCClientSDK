using System;

namespace Ihc
{
    /// <summary>
    /// Common interface for all models containing a binary file.
    /// </summary>
    public interface BinaryFile
    {
        public byte[] Data { get; init; }

        public string Filename { get; init; }
    }

    /// <summary>
    /// Common interface for all models containing a text file.
    /// </summary>
    public interface TextFile
    {
        public string Data { get; init; }

        public string Filename { get; init; }
        public static System.Text.Encoding Encoding { get; }
    }
}