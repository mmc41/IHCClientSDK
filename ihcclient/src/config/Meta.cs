using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ihc
{
    /// <summary>
    /// Serializable variant of System.Version that can be used with System.Text.Json
    /// </summary>
    public record Version
    {
        /// <summary>
        /// Gets the major version number
        /// </summary>
        public int Major { get; init;  }

        /// <summary>
        /// Gets the minor version number
        /// </summary>
        public int Minor { get; init; }

        /// <summary>
        /// Gets the build number
        /// </summary>
        public int Build { get; init; }

        /// <summary>
        /// Gets the revision number
        /// </summary>
        public int Revision { get; init; }

        /// <summary>
        /// Initializes a new instance of the Version record with the specified major, minor, build, and revision numbers
        /// </summary>
        /// <param name="Major">The major version number</param>
        /// <param name="Minor">The minor version number</param>
        /// <param name="Build">The build number</param>
        /// <param name="Revision">The revision number</param>
        [JsonConstructor]
        public Version(int Major, int Minor, int Build, int Revision)
        {
            this.Major = Major;
            this.Minor = Minor;
            this.Revision = Revision;
            this.Build = Build;
        }

        /// <summary>
        /// Initializes a new instance of the Version record from a System.Version instance
        /// </summary>
        /// <param name="version">The System.Version to convert from</param>
        public Version(System.Version version)
        {
            this.Major = version.Major;
            this.Minor = version.Minor;
            this.Build = version.Build;
            this.Revision = version.Revision;
        }
    }

    /// <summary>
    /// Model meta data for serialized data: Type + Version.
    /// </summary>
    public record ModelMetadata
    {
        /// <summary>
        /// Gets the metadata version (from Assembly)
        /// </summary>
        public Version? Version { get; init; }

        /// <summary>
        /// Gets the fully qualified type name of the serialized model
        /// </summary>
        public string? TypeFullName { get; init; }

        /// <summary>
        /// Returns metadata for the specified type using the current assembly version
        /// </summary>
        /// <param name="modelType">The model type to create metadata for</param>
        /// <returns>ModelMetadata containing the type's full name and assembly version</returns>
        internal static ModelMetadata Current(Type modelType)
        {
            System.Version? assemblyVersion = modelType.Assembly.GetName().Version;
            return new ModelMetadata()
            {
                TypeFullName = modelType.FullName,
                Version = assemblyVersion == null ? null : new Version(assemblyVersion)
            };
        }
    }
}