using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The STRUCTURE rules: the root's fixed children and its version, the modeled containment rules, the
    /// four function-block shape checks, embedded constants, and the vendor program skeleton.
    /// <para>
    /// Some of these are WARNINGS and stay warnings, which is the distinction the severity model rests on: an
    /// unusual root child order, a minor version ahead of the supported one, an unmodeled containment and a
    /// deviant program skeleton all LOAD AND WORK. The
    /// vendor tooling tolerates them, so calling them errors would be this tool asserting a rule the format does
    /// not have.
    /// </para>
    /// <para>
    /// The containment and root-order rules read <c>PlacementRules</c> and one declared child order rather than
    /// re-stating either. The placement model is deliberately permissive where the format is unmodeled, so a rule
    /// that hard-coded its own idea of legal placement would report on files the editor itself will author.
    /// </para>
    /// </summary>
    public static class StructureRules
    {
        /// <summary>The root's seven fixed children, in the order every vendor-authored file carries them.</summary>
        private static readonly ImmutableArray<string> RootChildOrder =
        [
            "modified", "customer_info", "installer_info", "project_info",
            "enum_definitions", "groups", "documentation_modules",
        ];

        /// <summary>
        /// The function block's five containers, derived from the shared section source rather than re-listed, so
        /// the rule cannot silently drift from the definition it is checking against.
        /// </summary>
        private static readonly ImmutableArray<string> FunctionBlockContainers =
            [.. FunctionBlockSections.All.Select(s => s.Container).Append("programs")];

        /// <summary>
        /// The (tag, attribute) pairs that are BLOCK-LOCAL programming references. Locality is a per-pair fact and
        /// not an attribute-name fact: a switch case's <c>link</c> is a local variable reference while a
        /// follow-link half's <c>link</c> is legitimately cross-block, so the name alone cannot decide it.
        /// <para>
        /// <c>internal</c> rather than private so the parity test can build its fixtures FROM this set instead of
        /// restating it. A test that re-declares the pairs it is checking keeps passing when a pair is added here,
        /// which is silent coverage rot in the one test whose whole purpose is parity.
        /// </para>
        /// </summary>
        internal static readonly ImmutableHashSet<(string Tag, string Attribute)> BlockLocalReferences =
        [
            ("event", "link1"), ("event", "link2"),
            ("condition", "link1"), ("condition", "link2"),
            ("action", "link1"), ("action", "link2"),
            ("case_action", "variable"), ("case_action", "value"),
            ("program_case", "link"),
        ];

        /// <summary>The rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "root-children", RootChildren),
                Rule(catalog, "root-version", RootVersion(catalog)),
                Rule(catalog, "root-version-minor", RootVersionMinor(catalog)),
                Rule(catalog, "containment", Containment),
                Rule(catalog, "fb-shape", BlockShape),
                Rule(catalog, "fb-programs", BlockPrograms),
                Rule(catalog, "fb-pin-container", PinContainers),
                Rule(catalog, "fb-local-ref", BlockLocalRefs),
                Rule(catalog, "inline-constant", InlineConstants),
                Rule(catalog, "program-shape", ProgramShape));
        }

        /// <summary>The root's children are not the seven fixed ones in the fixed order. Loads and works; deviates
        /// from every vendor-authored file.</summary>
        private static void RootChildren(IProjectInspection inspection)
        {
            ProjectElement root = inspection.Project.Root;
            string[] actual = [.. root.Children.Select(c => c.Tag)];
            if (!actual.SequenceEqual(RootChildOrder))
            {
                inspection.Report(root, Arguments(
                    ("actual", string.Join(", ", actual)),
                    ("expected", string.Join(", ", RootChildOrder))));
            }
        }

        /// <summary>
        /// A major version above the highest this tool models: opening it would misread content and saving would
        /// destroy it.
        /// <para>THE SUPPORTED MAJOR IS READ FROM THE ENTRY, as <see cref="RootVersionMinor"/> reads it: the
        /// number this predicate compares against is declared data, and both rows bind one declared constant so
        /// the boundary they partition stays a single fact.</para>
        /// <para>An unparseable major is passed over rather than guessed at, exactly as
        /// <see cref="RootVersionMinor"/> passes over an unparseable minor.</para>
        /// </summary>
        /// <param name="catalog">The catalogue the entry, and its declared supported major, are declared in.</param>
        private static ProjectInspection RootVersion(ProblemCatalog catalog)
        {
            int supportedMajor = (int)Threshold(catalog, "root-version", "SupportedVersionMajor");
            return inspection =>
            {
                ProjectElement root = inspection.Project.Root;
                if (root.GetAttribute("version_major") is { } major
                    && int.TryParse(major, out int value)
                    && value > supportedMajor)
                {
                    inspection.Report(root, Arguments(("version", major)));
                }
            };
        }

        /// <summary>
        /// A minor version ahead of the supported one: the file may carry content this model does not know, and
        /// a save can drop it silently.
        /// <para>THE MAJOR GATE IS THE POINT. A major ABOVE the supported one is <see cref="RootVersion"/>'s row,
        /// and a major BELOW it is accepted input — so this row speaks only for a file of the supported major.
        /// Both numbers are read from the entry; neither is written here.</para>
        /// <para>An unparseable minor is passed over rather than guessed at, exactly as
        /// <see cref="RootVersion"/> passes over an unparseable major.</para>
        /// </summary>
        /// <param name="catalog">The catalogue the entry, and both declared bounds, are declared in.</param>
        private static ProjectInspection RootVersionMinor(ProblemCatalog catalog)
        {
            int supportedMajor = (int)Threshold(catalog, "root-version-minor", "SupportedVersionMajor");
            int supportedMinor = (int)Threshold(catalog, "root-version-minor", "SupportedVersionMinor");
            return inspection =>
            {
                ProjectElement root = inspection.Project.Root;
                if (root.GetAttribute("version_major") is not { } major
                    || !int.TryParse(major, NumberStyles.Integer, CultureInfo.InvariantCulture, out int majorValue)
                    || majorValue != supportedMajor)
                {
                    return;
                }

                if (root.GetAttribute("version_minor") is { } minor
                    && int.TryParse(minor, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minorValue)
                    && minorValue > supportedMinor)
                {
                    inspection.Report(root, Arguments(("minor", minor), ("supported", supportedMinor)));
                }
            };
        }

        /// <summary>An element outside the modeled containment rules. Advisory, because the placement model is
        /// deliberately permissive wherever the vendor format is unmodeled.</summary>
        private static void Containment(IProjectInspection inspection) =>
            Walk(inspection.Project.Root, null, inspection);

        private static void Walk(ProjectElement parent, ProjectElement? grandParent, IProjectInspection inspection)
        {
            foreach (ProjectElement child in parent.Children)
            {
                if (!PlacementRules.CanInsert(parent.Tag, child.Tag, grandParent?.Tag))
                {
                    inspection.Report(child, Arguments(("tag", child.Tag), ("parent", parent.Tag)));
                }

                Walk(child, parent, inspection);
            }
        }

        /// <summary>A function block that does not hold exactly the five containers in their fixed order — it
        /// cannot be read or written as a function block at all.</summary>
        private static void BlockShape(IProjectInspection inspection)
        {
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                string[] actual = [.. block.Children.Select(c => c.Tag)];
                if (!actual.SequenceEqual(FunctionBlockContainers))
                {
                    inspection.Report(block, Arguments(
                        ("id", block.GetAttribute("id") ?? "?"),
                        ("expected", string.Join(", ", FunctionBlockContainers)),
                        ("actual", string.Join(", ", actual))));
                }
            }
        }

        /// <summary>A programs container holding something other than a simple program — not the shape the
        /// controller executes.</summary>
        private static void BlockPrograms(IProjectInspection inspection)
        {
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                if (block.FindChild("programs") is not { } programs)
                {
                    continue;
                }

                foreach (ProjectElement program in programs.Children)
                {
                    if (program.Tag != "program_simple")
                    {
                        inspection.Report(block, Arguments(
                            ("id", block.GetAttribute("id") ?? "?"), ("tag", program.Tag)));
                    }
                }
            }
        }

        /// <summary>A pin under the wrong variable container: its direction and kind no longer follow from where
        /// it lives.</summary>
        private static void PinContainers(IProjectInspection inspection)
        {
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                foreach (ProjectElement container in block.Children)
                {
                    foreach (ProjectElement child in container.Children)
                    {
                        if (PlacementRules.PinContainerFor(child.Tag) is { } required && required != container.Tag)
                        {
                            inspection.Report(child, Arguments(
                                ("id", block.GetAttribute("id") ?? "?"),
                                ("tag", child.Tag),
                                ("expected", required),
                                ("actual", container.Tag)));
                        }
                    }
                }
            }
        }

        /// <summary>A programming reference pointing outside its own function block. Programs are block-local by
        /// construction, so such a reference cannot be executed.</summary>
        private static void BlockLocalRefs(IProjectInspection inspection)
        {
            IIdAnalysis ids = inspection.Analyses.Ids;
            ProjectSchemaView view = inspection.Project.SchemaView;

            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                IReadOnlyList<ProjectElement> inBlock = block.DescendantsAndSelf();
                HashSet<string> local = new(StringComparer.Ordinal);
                foreach (ProjectElement element in inBlock)
                {
                    if (element.GetAttribute("id") is { } id)
                    {
                        local.Add(id);
                    }
                }

                foreach (ProjectElement element in inBlock)
                {
                    if (view.TryGet(element.Tag) is not { } schema)
                    {
                        continue;
                    }

                    foreach ((string name, string value) in element.Attrs)
                    {
                        // A reference to an id NO element carries is the dangling-reference rule's business, not
                        // this one's: reporting both would tell the user twice about one broken reference.
                        if (BlockLocalReferences.Contains((element.Tag, name))
                            && schema.FindAttr(name) is { Render: AttrRender.IdRef }
                            && ids.IsKnownToken(value)
                            && !local.Contains(value))
                        {
                            inspection.Report(element, Arguments(
                                ("attribute", name), ("value", value), ("tag", element.Tag)));
                        }
                    }
                }
            }
        }

        /// <summary>An embedded constant that its parent does not reference: the constant is orphaned inside the
        /// node that should own it.</summary>
        private static void InlineConstants(IProjectInspection inspection)
        {
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                foreach (ProjectElement leaf in block.DescendantsAndSelf())
                {
                    if (leaf.Tag is not ("event" or "condition" or "action" or "case_action"))
                    {
                        continue;
                    }

                    string reference = leaf.Tag == "case_action" ? "value" : "link2";
                    foreach (ProjectElement child in leaf.Children)
                    {
                        if (!child.Tag.StartsWith("resource_", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        string? childId = child.GetAttribute("id");
                        string? referenced = leaf.GetAttribute(reference);
                        if (childId is null || childId != referenced)
                        {
                            inspection.Report(leaf, Arguments(
                                ("tag", child.Tag),
                                ("id", childId ?? string.Empty),
                                ("parent", leaf.Tag),
                                ("attribute", reference),
                                ("value", referenced ?? string.Empty)));
                        }
                    }
                }
            }
        }

        /// <summary>A program without the vendor skeleton. Advisory: vendor tooling loads deviants without
        /// complaint.</summary>
        private static void ProgramShape(IProjectInspection inspection)
        {
            // Filtered off the shared walk rather than concatenated from two WithTag buckets: this rule takes TWO
            // tags, and concatenating their buckets would emit every program_simple before every program_sub
            // instead of in document order — which the executor's sequence tiebreak carries into the finding order.
            foreach (ProjectElement program in inspection.Analyses.Elements
                .Where(e => e.Tag is "program_simple" or "program_sub"))
            {
                string[] expected = program.Tag == "program_simple"
                    ? ["events", "actions"]
                    : ["conditions", "actions", "actions"];
                string[] actual = [.. program.Children.Select(c => c.Tag)];
                if (!actual.SequenceEqual(expected))
                {
                    inspection.Report(program, Arguments(
                        ("tag", program.Tag),
                        ("id", program.GetAttribute("id") ?? "?"),
                        ("expected", string.Join(", ", expected)),
                        ("actual", string.Join(", ", actual))));
                }
            }
        }
    }
}
