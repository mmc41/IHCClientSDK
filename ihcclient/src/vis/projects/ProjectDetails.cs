namespace Ihc.Vis.Projects
{
    /// <summary>
    /// The metadata supplied when creating a new project — the write path for the fields exposed read-only
    /// on <see cref="Project"/> (<see cref="Project.Programmer"/>, <see cref="Project.InstallerName"/>, …).
    /// Named <c>ProjectDetails</c> rather than <c>ProjectInfo</c> so it never clashes with the controller-side
    /// <see cref="Ihc.ProjectInfo"/> (a different concept: a cheap controller-reported summary), even when a
    /// caller imports both <c>Ihc</c> and <c>Ihc.Vis.Projects</c>.
    /// </summary>
    /// <remarks>
    /// The three positional fields are the minimum IHC Visual's File→New writes; the <c>init</c> properties
    /// cover the rest of the <c>project_info</c>/<c>installer_info</c>/<c>customer_info</c> dialog field set
    /// (every DTD attribute minus <c>udf</c>, which no vendor dialog writes). <c>null</c> means not written —
    /// after creation the same vocabulary is editable via <c>ProjectEditor.Set*Info</c>.
    /// </remarks>
    public sealed record ProjectDetails(string Programmer, string InstallerName, string InstallerCountry)
    {
        /// <summary>The project number (<c>project_info/@number</c>).</summary>
        public string? ProjectNumber { get; init; }

        /// <summary>The drawing reference (<c>project_info/@drawing</c>).</summary>
        public string? Drawing { get; init; }

        /// <summary>The project type (<c>project_info/@type</c>).</summary>
        public string? ProjectType { get; init; }

        /// <summary>The project description (<c>project_info/@description</c>).</summary>
        public string? Description { get; init; }

        /// <summary>The installer street address (<c>installer_info/@address</c>).</summary>
        public string? InstallerAddress { get; init; }

        /// <summary>The installer city (<c>installer_info/@city</c>).</summary>
        public string? InstallerCity { get; init; }

        /// <summary>The installer postal code (<c>installer_info/@zipcode</c>).</summary>
        public string? InstallerZipCode { get; init; }

        /// <summary>The installer phone number (<c>installer_info/@phone</c>).</summary>
        public string? InstallerPhone { get; init; }

        /// <summary>The installer mobile phone number (<c>installer_info/@mobilephone</c>).</summary>
        public string? InstallerMobilePhone { get; init; }

        /// <summary>The installer e-mail address (<c>installer_info/@email</c>).</summary>
        public string? InstallerEmail { get; init; }

        /// <summary>The customer name (<c>customer_info/@name</c>).</summary>
        public string? CustomerName { get; init; }

        /// <summary>The customer street address (<c>customer_info/@address</c>).</summary>
        public string? CustomerAddress { get; init; }

        /// <summary>The customer city (<c>customer_info/@city</c>).</summary>
        public string? CustomerCity { get; init; }

        /// <summary>The customer postal code (<c>customer_info/@zipcode</c>).</summary>
        public string? CustomerZipCode { get; init; }

        /// <summary>The customer country (<c>customer_info/@country</c>).</summary>
        public string? CustomerCountry { get; init; }

        /// <summary>The customer phone number (<c>customer_info/@phone</c>).</summary>
        public string? CustomerPhone { get; init; }

        /// <summary>The customer mobile phone number (<c>customer_info/@mobilephone</c>).</summary>
        public string? CustomerMobilePhone { get; init; }

        /// <summary>The customer e-mail address (<c>customer_info/@email</c>).</summary>
        public string? CustomerEmail { get; init; }
    }
}
