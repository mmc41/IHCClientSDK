using System.Collections.Immutable;

using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// The three File→New templates, hand-authored in code (plan Phase C) so <see cref="BuiltInCatalog"/> reproduces
    /// what <c>CatalogDiscovery.FromInstallDir</c> loads from <c>Data\NewDoc.idf</c>,
    /// <c>Data\EnumeratorDefinitions.def</c> and <c>Data\fb.def</c> — without any IHC Visual install present. Each
    /// element is built to be <b>structurally equal</b> (<see cref="ProjectElement.Equals(ProjectElement)"/>) to what
    /// <c>CatalogReader.Read</c> yields for the corresponding file, i.e. the POST-parse shape with the file's internal
    /// DTD <c>ATTLIST</c> defaults materialized in the same order the XML reader surfaces them (explicit attributes in
    /// document order, then defaulted attributes in DTD-declaration order). Danish characters are written as literal
    /// UTF-8 in the string literals (the repo's <c>.cs</c> convention — this file is UTF-8, as are the tests that use
    /// e.g. <c>"Værdi1"</c>); they decode to the same Unicode code points the reader produces from each file's Latin-1
    /// bytes (Latin-1 <c>0xE6</c> → U+00E6 <c>æ</c>, <c>0xF8</c> → U+00F8 <c>ø</c>, <c>0xE9</c> → U+00E9 <c>é</c>,
    /// <c>0xE5</c> → U+00E5 <c>å</c>).
    /// </summary>
    /// <remarks>
    /// <para><b>Verification.</b> An install-gated differential test asserts each of these equals its
    /// <c>FromInstallDir</c> peer; the install-free byte tests (<c>CreateNew</c> → <c>Project0-Tomt.vis</c>,
    /// <c>AddEmptyFunctionBlock</c> → project3 "Tom blok") are the shipping gate.</para>
    /// <para><b>Grammar intentionally Empty on the FB template.</b> Every <c>fb.def</c> element type
    /// (<c>functionblock, inputs, outputs, settings, internalsettings, programs, program_simple, events, actions</c>)
    /// is declared in the static schema registry (<c>CanonicalDtdBlocks.dtd</c>), so
    /// <c>ProjectEditor.MergeNonRegistryBlocks</c> never merges them and they never reach any saved bytes. Carrying a
    /// reconstruction of the vendor DTD would re-couple the SDK to vendor grammar for no functional effect, contrary
    /// to the plan's registry-first principle. The <c>FromInstallDir</c> template still parses its file's grammar (it
    /// parses every file uniformly); that difference is dead data, so the differential test compares Body + identity,
    /// not the grammar.</para>
    /// </remarks>
    public sealed partial class BuiltInCatalog
    {
        partial void AuthorTemplates()
        {
            newProjectSkeleton = BuildNewProjectSkeleton();
            builtInEnumerators = BuildBuiltInEnumerators();
            emptyFunctionBlockTemplate = BuildEmptyFunctionBlockTemplate();
        }

        // ---- C1: NewDoc.idf skeleton (v1 shape, DTD-defaults materialized) ----
        private static ProjectElement BuildNewProjectSkeleton() =>
            E("utcs_project", new[]
            {
                ("version_major", "1"), ("version_minor", "0"),
                ("id1", "_0xd10272d"), ("id2", "_0xd102e21"),
                ("helpid", "_0x51"), ("last_unique_id", "_0x40"),
                ("icon", "_0x0"),   // DTD default
            },
                E("modified", new[]
                {
                    ("year", "2004"), ("month", "6"), ("day", "13"), ("hour", "16"), ("minute", "46"),
                }),
                Contact("customer_info"),
                Contact("installer_info"),
                E("project_info", new[]
                {
                    ("programmer", ""), ("description", ""), ("number", ""), ("drawing", ""), ("type", ""),
                }),
                // The seeded enums live in BuiltInEnumerators; the skeleton container is empty, carrying only its
                // id/name (+ DTD-default note) which NewProjectBuilder reuses for the fresh project's container.
                E("enum_definitions", new[]
                {
                    ("id", "_0x3046"), ("name", "Enumerator definitioner"), ("note", ""),
                }),
                E("groups", new[]
                {
                    ("id", "_0x2031"), ("name", "Lokaliteter"),
                    ("note", "Gruppering af produkter og funktionsblokke"),
                    ("icon", "_0x15"), ("helpid", "_0x1324"),   // DTD defaults
                },
                    Room("_0x2132", "Stue"),
                    Room("_0x2232", "Entré"),          // Entré
                    Room("_0x2332", "Køkken"),         // Køkken
                    Room("_0x2432", "Soveværelse"),    // Soveværelse
                    Room("_0x2532", "Værelse"),        // Værelse
                    Room("_0x2632", "Bad"),
                    Room("_0x2732", "Bryggers"),
                    Room("_0x2832", "Garage"),
                    Room("_0x2932", "Kælder"),         // Kælder
                    Room("_0x2a32", "Udendørs")));     // Udendørs

        // A default room: id + name explicit, then the three group DTD defaults (icon/note/helpid).
        private static ProjectElement Room(string id, string name) =>
            E("group", new[] { ("id", id), ("name", name), ("icon", Ihc.Vis.Schema.ResourceMaterialization.RequireIcon("group")), ("note", ""), ("helpid", "_0x1388") });

        // The two contact containers share one DTD ATTLIST → eight defaulted-empty attributes in declaration order.
        private static ProjectElement Contact(string tag) =>
            E(tag, new[]
            {
                ("name", ""), ("address", ""), ("city", ""), ("zipcode", ""),
                ("country", ""), ("phone", ""), ("mobilephone", ""), ("email", ""),
            });

        // ---- C2: EnumeratorDefinitions.def (2 built-in enums) ----
        private static ProjectElement BuildBuiltInEnumerators() =>
            E("enum_definitions", new[]
            {
                ("id", "_0x01"), ("name", "Enumerator definitioner"), ("note", ""),
            },
                E("enum_definition", new[]
                {
                    ("id", "_0x10"), ("name", "Persienne tilstand"), ("typeid", "_0x10"), ("note", ""),
                },
                    EnumValue("_0x11", "0", "Ukendt", "_0x11"),
                    EnumValue("_0x12", "1", "Oppe", "_0x12"),
                    EnumValue("_0x13", "2", "Nede", "_0x13"),
                    EnumValue("_0x14", "3", "Kører op", "_0x14"),    // Kører op
                    EnumValue("_0x15", "4", "Kører ned", "_0x15")),  // Kører ned
                E("enum_definition", new[]
                {
                    ("id", "_0x16"), ("name", "Logning"), ("typeid", "_0x16"), ("note", ""),
                },
                    EnumValue("_0x17", "0", "Off", "_0x17"),
                    EnumValue("_0x18", "1", "Kun ændringer", "_0x18"),  // Kun ændringer
                    EnumValue("_0x19", "2", "Hver Time", "_0x19"),
                    EnumValue("_0x20", "3", "Dagligt", "_0x20"),
                    EnumValue("_0x21", "4", "Ugentligt", "_0x21"),
                    EnumValue("_0x22", "5", "Månedligt", "_0x22")));   // Månedligt

        // An enum value: id/index/name/typeid explicit, then the DTD-default empty note.
        private static ProjectElement EnumValue(string id, string index, string name, string typeid) =>
            E("enum_value", new[] { ("id", id), ("index", index), ("name", name), ("typeid", typeid), ("note", "") });

        // ---- C3: fb.def empty function block ("Tom blok") ----
        private static FunctionBlockDefinition BuildEmptyFunctionBlockTemplate() =>
            new FunctionBlockDefinition(
                MasterType: "", MasterVersion: "", MasterName: "Tom blok", DisplayName: "Tom blok",
                CategoryPath: "", Body: BuildEmptyFunctionBlockBody())
            {
                IsEmptyTemplate = true,
                // Grammar intentionally Empty — see class remarks (all tags registry-declared).
            };

        private static ProjectElement BuildEmptyFunctionBlockBody() =>
            E("functionblock", new[]
            {
                ("id", "_0x01"), ("name", "Tom blok"),
                ("master_schneider_electric", "no"), ("master_type", ""), ("master_version", ""),
                ("master_name", ""), ("master_programmer", ""),
                ("master_date_year", ""), ("master_date_month", ""), ("master_date_day", ""),
                ("locked", "no"), ("icon", "_0xf"), ("note", ""), ("helpid", "_0xfa0"),
            },
                E("inputs", new[]
                {
                    ("id", "_0x02"), ("name", "Input"),
                    ("note", "Variablene i denne gruppering er input til funktionsblokken"),
                    ("icon", "_0x4"), ("helpid", "_0xdac"),
                }),
                E("outputs", new[]
                {
                    ("id", "_0x03"), ("name", "Output"),
                    ("note", "Variablene i denne gruppering er output fra funktionsblokken"),
                    ("icon", "_0x14"), ("helpid", "_0xe10"),
                }),
                E("settings", new[]
                {
                    ("id", "_0x04"), ("name", "Indstillinger"),
                    ("note", "Variablene i denne gruppering er indstillinger til funktionsblokken"),
                    ("icon", "_0xd"), ("helpid", "_0xe74"),
                }),
                // Vendor typo preserved verbatim: id="_05" (not "_0x05") — CatalogReader keeps the raw token.
                E("internalsettings", new[]
                {
                    ("id", "_05"), ("name", "Interne variable"),
                    ("note", "Variablene i denne gruppe er private variable til funktionsblokken"),
                    ("icon", "_0x13"), ("helpid", "_0x1004"),
                }),
                E("programs", new[]
                {
                    ("id", "_0x06"), ("name", "Programmer"),
                    ("note", "Gruppering af funktionblokkens programmer"),
                    ("icon", "_0x19"), ("helpid", "_0xed8"),
                },
                    E("program_simple", new[]
                    {
                        ("id", "_0x07"), ("name", "Program"),
                        ("icon", "_0x7"), ("note", ""), ("helpid", "_0xbb9"),
                    },
                        E("events", new[]
                        {
                            ("id", "_0x08"), ("name", "Hændelser"),                 // Hændelser
                            ("note", "Hændelser som starter program"),             // Hændelser som starter program
                            ("icon", "_0xb"), ("helpid", "_0x2710"),
                        }),
                        E("actions", new[]
                        {
                            ("id", "_0x09"), ("name", "Kommandoer"),
                            // Gruppering af kommandoer som udføres når hændelse er indtruffet
                            ("note", "Gruppering af kommandoer som udføres når hændelse er indtruffet"),
                            ("icon", "_0x8"), ("helpid", "_0x27d8"), ("type", "_0x2"),
                        }))));

        // Element factory sharing CatalogReader's Id derivation (ElementId.ParseOrNull), so a vendor-typo token like
        // "_05" yields the same Id (or null) on both sides and the trees compare structurally equal.
        private static ProjectElement E(string tag, (string, string)[] attrs, params ProjectElement[] children)
        {
            ImmutableArray<(string, string)> bag = attrs.ToImmutableArray();
            return new ProjectElement(tag, ElementId.ParseOrNull(ProjectElement.GetAttribute(bag, "id")),
                bag, children.ToImmutableArray());
        }
    }
}
