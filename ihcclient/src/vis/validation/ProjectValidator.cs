#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Addressing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Io;
using TypeCode = Ihc.Vis.Schema.TypeCode;
namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The pre-serialize validation checklist (spec ch. 10 §10.5): id well-formedness / token and counter
    /// uniqueness / type-code agreement, the <c>last_unique_id</c> invariants (high-water mark, well-formedness,
    /// 24-bit ceiling), IDREF resolution (schema-driven, so only genuine IDREF attributes are checked),
    /// reciprocal <c>link_from_resource</c>/<c>link_to_resource</c> and scene-row bijections, function-block
    /// shape and programming-reference locality (references stay within one function block; embedded constants
    /// match their parent's <c>link2</c>/<c>value</c>), <c>resource_enum</c> typedef/inivalue consistency,
    /// dataline addressing (1–128, unique per direction), ISO-8859-1 encodability of all text, and
    /// registry-backed attribute conformance — every <c>#REQUIRED</c> attribute present, no undeclared
    /// attributes, and every enumerated attribute's value within its declared set. Structural deviations vendor
    /// tooling tolerates (root child order, unmodeled containment, program skeleton shape) surface as
    /// <see cref="ValidationSeverity.Warning"/>s, which never make a project invalid. Returns a structured
    /// <see cref="ProjectValidationResult"/> rather than throwing, so a GUI can surface every problem at once.
    /// </summary>
    internal static class ProjectValidator
    {
        public static ProjectValidationResult Validate(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            ProjectSchemaView view = ProjectSchemaView.For(project);
            var findings = new FindingCollector();

            IReadOnlyList<ProjectElement> elements = project.Root.DescendantsAndSelf();

            // idToElement's key set IS the set of id tokens — no separate HashSet is kept in lockstep with it.
            var idToElement = new Dictionary<string, ProjectElement>(StringComparer.Ordinal);
            long maxCounter = ValidateIds(elements, idToElement, findings);

            foreach (ProjectElement element in elements)
            {
                ValidateElement(element, idToElement, findings, view);
                if (element.Tag == "functionblock")
                {
                    ValidateFunctionBlockShape(element, findings);
                    ValidateProgrammingReferences(element, idToElement, findings, view);
                }
                if (element.Tag is "program_simple" or "program_sub")
                {
                    ValidateProgramShape(element, findings);
                }
            }

            // Follow-links must always be wired (a half with no partner is corruption); scene rows may be authored
            // unwired (link=NullToken), so those are skipped rather than flagged.
            ValidateReciprocity(elements, ReciprocalTags.FollowLinkHalfTags, "link-bijection", "half",
                allowUnwired: false, findings);
            ValidateReciprocity(elements, ReciprocalTags.SceneHalfTags, "scene-bijection", "scene row",
                allowUnwired: true, findings);
            ValidateDatalineAddressing(elements, findings);
            ValidateEnumConsistency(elements, idToElement, findings);
            ValidateRoot(project, maxCounter, findings);
            ValidateContainment(project.Root, grandParent: null, findings);

            return ProjectValidationResult.FromFindings(findings.ToImmutable());
        }

        /// <summary>
        /// The full categorized verification (R10): the structural checklist above plus the
        /// <see cref="DocumentationValidator"/> completeness checks, the latter appended as
        /// <see cref="ValidationCategory.Documentation"/> warnings in their own pinned order.
        /// <c>IsValid</c>/<c>Errors</c> semantics are unchanged by construction — documentation
        /// findings are never <see cref="ValidationSeverity.Error"/>s.
        /// </summary>
        public static ProjectValidationResult ValidateCategorized(Project project)
        {
            ProjectValidationResult structural = Validate(project);
            ImmutableArray<ProjectValidationFinding> documentation = DocumentationValidator.Check(project);
            return documentation.IsEmpty
                ? structural
                : ProjectValidationResult.FromFindings(structural.Findings.AddRange(documentation));
        }

        // ----- ids -----

        private static long ValidateIds(IReadOnlyList<ProjectElement> elements,
            Dictionary<string, ProjectElement> idToElement, FindingCollector findings)
        {
            var counters = new HashSet<int>();
            long maxCounter = 0;
            foreach (ProjectElement element in elements)
            {
                string? idToken = element.GetAttribute("id");
                if (idToken is null)
                {
                    continue;
                }
                if (!idToElement.TryAdd(idToken, element))
                {
                    findings.Error("id-duplicate-token", element,
                        $"duplicate id token '{idToken}' (element '{element.Tag}')");
                    continue;
                }
                if (ElementId.TryParse(idToken, out ElementId id))
                {
                    if (!counters.Add(id.Counter))
                    {
                        findings.Error("id-duplicate-counter", element,
                            $"duplicate id counter in '{idToken}' (element '{element.Tag}')");
                    }
                    if (id.Counter > maxCounter)
                    {
                        maxCounter = id.Counter;
                    }
                    int? typeCode = TypeCode.ForTag(element.Tag);
                    if (typeCode is { } tc && tc != id.TypeCode)
                    {
                        findings.Error("id-typecode", element,
                            $"id '{idToken}' on '{element.Tag}' has type-code 0x{id.TypeCode:x2}, expected 0x{tc:x2}");
                    }
                }
                else
                {
                    findings.Error("id-wellformed", element,
                        $"id '{idToken}' on '{element.Tag}' is not a well-formed _0x hex token in the legal " +
                        "packed range (spec ch. 02)");
                }
            }
            return maxCounter;
        }

        // ----- per-element attribute conformance -----

        private static void ValidateElement(ProjectElement element, Dictionary<string, ProjectElement> idToElement,
            FindingCollector findings, ProjectSchemaView view)
        {
            ElementSchema? schema = view.TryGet(element.Tag);
            if (schema is null)
            {
                findings.Error("element-undeclared", element,
                    $"element type '{element.Tag}' is not declared in the project's inline DTD or the schema " +
                    "registry (cannot be serialized)");
                return;
            }

            // #REQUIRED attributes must be present (runs even when the element carries no attributes at all).
            foreach (AttrSchema attr in schema.Attrs)
            {
                if (attr.Kind == AttrKind.Required && element.GetAttribute(attr.Name) is null)
                {
                    findings.Error("attr-required", element,
                        $"required attribute '{attr.Name}' missing on '{element.Tag}'");
                }
            }

            if (element.Attrs.IsDefaultOrEmpty)
            {
                return;
            }
            foreach ((string name, string value) in element.Attrs)
            {
                if (!Latin1.Contains(value))
                {
                    findings.Error("attr-latin1", element,
                        $"attribute '{name}' on '{element.Tag}' has non-ISO-8859-1 text");
                }
                AttrSchema? attr = schema.FindAttr(name);
                if (attr is null)
                {
                    findings.Error("attr-undeclared", element,
                        $"attribute '{name}' on '{element.Tag}' is not declared in the element's inline-DTD " +
                        "block or the schema registry (serialization will fail)");
                    continue;
                }
                if (attr.Render == AttrRender.IdRef && value != ElementId.NullToken && !idToElement.ContainsKey(value))
                {
                    // The null token is the sentinel for an unwired IDREF (StampRequiredNullTokens stamps it,
                    // ValidateSceneBijection blesses it) — a legitimate authored state, never a dangling reference.
                    findings.Error("idref-dangling", element,
                        $"dangling {name}='{value}' on '{element.Tag}' (no element has that id)");
                }
                if (!attr.EnumValues.IsDefaultOrEmpty && !attr.EnumValues.Contains(value))
                {
                    findings.Error("attr-enum-range", element,
                        $"attribute {name}='{value}' on '{element.Tag}' is not one of ({string.Join(" | ", attr.EnumValues)})");
                }
            }
        }

        // ----- function-block shape (spec ch. 06 §6.3) -----

        // The five containers a function block must hold, in fixed order: the four variable sections from the
        // shared FunctionBlockSections source of truth, followed by the programs container. Derived — not an
        // independent literal — so it can never silently drift from FunctionBlockSections.All.
        private static readonly string[] FunctionBlockContainers =
            FunctionBlockSections.All.Select(s => s.Container).Append("programs").ToArray();

        private static void ValidateFunctionBlockShape(ProjectElement functionBlock, FindingCollector findings)
        {
            string id = functionBlock.GetAttribute("id") ?? "?";
            ImmutableArray<ProjectElement> children = functionBlock.Children.IsDefaultOrEmpty
                ? ImmutableArray<ProjectElement>.Empty
                : functionBlock.Children;

            // (1) exactly the five containers, in the fixed order (no missing / extra / foreign / reordered child).
            bool shapeOk = children.Length == FunctionBlockContainers.Length;
            for (int i = 0; shapeOk && i < children.Length; i++)
            {
                shapeOk = children[i].Tag == FunctionBlockContainers[i];
            }
            if (!shapeOk)
            {
                var actual = new List<string>();
                foreach (ProjectElement c in children)
                {
                    actual.Add(c.Tag);
                }
                findings.Error("fb-shape", functionBlock,
                    $"functionblock '{id}' must contain exactly the five containers " +
                    $"[{string.Join(", ", FunctionBlockContainers)}] in that order, but has [{string.Join(", ", actual)}]");
            }

            // (2) programs may hold only program_simple.
            ProjectElement? programs = functionBlock.FindChild("programs");
            if (programs is not null && !programs.Children.IsDefaultOrEmpty)
            {
                foreach (ProjectElement program in programs.Children)
                {
                    if (program.Tag != "program_simple")
                    {
                        findings.Error("fb-programs", functionBlock,
                            $"functionblock '{id}': programs contains '{program.Tag}', but programs may hold only program_simple");
                    }
                }
            }

            // (3) pin types are bound to their container (§6.3.1).
            foreach (ProjectElement container in children)
            {
                if (container.Children.IsDefaultOrEmpty)
                {
                    continue;
                }
                foreach (ProjectElement child in container.Children)
                {
                    string? required = PlacementRules.PinContainerFor(child.Tag);
                    if (required is not null && required != container.Tag)
                    {
                        findings.Error("fb-pin-container", child,
                            $"functionblock '{id}': {child.Tag} must be under {required}, not {container.Tag}");
                    }
                }
            }
        }

        // ----- programming-reference locality + embedded constants (spec ch. 07 / ch. 10 §10.5) -----

        // FB-locality is a per-(element, attribute) fact, not an attribute-name fact: program_case@link is a local
        // switch-variable IDREF, but link_from/to_resource@link is a legitimately non-local follow-link — so `link`
        // cannot be gated by name alone. (Pairs per CanonicalDtdBlocks.dtd: event/condition/action → link1/link2;
        // case_action → variable/value; program_case → link.)
        private static readonly HashSet<(string Tag, string Attr)> FbLocalRefAttrs = new()
        {
            ("event", "link1"), ("event", "link2"),
            ("condition", "link1"), ("condition", "link2"),
            ("action", "link1"), ("action", "link2"),
            ("case_action", "variable"), ("case_action", "value"),
            ("program_case", "link"),
        };

        private static void ValidateProgrammingReferences(ProjectElement functionBlock, Dictionary<string, ProjectElement> idToElement,
            FindingCollector findings, ProjectSchemaView view)
        {
            var localIds = new HashSet<string>(StringComparer.Ordinal);
            CollectIdTokens(functionBlock, localIds);

            void Walk(ProjectElement element)
            {
                ElementSchema? schema = view.TryGet(element.Tag);
                if (schema is not null && !element.Attrs.IsDefaultOrEmpty)
                {
                    foreach ((string name, string value) in element.Attrs)
                    {
                        // Locality applies to the programming references only; `link` (follow-link halves),
                        // `typedef`/`inivalue` (project-level enum registry) etc. are legitimately non-local.
                        if (FbLocalRefAttrs.Contains((element.Tag, name))
                            && schema.FindAttr(name) is { Render: AttrRender.IdRef }
                            && idToElement.ContainsKey(value) && !localIds.Contains(value))
                        {
                            findings.Error("fb-local-ref", element,
                                $"{name}='{value}' on '{element.Tag}' references an element outside its function " +
                                "block (programming references must stay within one functionblock)");
                        }
                    }
                }
                if (element.Tag is "event" or "condition" or "action" or "case_action")
                {
                    ValidateEmbeddedConstants(element, findings);
                }
                if (!element.Children.IsDefaultOrEmpty)
                {
                    foreach (ProjectElement child in element.Children)
                    {
                        Walk(child);
                    }
                }
            }
            Walk(functionBlock);
        }

        private static void ValidateEmbeddedConstants(ProjectElement leaf, FindingCollector findings)
        {
            if (leaf.Children.IsDefaultOrEmpty)
            {
                return;
            }
            string referenceAttr = leaf.Tag == "case_action" ? "value" : "link2";
            foreach (ProjectElement child in leaf.Children)
            {
                if (!child.Tag.StartsWith("resource_", StringComparison.Ordinal))
                {
                    continue;
                }
                string? childId = child.GetAttribute("id");
                string? referenced = leaf.GetAttribute(referenceAttr);
                if (childId is null || childId != referenced)
                {
                    findings.Error("inline-constant", leaf,
                        $"embedded constant <{child.Tag}> '{childId}' inside <{leaf.Tag}> must be referenced by " +
                        $"its parent's {referenceAttr} (found '{referenced}')");
                }
            }
        }

        // ----- program skeleton shapes (advisory: vendor loads deviants without complaint) -----

        private static void ValidateProgramShape(ProjectElement program, FindingCollector findings)
        {
            string[] expected = program.Tag == "program_simple"
                ? new[] { "events", "actions" }
                : new[] { "conditions", "actions", "actions" };
            ImmutableArray<ProjectElement> children = program.Children.IsDefaultOrEmpty
                ? ImmutableArray<ProjectElement>.Empty
                : program.Children;
            bool ok = children.Length == expected.Length;
            for (int i = 0; ok && i < children.Length; i++)
            {
                ok = children[i].Tag == expected[i];
            }
            if (!ok)
            {
                findings.Warning("program-shape", program,
                    $"'{program.Tag}' '{program.GetAttribute("id") ?? "?"}' does not have the vendor skeleton " +
                    $"[{string.Join(", ", expected)}] (found [{string.Join(", ", children.Select(c => c.Tag))}])");
            }
        }

        // ----- reciprocal bijections (spec ch. 06 §6.4, ch. 08) -----

        // The single reciprocal-pair check for both the follow-link halves and the scene rows: every wired half must
        // point at a live partner of the COMPLEMENTARY kind that points back at it. Merging the two once-separate
        // checks closes the scene gap — the scene variant never verified the partner kind, so a scene member wired to
        // another member (instead of its scene_link) slipped through. <paramref name="allowUnwired"/> keeps the
        // scene-only leniency (a NullToken link is a legitimate authored state; a follow-link half is never unwired).
        private static void ValidateReciprocity(IReadOnlyList<ProjectElement> elements, IReadOnlySet<string> halfTags,
            string ruleId, string noun, bool allowUnwired, FindingCollector findings)
        {
            var halves = new Dictionary<string, ProjectElement>(StringComparer.Ordinal);
            foreach (ProjectElement element in elements)
            {
                if (halfTags.Contains(element.Tag) && element.GetAttribute("id") is { } id)
                {
                    halves[id] = element;
                }
            }

            foreach (ProjectElement half in halves.Values)
            {
                string? partnerId = half.GetAttribute("link");
                if (partnerId is null || partnerId == ElementId.NullToken)
                {
                    if (allowUnwired)
                    {
                        continue;   // an unwired scene row is a legitimate authored state
                    }
                    findings.Error(ruleId, half,
                        $"{noun} {half.Tag} '{half.GetAttribute("id")}' links to missing {noun} '{partnerId}'");
                    continue;
                }
                if (!halves.TryGetValue(partnerId, out ProjectElement? partner))
                {
                    findings.Error(ruleId, half,
                        $"{noun} {half.Tag} '{half.GetAttribute("id")}' links to missing {noun} '{partnerId}'");
                    continue;
                }
                IReadOnlySet<string> expected = ReciprocalComplements[half.Tag];
                if (!expected.Contains(partner.Tag))
                {
                    findings.Error(ruleId, half,
                        $"{noun} {half.Tag} '{half.GetAttribute("id")}' partner is a {partner.Tag}, "
                        + $"expected {string.Join(" or ", expected)}");
                }
                else if (partner.GetAttribute("link") != half.GetAttribute("id"))
                {
                    findings.Error(ruleId, half,
                        $"{noun} {half.Tag} '{half.GetAttribute("id")}' is not reciprocally linked");
                }
            }
        }

        // The complementary partner kind(s) for each reciprocal half, derived from ReciprocalTags (the single source
        // of truth): a from-half pairs with a to-half and vice versa; a scene member row pairs with a scene_link and a
        // scene_link with any member row.
        private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ReciprocalComplements =
            BuildReciprocalComplements();

        private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildReciprocalComplements()
        {
            static IReadOnlySet<string> Of(params string[] tags) => new HashSet<string>(tags, StringComparer.Ordinal);
            var map = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                [ReciprocalTags.FollowLinkFromTag] = Of(ReciprocalTags.FollowLinkToTag),
                [ReciprocalTags.FollowLinkToTag] = Of(ReciprocalTags.FollowLinkFromTag),
                [ReciprocalTags.SceneLinkTag] = ReciprocalTags.SceneMemberTags,
            };
            foreach (string member in ReciprocalTags.SceneMemberTags)
            {
                map[member] = Of(ReciprocalTags.SceneLinkTag);
            }
            return map;
        }

        // ----- dataline addressing (spec ch. 04: modules 1–128, unique per direction) -----

        private static void ValidateDatalineAddressing(IReadOnlyList<ProjectElement> elements, FindingCollector findings)
        {
            var seen = new Dictionary<(string Direction, long Address), ProjectElement>();
            foreach (ProjectElement element in elements)
            {
                if (element.Tag is not ("dataline_input" or "dataline_output"))
                {
                    continue;
                }
                string? address = element.GetAttribute("address_dataline");
                if (address is null || address == ElementId.NullToken)
                {
                    continue;   // unaddressed (the DTD default) — legal while unconfigured
                }
                if (!HexToken.TryParseValue(address, out long value))
                {
                    findings.Error("dataline-address", element,
                        $"address_dataline='{address}' on '{element.Tag}' is not a _0x hex token");
                    continue;
                }
                if (value < 1 || value > DatalineAddress.MaxAddressValue)
                {
                    findings.Error("dataline-address", element,
                        $"address_dataline='{address}' on '{element.Tag}' is outside the legal 1–{DatalineAddress.MaxAddressValue} module range");
                    continue;
                }
                (string Tag, long value) key = (element.Tag, value);
                if (seen.TryGetValue(key, out ProjectElement? first))
                {
                    findings.Error("dataline-address", element,
                        $"address_dataline='{address}' on '{element.Tag}' '{element.GetAttribute("name")}' duplicates " +
                        $"the address of '{first.GetAttribute("name")}' (addresses are unique per direction)");
                }
                else
                {
                    seen[key] = element;
                }
            }
        }

        // ----- resource_enum typedef/inivalue consistency -----

        private static void ValidateEnumConsistency(IReadOnlyList<ProjectElement> elements,
            Dictionary<string, ProjectElement> idToElement, FindingCollector findings)
        {
            foreach (ProjectElement element in elements)
            {
                if (element.Tag != "resource_enum")
                {
                    continue;
                }
                string? typedef = element.GetAttribute("typedef");
                if (typedef is null || typedef == ElementId.NullToken || !idToElement.TryGetValue(typedef, out ProjectElement? definition))
                {
                    continue;   // absent/null/dangling typedef is covered by the schema IDREF pass
                }
                if (definition.Tag != "enum_definition")
                {
                    findings.Error("enum-typedef", element,
                        $"typedef='{typedef}' on resource_enum '{element.GetAttribute("name")}' references a " +
                        $"<{definition.Tag}>, not an enum_definition");
                    continue;
                }
                string? inivalue = element.GetAttribute("inivalue");
                if (inivalue is null || inivalue == ElementId.NullToken)
                {
                    continue;
                }
                bool found = !definition.Children.IsDefaultOrEmpty
                    && definition.Children.Any(v => v.Tag == "enum_value" && v.GetAttribute("id") == inivalue);
                if (!found)
                {
                    findings.Error("enum-inivalue", element,
                        $"inivalue='{inivalue}' on resource_enum '{element.GetAttribute("name")}' is not a value of " +
                        $"its typedef enum '{definition.GetAttribute("name")}'");
                }
            }
        }

        // ----- root invariants -----

        private static readonly string[] RootChildOrder =
        {
            "modified", "customer_info", "installer_info", "project_info",
            "enum_definitions", "groups", "documentation_modules",
        };

        private static void ValidateRoot(Project project, long maxCounter, FindingCollector findings)
        {
            ProjectElement root = project.Root;

            string? major = root.GetAttribute("version_major");
            if (major is not null && int.TryParse(major, out int majorValue) && majorValue > 4)
            {
                findings.Error("root-version", root,
                    $"version_major='{major}': IHC Visual silently rejects project versions above 4 (spec ch. 10 §10.5)");
            }

            string? luidToken = project.LastUniqueId;
            long lastUniqueId = 0;
            if (luidToken is not null && !HexToken.TryParseValue(luidToken, out lastUniqueId))
            {
                findings.Error("luid-malformed", root,
                    $"last_unique_id '{luidToken}' is not a _0x hex token");
            }
            else if (lastUniqueId > IdAllocator.CounterCeiling)
            {
                findings.Error("luid-ceiling", root,
                    $"last_unique_id '{luidToken}' exceeds the 24-bit id counter ceiling (0xffffff)");
            }
            if (lastUniqueId < maxCounter)
            {
                findings.Error("luid-low", root,
                    $"last_unique_id (0x{lastUniqueId:x}) is below the highest counter present (0x{maxCounter:x})");
            }

            var actual = root.Children.IsDefaultOrEmpty
                ? new List<string>()
                : root.Children.Select(c => c.Tag).ToList();
            if (!actual.SequenceEqual(RootChildOrder))
            {
                findings.Warning("root-children", root,
                    $"the root's children are [{string.Join(", ", actual)}]; a vendor file has the seven fixed " +
                    $"children [{string.Join(", ", RootChildOrder)}] in that order");
            }
        }

        // ----- containment (advisory; the placement model is deliberately permissive where unmodeled) -----

        private static void ValidateContainment(ProjectElement parent, ProjectElement? grandParent, FindingCollector findings)
        {
            if (parent.Children.IsDefaultOrEmpty)
            {
                return;
            }
            foreach (ProjectElement child in parent.Children)
            {
                if (!PlacementRules.CanInsert(parent.Tag, child.Tag, grandParent?.Tag))
                {
                    findings.Warning("containment", child,
                        $"<{child.Tag}> under <{parent.Tag}> is outside the modeled containment rules (spec ch. 03/04/06)");
                }
                ValidateContainment(child, parent, findings);
            }
        }

        // ----- helpers -----

        private static void CollectIdTokens(ProjectElement element, HashSet<string> into)
        {
            foreach (ProjectElement e in element.DescendantsAndSelf())
            {
                if (e.GetAttribute("id") is { } id)
                {
                    into.Add(id);
                }
            }
        }

    }
}
