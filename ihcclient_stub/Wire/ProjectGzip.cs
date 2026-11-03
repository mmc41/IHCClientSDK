using System.IO.Compression;

namespace Ihc.Stubbing
{
    /// <summary>
    /// A project as the controller carries it: its ISO-8859-1 bytes as one gzip stream, whole in a one-call transfer
    /// and cut into slices of that same stream in a segmented download.
    /// </summary>
    internal static class ProjectGzip
    {
        /// <summary>The gzip stream of <paramref name="project"/>'s text.</summary>
        internal static byte[] Of(ProjectFile project) => Compress(ProjectFile.Encoding.GetBytes(project.Data));

        /// <summary>The project named <paramref name="filename"/> whose text <paramref name="gzip"/> carries.</summary>
        /// <exception cref="InvalidDataException"><paramref name="gzip"/> is no gzip stream.</exception>
        internal static ProjectFile ToProject(string filename, byte[] gzip) =>
            new(filename, ProjectFile.Encoding.GetString(Decompress(gzip)));

        internal static byte[] Compress(byte[] plain)
        {
            using var compressed = new MemoryStream();
            using (var gzip = new GZipStream(compressed, CompressionMode.Compress))
            {
                gzip.Write(plain);
            }
            return compressed.ToArray();
        }

        internal static byte[] Decompress(byte[] compressed)
        {
            using var gzip = new GZipStream(new MemoryStream(compressed), CompressionMode.Decompress);
            using var plain = new MemoryStream();
            gzip.CopyTo(plain);
            return plain.ToArray();
        }
    }
}
