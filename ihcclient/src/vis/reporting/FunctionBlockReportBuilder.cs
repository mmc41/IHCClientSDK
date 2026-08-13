#nullable enable
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Reporting
{
    /// <summary>
    /// Builds the "Funktionsblok dokumentation" report as a shape document (spec R4/§14): every function
    /// block per flattened locality in document order (U5), each as one <see cref="FbBlockShape"/> — the B7
    /// heading (block <c>@name</c>) and paragraph rules, the four variable sections filtered to the
    /// vendor-scope types, pins with notes and never a value while settings/internal variables carry
    /// <c>= value</c> per the A11 formats incl. B1's real month (A10), the program tree with A12 nesting
    /// (the and/or group icon from the conditions group's <c>type</c>, U6 unknown → and; U7 drops stray
    /// <c>program_sub</c>/<c>program_case</c> directly under <c>programs</c>; a <c>case_action</c> renders
    /// only its action children), and A3/B8 independent <c>%P</c>/<c>%S</c>/<c>%LT</c> statement
    /// substitution. Full mode adds the common meta/Projekt/appendix shapes.
    /// </summary>
    internal static class FunctionBlockReportBuilder
    {
        private static readonly string Title = ReportTitles.For(ReportKind.FunctionBlocks);

        // The vendor-scope variable types the settings/internalsettings sections render (staleness gaps —
        // holiday/humidity/lux/energy — stay omitted in Standard; register C owns their future).
        private static readonly FrozenSet<string> VariableTags = new[]
        {
            "resource_timer", "resource_time", "resource_timertime", "resource_counter", "resource_integer",
            "resource_enum", "resource_date", "resource_weekday", "resource_flag", "resource_temperature",
            "resource_light_level", "resource_floating_point",
        }.ToFrozenSet(StringComparer.Ordinal);

        private static readonly FrozenSet<string> PinTags =
            new[] { "resource_input", "resource_output", "resource_scene" }.ToFrozenSet(StringComparer.Ordinal);

        private static readonly string[] DanishMonths =
        {
            "januar", "februar", "marts", "april", "maj", "juni",
            "juli", "august", "september", "oktober", "november", "december",
        };

        private static readonly FrozenDictionary<string, string> DanishWeekdays = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["monday"] = "mandag",
            ["tuesday"] = "tirsdag",
            ["wednesday"] = "onsdag",
            ["thursday"] = "torsdag",
            ["friday"] = "fredag",
            ["saturday"] = "lørdag",
            ["sunday"] = "søndag",
        }.ToFrozenDictionary(StringComparer.Ordinal);

        private static readonly Regex Placeholder = new("%LT|%P|%S", RegexOptions.Compiled);

        // The label lines the vendor notes carry redundantly under the fixed "Anvendelse" kicker.
        private static readonly FrozenSet<string> DroppedLabelLines =
            new[] { "Anvendelse:", "Anvendes:" }.ToFrozenSet(StringComparer.Ordinal);

        public static ReportShapeDocument Build(Project project, DateTimeOffset generatedAt)
        {
            ArgumentNullException.ThrowIfNull(project);
            var index = new TreeIndex(project.Root);

            var shapes = ImmutableArray.CreateBuilder<ReportShape>();
            shapes.Add(FullModeShapes.MetaLine(project, generatedAt));
            shapes.Add(FullModeShapes.ProjektBlock(project));
            foreach (ProjectElement locality in TreeIndex.Localities(project))
            {
                foreach (ProjectElement block in locality.Children.Where(c => c.Tag == "functionblock"))
                {
                    shapes.Add(BuildBlock(project, block, index));
                }
            }
            shapes.AddRange(FullModeShapes.FindingsAppendix(project, index));
            // The FB report's first shape sits directly under the h1 with no blank separator.
            return new ReportShapeDocument(Title, shapes.ToImmutable(), TitleHugsFirstShape: true);
        }

        private static FbBlockShape BuildBlock(Project project, ProjectElement block, TreeIndex index)
        {
            string name = ReportText.Collapse(block.GetAttribute("name"));
            var rows = ImmutableArray.CreateBuilder<ReportTreeRow>();

            foreach (string container in new[] { "inputs", "outputs", "settings", "internalsettings", "programs" })
            {
                if (block.FindChild(container) is not { } section)
                {
                    continue;
                }
                rows.Add(Row(section, 0, index));
                if (container == "programs")
                {
                    // U7: only program_simple children are programs; stray sub/case elements are dropped.
                    foreach (ProjectElement program in section.Children.Where(c => c.Tag == "program_simple"))
                    {
                        AddProgramRows(program, 1, rows, index);
                    }
                }
                else
                {
                    // The four variable sections render the same vendor-scope type union (pins, scenes and
                    // the classic variable types); only settings/internalsettings add `= value` (A10).
                    bool variables = container is "settings" or "internalsettings";
                    foreach (ProjectElement child in section.Children
                        .Where(c => PinTags.Contains(c.Tag) || VariableTags.Contains(c.Tag)))
                    {
                        rows.Add(Row(child, 1, index, value: variables ? FormatValue(child, index) : null));
                    }
                }
            }
            return new FbBlockShape(name, block.GetAttribute("id"), Identity(project, block, index),
                Paragraphs(name, block.GetAttribute("note")), rows.ToImmutable(), Standalone: true);
        }

        // The Full-only per-block identity grid: locality (nearest group), the library master type/version,
        // and the locked state as Ja/Nej — A1 masthead placeholders for the blanks.
        private static ImmutableArray<KeyValueRow> Identity(Project project, ProjectElement block, TreeIndex index) =>
            ImmutableArray.Create(
                new KeyValueRow("Lokalitet", ReportText.Display(index.LocalityName(block))),
                new KeyValueRow("Type", ReportText.Display(block.GetAttribute("master_type"))),
                new KeyValueRow("Version", ReportText.Display(block.GetAttribute("master_version"))),
                new KeyValueRow("Låst", project.View(block).Locked ? "Ja" : "Nej"));

        // ----- B7 heading/paragraph rules -----

        private static ImmutableArray<FbParagraph> Paragraphs(string blockName, string? note)
        {
            var result = ImmutableArray<FbParagraph>.Empty;
            if (!string.IsNullOrWhiteSpace(note))
            {
                List<List<string>> paragraphs = SplitParagraphs(note);

                // B7: the first line is the vendor's repeated heading only when it EQUALS the block name.
                if (paragraphs.Count > 0 && paragraphs[0].Count > 0 && paragraphs[0][0].Trim() == blockName.Trim())
                {
                    paragraphs[0].RemoveAt(0);
                }
                foreach (List<string> paragraph in paragraphs)
                {
                    paragraph.RemoveAll(line => DroppedLabelLines.Contains(line.Trim()));
                }
                paragraphs.RemoveAll(paragraph => paragraph.Count == 0);

                // The trailing small print: the LAST paragraph is note-styled when more than one remains.
                result = paragraphs
                    .SelectMany((paragraph, i) => paragraph
                        .Select(line => new FbParagraph(line, i == paragraphs.Count - 1 && paragraphs.Count > 1)))
                    .ToImmutableArray();
            }
            return result;
        }

        private static List<List<string>> SplitParagraphs(string note)
        {
            var paragraphs = new List<List<string>> { new() };
            foreach (string raw in note.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                string line = ReportText.Collapse(raw);
                if (line.Length == 0)
                {
                    if (paragraphs[^1].Count > 0)
                    {
                        paragraphs.Add(new List<string>());
                    }
                }
                else
                {
                    paragraphs[^1].Add(line);
                }
            }
            if (paragraphs[^1].Count == 0)
            {
                paragraphs.RemoveAt(paragraphs.Count - 1);
            }
            return paragraphs;
        }

        // ----- program tree (A12) -----

        private static void AddProgramRows(ProjectElement element, int depth, ImmutableArray<ReportTreeRow>.Builder rows, TreeIndex index)
        {
            rows.Add(Row(element, depth, index));
            foreach (ProjectElement child in ProgramChildren(element))
            {
                AddProgramRows(child, depth + 1, rows, index);
            }
        }

        // What each program element renders beneath itself: containers recurse into their statement or
        // group children; a case_action renders ONLY its action children (its embedded value constant is
        // consumed by the value column, and the vendor never rendered deeper structure there).
        private static IEnumerable<ProjectElement> ProgramChildren(ProjectElement element) => element.Tag switch
        {
            "program_simple" or "program_sub" => element.Children
                .Where(c => c.Tag is "events" or "actions" or "conditions"),
            "events" => element.Children.Where(c => c.Tag is "event" or "event_power"),
            "actions" => element.Children
                .Where(c => c.Tag is "action" or "program_sub" or "program_case"),
            "conditions" => element.Children.Where(c => c.Tag is "condition" or "conditions"),
            "program_case" => element.Children.Where(c => c.Tag is "case_action" or "actions"),
            "case_action" => element.Children.Where(c => c.Tag == "action"),
            _ => Enumerable.Empty<ProjectElement>(),
        };

        // ----- rows, statement texts (A3/B8) and values (A10/A11) -----

        // Statement elements render their substituted template (A3); everything else renders its stored
        // name literally — a pin or group whose NAME happens to contain " = " or a placeholder never splits.
        private static readonly FrozenSet<string> StatementTags = new[]
        {
            "event", "event_power", "action", "condition", "program_case", "case_action",
        }.ToFrozenSet(StringComparer.Ordinal);

        private static IconTreeRow Row(ProjectElement element, int depth, TreeIndex index, string? value = null)
        {
            string name;
            if (StatementTags.Contains(element.Tag))
            {
                (name, value) = StatementText(element, index);
            }
            else
            {
                name = ReportText.Collapse(element.GetAttribute("name"));
                // A sub-program's note renders inline after its name (witnessed: "Under program
                // (Funktions beskrivelse)") — program_case/program_simple notes never do.
                if (element.Tag == "program_sub" && ReportText.Collapse(element.GetAttribute("note")) is { Length: > 0 } subNote)
                {
                    name += " (" + subNote + ")";
                }
            }
            return new IconTreeRow(depth,
                ReportIconKeys.ForElement(element) ?? "unknown",
                name,
                value,
                NoteOf(element),
                IdToken: null);   // FB tree rows never carry the Full id chip (oracle-witnessed); only the heading does
        }

        // The note column: pins/scenes and variables show their own @note; groups and statements never do.
        private static string? NoteOf(ProjectElement element)
        {
            string? note = null;
            if (PinTags.Contains(element.Tag) || VariableTags.Contains(element.Tag))
            {
                string text = ReportText.Collapse(element.GetAttribute("note"));
                note = text.Length > 0 ? text : null;
            }
            return note;
        }

        // A3/B8: the stored name is a template; %P (link1), %S (link2: embedded constant → its VALUE,
        // sibling reference → its NAME) and %LT (the case selector, @link) substitute independently.
        // A template containing " = " splits into name/value at its first occurrence; a case_action row
        // is "<name> <selector>" with the embedded constant's value.
        private static (string Name, string? Value) StatementText(ProjectElement element, TreeIndex index)
        {
            string template = ReportText.SingleLine(element.GetAttribute("name"));
            (string Name, string? Value) result;
            if (element.Tag == "case_action")
            {
                ProjectElement? selector = index.ById(element.GetAttribute("variable"));
                ProjectElement? constant = index.ById(element.GetAttribute("value"));
                result = (ReportText.Collapse(template + " " + ReportText.Collapse(selector?.GetAttribute("name"))),
                    constant is null ? null : FormatValue(constant, index));
            }
            else
            {
                // Each placeholder KIND substitutes its FIRST occurrence only (oracle-witnessed:
                // "%P = %P + 1" renders "Sekund = %P + 1") — the consumed-state spans the name/value split.
                var consumed = new HashSet<string>(StringComparer.Ordinal);
                int split = template.IndexOf(" = ", StringComparison.Ordinal);
                result = split < 0
                    ? (ReportText.Collapse(Substitute(template, element, index, consumed)), null)
                    : (ReportText.Collapse(Substitute(template[..split], element, index, consumed)),
                       ReportText.Collapse(Substitute(template[(split + 3)..], element, index, consumed)));
            }
            return result;
        }

        private static string Substitute(string template, ProjectElement statement, TreeIndex index, HashSet<string> consumed) =>
            Placeholder.Replace(template, match =>
            {
                string result;
                if (!consumed.Add(match.Value))
                {
                    result = match.Value;   // later occurrences of the same kind stay literal (vendor-witnessed)
                }
                else
                {
                    string? substituted = match.Value switch
                    {
                        "%P" => index.ById(statement.GetAttribute("link1"))?.GetAttribute("name"),
                        "%LT" => index.ById(statement.GetAttribute("link"))?.GetAttribute("name"),
                        _ => SecondOperand(statement, index),
                    };
                    // Referenced names substitute collapsed/trimmed — a stored trailing space would
                    // otherwise smuggle a double space into the statement text ("Tænd  -> ON").
                    result = ReportText.Collapse(substituted);
                }
                return result;
            });

        private static string? SecondOperand(ProjectElement statement, TreeIndex index)
        {
            string? operand = null;
            if (index.ById(statement.GetAttribute("link2")) is { } target)
            {
                operand = ReferenceEquals(index.Parent(target), statement)
                    ? FormatValue(target, index)    // embedded inline constant → its value
                    : target.GetAttribute("name");  // sibling reference → its name
            }
            return operand;
        }

        // A11 value formats (B1: the REAL month), keyed by variable type.
        private static string? FormatValue(ProjectElement variable, TreeIndex index) => variable.Tag switch
        {
            "resource_timer" or "resource_timertime" =>
                $"{Int(variable, "hour"):00}:{Int(variable, "minute"):00}:{Int(variable, "second"):00},{Int(variable, "millisecond"):000}",
            "resource_time" => $"{Int(variable, "hour"):00}:{Int(variable, "minute"):00}:{Int(variable, "second"):00}",
            "resource_enum" => ReportText.Collapse(index.ById(variable.GetAttribute("inivalue"))?.GetAttribute("name")),
            "resource_date" => Int(variable, "day").ToString(CultureInfo.InvariantCulture) + ". " + MonthName(variable),
            "resource_weekday" => DanishWeekdays.TryGetValue(variable.GetAttribute("inivalue") ?? "monday", out string? day)
                ? day
                : ReportText.Collapse(variable.GetAttribute("inivalue")),
            "resource_flag" or "resource_holiday" => variable.GetAttribute("inivalue") == "on" ? "On" : "Off",
            "resource_temperature" => Ini(variable, "0.00") + " C",
            "resource_light_level" => Ini(variable, "0") + "%",
            "resource_floating_point" => Ini(variable, "0.00"),
            _ => Ini(variable, "0"),
        };

        private static string MonthName(ProjectElement date)
        {
            int month = Int(date, "month");
            return month is >= 1 and <= 12
                ? DanishMonths[month - 1]
                : ReportText.SingleLine(date.GetAttribute("month"));
        }

        // The display default when a variable carries no inivalue — per-type ("0.00" for the decimal
        // family, "0" otherwise), matching what the vendor renderer showed for untouched variables.
        private static string Ini(ProjectElement variable, string absentDefault) =>
            ReportText.Collapse(variable.GetAttribute("inivalue") ?? absentDefault);

        private static int Int(ProjectElement element, string attribute) =>
            int.TryParse(element.GetAttribute(attribute), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
    }
}
