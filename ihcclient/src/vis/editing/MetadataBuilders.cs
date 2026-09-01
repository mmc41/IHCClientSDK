using System;
using System.Collections.Generic;

namespace Ihc.Vis.Editing
{
    /// <summary>
    /// Fluent configurator for the id-less <c>project_info</c> root metadata block (the Dokumentation ▸
    /// Projektinfo dialog's project half). Only the fields set here are written (upsert); setting <c>""</c>
    /// clears a field — it is dropped as the DTD default on commit, matching the vendor's blank ⇒
    /// attribute-omitted semantics. <c>udf</c> is DTD-declared but has no dialog field and is never written.
    /// </summary>
    public sealed class ProjectInfoBuilder
    {
        private readonly List<(string, string)> attributes = new();

        internal ProjectInfoBuilder()
        {
        }

        internal IReadOnlyList<(string, string)> Attributes => attributes;

        /// <summary>Sets the responsible programmer (<c>programmer</c>).</summary>
        public ProjectInfoBuilder Programmer(string value) => Add("programmer", value);

        /// <summary>Sets the project number (<c>number</c>).</summary>
        public ProjectInfoBuilder Number(string value) => Add("number", value);

        /// <summary>Sets the drawing reference (<c>drawing</c>).</summary>
        public ProjectInfoBuilder Drawing(string value) => Add("drawing", value);

        /// <summary>Sets the project type (<c>type</c>).</summary>
        public ProjectInfoBuilder Type(string value) => Add("type", value);

        /// <summary>Sets the project description (<c>description</c>).</summary>
        public ProjectInfoBuilder Description(string value) => Add("description", value);

        private ProjectInfoBuilder Add(string name, string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            attributes.Add((name, value));
            return this;
        }
    }

    /// <summary>
    /// Fluent configurator for the identical id-less <c>customer_info</c> / <c>installer_info</c> root
    /// metadata blocks (the Dokumentation ▸ Projektinfo dialog's two party halves). Same upsert / blank-clears
    /// semantics as <see cref="ProjectInfoBuilder"/>; <c>udf</c> is never written.
    /// </summary>
    public sealed class PartyInfoBuilder
    {
        private readonly List<(string, string)> attributes = new();

        internal PartyInfoBuilder()
        {
        }

        internal IReadOnlyList<(string, string)> Attributes => attributes;

        /// <summary>Sets the party name (<c>name</c>).</summary>
        public PartyInfoBuilder Name(string value) => Add("name", value);

        /// <summary>Sets the street address (<c>address</c>).</summary>
        public PartyInfoBuilder Address(string value) => Add("address", value);

        /// <summary>Sets the city (<c>city</c>).</summary>
        public PartyInfoBuilder City(string value) => Add("city", value);

        /// <summary>Sets the postal code (<c>zipcode</c>).</summary>
        public PartyInfoBuilder ZipCode(string value) => Add("zipcode", value);

        /// <summary>Sets the country (<c>country</c>).</summary>
        public PartyInfoBuilder Country(string value) => Add("country", value);

        /// <summary>Sets the phone number (<c>phone</c>).</summary>
        public PartyInfoBuilder Phone(string value) => Add("phone", value);

        /// <summary>Sets the mobile phone number (<c>mobilephone</c>).</summary>
        public PartyInfoBuilder MobilePhone(string value) => Add("mobilephone", value);

        /// <summary>Sets the e-mail address (<c>email</c>).</summary>
        public PartyInfoBuilder Email(string value) => Add("email", value);

        private PartyInfoBuilder Add(string name, string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            attributes.Add((name, value));
            return this;
        }
    }
}
