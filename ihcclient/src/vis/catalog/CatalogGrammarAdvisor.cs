using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

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
        /// <summary>The advisory categories over <paramref name="body"/> against <paramref name="grammar"/>:
        /// <c>grammar-undeclared-type</c> ("declared" = any declaration record for the tag, full or
        /// orphan-ATTLIST-only, ordinal match), <c>grammar-undeclared-attribute</c>,
        /// <c>grammar-missing-required</c>, <c>grammar-enum-value</c>, <c>grammar-duplicate-id</c>,
        /// <c>grammar-dangling-idref</c> (within the definition), and <c>catalog-bound-unreadable</c>.</summary>
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
                    "Dobbelt id",
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
                    "Ukendt elementtype",
                    $"The body uses element type '{element.Tag}' that the effective grammar does not declare " +
                    "(an authentic subset-DTD shape; the written file stays loadable, but the type carries no " +
                    "catalog defaults)."));
            }
            else
            {
                AdviseAttrs(element, declaration, view.TryGet(element.Tag), findings);
            }

            // IDREF dangling detection reads the schema view (grammar first, registry fallback), so a registry
            // family's scene_resource is checked even when the grammar omits the declaration.
            if (view.TryGet(element.Tag) is { } schema)
            {
                foreach ((string name, string value) in element.Attrs)
                {
                    if (schema.IsIdRef(name) && value.Length > 0 && !ids.Contains(value))
                    {
                        findings.Add(Warn("grammar-dangling-idref", $"{element.Tag}@{name}",
                            "Reference uden mål",
                            $"IDREF attribute '{name}' on <{element.Tag}> references '{value}', which is not the " +
                            "id of any element in this definition."));
                    }
                }
            }

            foreach (ProjectElement child in element.Children)
            {
                Walk(child, grammar, view, ids, findings);
            }
        }

        private static void AdviseAttrs(ProjectElement element, GrammarDeclaration declaration,
            ElementSchema? schema, ImmutableArray<ProjectValidationFinding>.Builder findings)
        {
            foreach ((string name, string value) in element.Attrs)
            {
                GrammarAttr? attr = declaration.FindAttr(name);
                if (attr is null)
                {
                    findings.Add(Warn("grammar-undeclared-attribute", $"{element.Tag}@{name}",
                        "Ukendt attribut",
                        $"Attribute '{name}' on <{element.Tag}> is not declared by the effective grammar."));
                }
                else if (attr.Type == GrammarAttrType.Enumerated && !attr.EnumTokens.Contains(value))
                {
                    findings.Add(Warn("grammar-enum-value", $"{element.Tag}@{name}",
                        "Værdi uden for listen",
                        $"Value '{value}' of '{name}' on <{element.Tag}> is outside its declared enumeration " +
                        $"({string.Join(" | ", attr.EnumTokens)})."));
                }
            }

            AdviseBounds(element, schema, findings);

            foreach (GrammarAttr attr in declaration.Attrs)
            {
                if (attr.Default == GrammarDefault.Required && element.GetAttribute(attr.Name) is null)
                {
                    findings.Add(Warn("grammar-missing-required", $"{element.Tag}@{attr.Name}",
                        "Manglende påkrævet attribut",
                        $"#REQUIRED attribute '{attr.Name}' is missing on <{element.Tag}>."));
                }
            }
        }

        /// <summary>
        /// A declared numeric bound the reader cannot parse. Downstream it is indistinguishable from no bound at
        /// all, so the definition states a limit nothing enforces and the composed dialog stops offering the
        /// field rather than offering it unbounded — this row is what says why.
        /// </summary>
        /// <remarks>
        /// EFFECTIVE, exactly as <see cref="ElementView.Effective"/> reads it: the element's own value when
        /// present, else the attribute's declared DEFAULT. Checking only what the element carries would leave a
        /// grammar that defaults <c>minimum</c> to something unreadable reported by nothing, while every element
        /// of that tag inherits it — which is the same silence the row was minted to end, one level up. The
        /// schema view is the same reader the read side uses, so the pass and the consequence cannot disagree
        /// about what the bound says.
        /// </remarks>
        private static void AdviseBounds(ProjectElement element, ElementSchema? schema,
            ImmutableArray<ProjectValidationFinding>.Builder findings)
        {
            foreach (string name in BoundAttributes)
            {
                string? effective = element.GetAttribute(name)
                    ?? (schema?.FindAttr(name) is { Kind: AttrKind.Defaulted } declared ? declared.Default : null);
                if (effective is not { Length: > 0 } bound
                    || int.TryParse(bound, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    continue;
                }

                findings.Add(Warn("catalog-bound-unreadable", $"{element.Tag}@{name}",
                    "Grænseværdi kan ikke læses",
                    $"Bound '{name}' on <{element.Tag}> is '{bound}', which is not a whole number — the " +
                    "engine reads it as no bound at all, and a dialog will not offer the field."));
            }
        }

        /// <summary>The two attributes whose value is a numeric bound on the element's own <c>value</c> — the same
        /// pair <c>ElementView.DeclaredBounds</c> reads.</summary>
        private static readonly string[] BoundAttributes = ["minimum", "maximum"];

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

        /// <summary>
        /// One advisory, carrying BOTH sentences: the Danish label a user reads and the English detail a
        /// developer reads. The Danish is a literal copy of the code's catalogue template, kept here because
        /// this layer may not read the catalogue — a drift test holds the copy equal to the entry.
        /// </summary>
        private static ProjectValidationFinding Warn(
            string code, string? subject, string label, string diagnostic) =>
            new(ValidationSeverity.Warning, code, subject, label) { Diagnostic = diagnostic };
    }
}
