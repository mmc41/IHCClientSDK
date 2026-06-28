#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace Ihc.Projects
{
    /// <summary>
    /// The pre-serialize validation checklist (spec ch. 10 §10.5): id well-formedness / counter uniqueness /
    /// type-code agreement, the <c>last_unique_id</c> high-water-mark invariant, IDREF resolution (schema-driven,
    /// so only genuine IDREF attributes are checked), reciprocal <c>link_from_resource</c>/<c>link_to_resource</c>
    /// bijection, ISO-8859-1 encodability of all text, and registry-backed attribute conformance — every
    /// <c>#REQUIRED</c> attribute is present and every enumerated attribute's value is within its declared set
    /// (e.g. <c>locked</c> ∈ {yes, no}). The last two derive from the same schema registry the inline DTD is emitted
    /// from, so they catch authoring mistakes against the registry without re-parsing the file's own DTD (which is
    /// deliberately ignored on load). Returns a structured <see cref="ProjectValidationResult"/> rather than
    /// throwing, so a GUI can surface every problem at once.
    /// </summary>
    internal static class ProjectValidator
    {
        public static ProjectValidationResult Validate(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            ProjectSchemaView view = ProjectSchemaView.For(project);
            var errors = ImmutableArray.CreateBuilder<string>();

            var elements = new List<ProjectElement>();
            Collect(project.Root, elements);

            var idTokens = new HashSet<string>(StringComparer.Ordinal);
            var counters = new HashSet<int>();
            long maxCounter = 0;
            foreach (ProjectElement element in elements)
            {
                string? idToken = element.GetAttribute("id");
                if (idToken is not null)
                {
                    idTokens.Add(idToken);
                    if (ElementId.TryParse(idToken, out ElementId id))
                    {
                        if (!counters.Add(id.Counter))
                        {
                            errors.Add($"duplicate id counter in '{idToken}' (element '{element.Tag}')");
                        }
                        if (id.Counter > maxCounter)
                        {
                            maxCounter = id.Counter;
                        }
                        int? typeCode = TypeCode.ForTag(element.Tag);
                        if (typeCode is { } tc && tc != id.TypeCode)
                        {
                            errors.Add($"id '{idToken}' on '{element.Tag}' has type-code 0x{id.TypeCode:x2}, expected 0x{tc:x2}");
                        }
                    }
                }
            }

            foreach (ProjectElement element in elements)
            {
                ValidateElement(element, idTokens, errors, view);
            }

            ValidateLinkBijection(elements, errors);

            long lastUniqueId = ParseHex(project.LastUniqueId);
            if (lastUniqueId < maxCounter)
            {
                errors.Add($"last_unique_id (0x{lastUniqueId:x}) is below the highest counter present (0x{maxCounter:x})");
            }

            return errors.Count == 0
                ? ProjectValidationResult.Success
                : new ProjectValidationResult(false, errors.ToImmutable());
        }

        private static void ValidateElement(ProjectElement element, HashSet<string> idTokens, ImmutableArray<string>.Builder errors, ProjectSchemaView view)
        {
            ElementSchema? schema = view.TryGet(element.Tag);
            if (schema is null)
            {
                errors.Add($"element type '{element.Tag}' is not declared in the project's inline DTD or the schema registry (cannot be serialized)");
                return;
            }

            // #REQUIRED attributes must be present (runs even when the element carries no attributes at all).
            foreach (AttrSchema attr in schema.Attrs)
            {
                if (attr.Kind == AttrKind.Required && element.GetAttribute(attr.Name) is null)
                {
                    errors.Add($"required attribute '{attr.Name}' missing on '{element.Tag}'");
                }
            }

            if (element.Attrs.IsDefaultOrEmpty)
            {
                return;
            }
            foreach ((string name, string value) in element.Attrs)
            {
                if (!IsLatin1(value))
                {
                    errors.Add($"attribute '{name}' on '{element.Tag}' has non-ISO-8859-1 text");
                }
                AttrSchema? attr = FindAttr(schema, name);
                if (attr is null)
                {
                    continue;   // an attribute outside the registry is dropped on serialize, not a validity error
                }
                if (attr.Render == AttrRender.IdRef && !idTokens.Contains(value))
                {
                    errors.Add($"dangling {name}='{value}' on '{element.Tag}' (no element has that id)");
                }
                if (!attr.EnumValues.IsDefaultOrEmpty && !attr.EnumValues.Contains(value))
                {
                    errors.Add($"attribute {name}='{value}' on '{element.Tag}' is not one of ({string.Join(" | ", attr.EnumValues)})");
                }
            }
        }

        private static void ValidateLinkBijection(List<ProjectElement> elements, ImmutableArray<string>.Builder errors)
        {
            var halves = new Dictionary<string, ProjectElement>(StringComparer.Ordinal);
            foreach (ProjectElement element in elements)
            {
                if (element.Tag is "link_from_resource" or "link_to_resource" && element.GetAttribute("id") is { } id)
                {
                    halves[id] = element;
                }
            }

            foreach (ProjectElement half in halves.Values)
            {
                string? partnerId = half.GetAttribute("link");
                if (partnerId is null || !halves.TryGetValue(partnerId, out ProjectElement? partner))
                {
                    errors.Add($"{half.Tag} '{half.GetAttribute("id")}' links to missing half '{partnerId}'");
                    continue;
                }
                string expectedTag = half.Tag == "link_from_resource" ? "link_to_resource" : "link_from_resource";
                if (partner.Tag != expectedTag)
                {
                    errors.Add($"{half.Tag} '{half.GetAttribute("id")}' partner is a {partner.Tag}, expected {expectedTag}");
                }
                else if (partner.GetAttribute("link") != half.GetAttribute("id"))
                {
                    errors.Add($"{half.Tag} '{half.GetAttribute("id")}' is not reciprocally linked");
                }
            }
        }

        private static AttrSchema? FindAttr(ElementSchema schema, string attrName)
        {
            foreach (AttrSchema attr in schema.Attrs)
            {
                if (attr.Name == attrName)
                {
                    return attr;
                }
            }
            return null;
        }

        private static bool IsLatin1(string value)
        {
            foreach (char c in value)
            {
                if (c > 0xFF)
                {
                    return false;
                }
            }
            return true;
        }

        private static long ParseHex(string? token) =>
            token is not null
            && token.StartsWith("_0x", StringComparison.Ordinal)
            && long.TryParse(token.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long value)
                ? value
                : 0;

        private static void Collect(ProjectElement element, List<ProjectElement> into)
        {
            into.Add(element);
            if (element.Children.IsDefaultOrEmpty)
            {
                return;
            }
            foreach (ProjectElement child in element.Children)
            {
                Collect(child, into);
            }
        }
    }
}
