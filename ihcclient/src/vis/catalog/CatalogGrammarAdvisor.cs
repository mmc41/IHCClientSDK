#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Ihc.Vis.Model;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// The grammar↔body advisory pass of the builders' <c>Validate()</c>: non-blocking <b>warnings</b> for the
    /// validity-class findings the well-formedness write gate deliberately does not block on — authentic vendor
    /// files violate them (superset/subset DTDs, the S0 kWh <c>accessibility="readwrite"</c> enum bug) and must
    /// keep writing, but a hand author probably wants to know. Evaluated only in <c>Validate()</c>, never inside
    /// <c>Write</c>; skipped entirely when the effective grammar carries no declarations (open-world
    /// <c>Create</c> without a grammar — everything would be "undeclared" noise).
    /// </summary>
    internal static class CatalogGrammarAdvisor
    {
        /// <summary>The six advisory categories over <paramref name="body"/> against <paramref name="grammar"/>:
        /// <c>grammar-undeclared-type</c> ("declared" = any declaration record for the tag, full or
        /// orphan-ATTLIST-only, ordinal match), <c>grammar-undeclared-attribute</c>,
        /// <c>grammar-missing-required</c>, <c>grammar-enum-value</c>, <c>grammar-duplicate-id</c>, and
        /// <c>grammar-dangling-idref</c> (within the definition).</summary>
        public static ImmutableArray<ProjectValidationFinding> Advise(ProjectElement body, CatalogGrammar grammar)
        {
            var findings = ImmutableArray.CreateBuilder<ProjectValidationFinding>();
            if (grammar is null || grammar.Declarations.IsEmpty)
            {
                return findings.ToImmutable();
            }
            ProjectSchemaView view = ProjectSchemaView.For(grammar);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var duplicateIds = new HashSet<string>(StringComparer.Ordinal);
            CollectIds(body, ids, duplicateIds);
            foreach (string duplicate in duplicateIds)
            {
                findings.Add(Warn("grammar-duplicate-id", duplicate,
                    $"Two or more elements share the id '{duplicate}' (XML VC: ID) — the insert transform " +
                    "re-mints them to distinct ids, but IDREFs to the token become ambiguous."));
            }

            Walk(body, grammar, view, ids, findings);
            return findings.ToImmutable();
        }

        private static void Walk(ProjectElement element, CatalogGrammar grammar, ProjectSchemaView view,
            HashSet<string> ids, ImmutableArray<ProjectValidationFinding>.Builder findings)
        {
            GrammarDeclaration? declaration = grammar.TryGetDeclaration(element.Tag);
            if (declaration is null)
            {
                findings.Add(Warn("grammar-undeclared-type", element.Tag,
                    $"The body uses element type '{element.Tag}' that the effective grammar does not declare " +
                    "(an authentic subset-DTD shape; the written file stays loadable, but the type carries no " +
                    "catalog defaults)."));
            }
            else
            {
                AdviseAttrs(element, declaration, findings);
            }

            // IDREF dangling detection reads the schema view (grammar first, registry fallback), so a registry
            // family's scene_resource is checked even when the grammar omits the declaration.
            if (view.TryGet(element.Tag) is { } schema)
            {
                foreach ((string name, string value) in element.AttrsOrEmpty())
                {
                    if (schema.IsIdRef(name) && value.Length > 0 && !ids.Contains(value))
                    {
                        findings.Add(Warn("grammar-dangling-idref", $"{element.Tag}@{name}",
                            $"IDREF attribute '{name}' on <{element.Tag}> references '{value}', which is not the " +
                            "id of any element in this definition."));
                    }
                }
            }

            foreach (ProjectElement child in element.ChildrenOrEmpty())
            {
                Walk(child, grammar, view, ids, findings);
            }
        }

        private static void AdviseAttrs(ProjectElement element, GrammarDeclaration declaration,
            ImmutableArray<ProjectValidationFinding>.Builder findings)
        {
            foreach ((string name, string value) in element.AttrsOrEmpty())
            {
                GrammarAttr? attr = declaration.FindAttr(name);
                if (attr is null)
                {
                    findings.Add(Warn("grammar-undeclared-attribute", $"{element.Tag}@{name}",
                        $"Attribute '{name}' on <{element.Tag}> is not declared by the effective grammar."));
                }
                else if (attr.Type == GrammarAttrType.Enumerated && !attr.EnumTokens.Contains(value))
                {
                    findings.Add(Warn("grammar-enum-value", $"{element.Tag}@{name}",
                        $"Value '{value}' of '{name}' on <{element.Tag}> is outside its declared enumeration " +
                        $"({string.Join(" | ", attr.EnumTokens)})."));
                }
            }
            foreach (GrammarAttr attr in declaration.Attrs)
            {
                if (attr.Default == GrammarDefault.Required && element.GetAttribute(attr.Name) is null)
                {
                    findings.Add(Warn("grammar-missing-required", $"{element.Tag}@{attr.Name}",
                        $"#REQUIRED attribute '{attr.Name}' is missing on <{element.Tag}>."));
                }
            }
        }

        private static void CollectIds(ProjectElement element, HashSet<string> ids, HashSet<string> duplicates)
        {
            foreach (ProjectElement e in element.DescendantsAndSelf())
            {
                if (e.GetAttribute("id") is { } id && !ids.Add(id))
                {
                    duplicates.Add(id);
                }
            }
        }

        private static ProjectValidationFinding Warn(string category, string? subject, string message) =>
            new(ValidationSeverity.Warning, category, subject, message);
    }
}
