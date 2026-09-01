namespace Ihc.Vis.Projects
{
    /// <summary>
    /// Which id-allocation sub-order a File→New seed uses for the two built-in enums versus the three
    /// documentation-module containers. IHC Visual builds vary: most seed the enums first
    /// (<see cref="EnumsFirst"/> — e.g. Project0-Tomt, Project1-SimpelWired), but some seed the
    /// <c>*_modules</c> first (<see cref="ModulesFirst"/> — e.g. project2-CustomBlock). The document emission
    /// order is identical either way; only the seed ids differ (experiments A4 anomaly A-1).
    /// </summary>
    public enum SeedIdLayout
    {
        /// <summary>The two built-in enums (and their values) allocate first, then the documentation modules. The default.</summary>
        EnumsFirst,

        /// <summary>The three documentation <c>*_modules</c> containers allocate first, then the built-in enums.</summary>
        ModulesFirst,
    }
}
