using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Validation
{
    /// <summary>How a program touches a variable. The three kinds the format distinguishes, and nothing else.</summary>
    public enum VariableUsageKind
    {
        /// <summary>The program STARTS when this variable changes — an <c>event</c>'s first operand.</summary>
        Trigger,

        /// <summary>The program reads the variable's value: a condition's operands, an assignment's source.</summary>
        Read,

        /// <summary>The program assigns the variable — a command's target.</summary>
        Write,
    }

    /// <summary>One program touching one variable, in one way. The unit of the shared program read model.</summary>
    /// <param name="Program">The <c>program_simple</c>/<c>program_sub</c>/<c>program_case</c> the row sits in.</param>
    /// <param name="Row">The <c>event</c>/<c>condition</c>/<c>action</c>/<c>case_action</c> row itself.</param>
    /// <param name="Variable">The variable the row names.</param>
    /// <param name="Kind">Whether the row triggers on, reads or writes it.</param>
    public sealed record VariableUsage(
        ProjectElement Program,
        ProjectElement Row,
        ProjectElement Variable,
        VariableUsageKind Kind);

    /// <summary>
    /// One case branch's test, resolved: the branch, the switch variable it tests, the inline operand holding the
    /// tested value, and that value's token.
    /// </summary>
    /// <param name="Branch">The <c>case_action</c> row.</param>
    /// <param name="Switch">The switch variable, when its reference resolves.</param>
    /// <param name="Operand">The inline element the branch's <c>value</c> names, when it resolves.</param>
    /// <param name="ValueToken">The value the operand stores — an <c>enum_value</c> token, or a literal.</param>
    public sealed record CaseTest(
        ProjectElement Branch,
        ProjectElement? Switch,
        ProjectElement? Operand,
        string? ValueToken);

    /// <summary>
    /// THE SHARED PROGRAM READ MODEL: which variables each program triggers on, reads and writes, plus what the
    /// case branches test. Eleven of the catalogue's LOG rows are dataflow questions, and without one stated model
    /// each of them invents its own traversal — at which point two rules can disagree about whether
    /// <c>%P = %P + 1</c> reads its target, and nothing in the build would notice.
    ///
    /// <para><b>THE SEMANTICS, declared once and derived from the format rather than assumed:</b></para>
    /// <list type="bullet">
    /// <item><c>event/@link1</c> — TRIGGER. The program starts when this variable changes.</item>
    /// <item><c>event/@link2</c> — READ. The value the transition is compared against (<c>%P -&gt; %S</c>).</item>
    /// <item><c>condition/@link1</c>, <c>condition/@link2</c> — READ. Both sides of a test are read.</item>
    /// <item><c>action/@link1</c> — WRITE, and ALSO a read when the row is self-modifying.</item>
    /// <item><c>action/@link2</c> — READ. The source of an assignment (<c>%P = %S</c>).</item>
    /// <item><c>case_action/@variable</c> — READ. A switch reads the variable it switches on.</item>
    /// </list>
    ///
    /// <para><b>Self-modifying commands are recognised from the FILE, not from a method table.</b> A program row
    /// stores the vendor's name TEMPLATE verbatim with <c>%P</c>/<c>%S</c> still live (that is what lets the row
    /// re-render after a rename), so <c>%P = %P + %S</c> announces in the file that it reads its own target. Using
    /// the template beats keying on the method token: the corpus carries four arithmetic tokens
    /// (<c>_0x5f</c>, <c>_0x69</c>, <c>_0x73</c>, <c>_0x7d</c>) that <see cref="Programs.ProgramMethodCatalog"/>
    /// does not model, and a token-keyed reading would have silently missed 26 of the corpus's reads.</para>
    ///
    /// <para><b>What the model deliberately does NOT decide:</b> whether a relative command such as
    /// <i>Kip %P</i> (toggle) or <i>Regulér %P op</i> (dim up) reads its target. Their templates mention the target
    /// once, so the model counts them as writes alone. Every consumer that would be affected is a row about the
    /// block's own state variables, and no corpus variable is touched only by a relative command — measured, not
    /// assumed.</para>
    /// </summary>
    public interface IProgramUsageAnalysis
    {
        /// <summary>Every usage in the project, in document order.</summary>
        EquatableArray<VariableUsage> Usages { get; }

        /// <summary>Every case branch's resolved test, in document order.</summary>
        EquatableArray<CaseTest> CaseTests { get; }

        /// <summary>Whether any program starts on this variable.</summary>
        /// <param name="variable">The variable to ask about.</param>
        bool IsTriggeredOn(ProjectElement variable);

        /// <summary>Whether any program reads this variable's value.</summary>
        /// <param name="variable">The variable to ask about.</param>
        bool IsRead(ProjectElement variable);

        /// <summary>Whether any program assigns this variable.</summary>
        /// <param name="variable">The variable to ask about.</param>
        bool IsWritten(ProjectElement variable);

        /// <summary>
        /// Whether the variable owns a follow-link or scene-link half — the value leaves or arrives outside the
        /// programs, so "no program touches it" does not make it dead.
        /// </summary>
        /// <param name="variable">The variable to ask about.</param>
        bool IsLinked(ProjectElement variable);

        /// <summary>The usages of one program, in document order.</summary>
        /// <param name="program">The program to ask about.</param>
        EquatableArray<VariableUsage> Of(ProjectElement program);

        /// <summary>
        /// Whether a command row READS its own write target as well as writing it — <c>%P = %P + 1</c> does,
        /// <c>%P = 0</c> does not.
        /// <para>
        /// On the model rather than in each rule, for the reason the model exists: the analysis already applies
        /// this test when it decides whether an <c>action</c> contributes a Read beside its Write, and a rule
        /// carrying its own copy is how two rules come to disagree about one row with nothing in the build
        /// noticing.
        /// </para>
        /// </summary>
        /// <param name="row">The command row to ask about.</param>
        bool IsSelfModifying(ProjectElement row);
    }

    /// <summary>The one walk over every program row that answers the dataflow questions.</summary>
    internal sealed class ProgramUsageAnalysis : IProgramUsageAnalysis
    {
        /// <summary>The three program kinds a row can sit in.</summary>
        private static readonly ImmutableHashSet<string> ProgramTags =
            ["program_simple", "program_sub", "program_case"];

        private readonly ImmutableArray<VariableUsage> usages;
        private readonly ImmutableArray<CaseTest> caseTests;
        private readonly HashSet<string> triggered = new(StringComparer.Ordinal);
        private readonly HashSet<string> read = new(StringComparer.Ordinal);
        private readonly HashSet<string> written = new(StringComparer.Ordinal);

        private ProgramUsageAnalysis(
            ImmutableArray<VariableUsage> usages,
            ImmutableArray<CaseTest> caseTests)
        {
            this.usages = usages;
            this.caseTests = caseTests;
            foreach (VariableUsage usage in usages)
            {
                if (usage.Variable.GetAttribute("id") is not { Length: > 0 } id)
                {
                    continue;
                }

                switch (usage.Kind)
                {
                    case VariableUsageKind.Trigger:
                        triggered.Add(id);
                        break;
                    case VariableUsageKind.Read:
                        read.Add(id);
                        break;
                    default:
                        written.Add(id);
                        break;
                }
            }
        }

        public EquatableArray<VariableUsage> Usages => usages;

        public EquatableArray<CaseTest> CaseTests => caseTests;

        /// <summary>Walks every program row once and records what it touches.</summary>
        /// <param name="elements">Every element in document order — the walk the run already materialised.</param>
        /// <param name="topology">The topology analysis, for id resolution and the enclosing program.</param>
        public static ProgramUsageAnalysis Of(
            ImmutableArray<ProjectElement> elements, ITopologyAnalysis topology)
        {
            ArgumentNullException.ThrowIfNull(topology);

            var usages = ImmutableArray.CreateBuilder<VariableUsage>();
            var tests = ImmutableArray.CreateBuilder<CaseTest>();
            foreach (ProjectElement element in elements)
            {
                // The tag test comes FIRST: only these four tags reach the switch, and Enclosing walks the parent
                // chain to the root, so asking it about every element in the document was an O(depth) lookup
                // discarded for the ~95% that are not program rows.
                if (element.Tag is not ("event" or "condition" or "action" or "case_action"))
                {
                    continue;
                }

                if (Enclosing(topology, element) is not { } program)
                {
                    continue;
                }

                switch (element.Tag)
                {
                    case "event":
                        Add(usages, topology, program, element, "link1", VariableUsageKind.Trigger);
                        Add(usages, topology, program, element, "link2", VariableUsageKind.Read);
                        break;
                    case "condition":
                        Add(usages, topology, program, element, "link1", VariableUsageKind.Read);
                        Add(usages, topology, program, element, "link2", VariableUsageKind.Read);
                        break;
                    case "action":
                        Add(usages, topology, program, element, "link1", VariableUsageKind.Write);
                        if (SelfModifying(element))
                        {
                            Add(usages, topology, program, element, "link1", VariableUsageKind.Read);
                        }

                        Add(usages, topology, program, element, "link2", VariableUsageKind.Read);
                        break;
                    case "case_action":
                        Add(usages, topology, program, element, "variable", VariableUsageKind.Read);
                        ProjectElement? operand = topology.ByToken(element.GetAttribute("value"));
                        tests.Add(new CaseTest(
                            element,
                            topology.ByToken(element.GetAttribute("variable")),
                            operand,
                            operand?.GetAttribute("inivalue")));
                        break;
                    default:
                        break;
                }
            }

            return new ProgramUsageAnalysis(usages.ToImmutable(), tests.ToImmutable());
        }

        public bool IsTriggeredOn(ProjectElement variable) => Has(triggered, variable);

        public bool IsRead(ProjectElement variable) => Has(read, variable);

        public bool IsWritten(ProjectElement variable) => Has(written, variable);

        public bool IsLinked(ProjectElement variable)
        {
            ArgumentNullException.ThrowIfNull(variable);
            return variable.Children.Any(c => ReciprocalTags.CrossBoundaryHalfTags.Contains(c.Tag));
        }

        public EquatableArray<VariableUsage> Of(ProjectElement program) =>
            [.. usages.Where(u => ReferenceEquals(u.Program, program))];

        public bool IsSelfModifying(ProjectElement row)
        {
            ArgumentNullException.ThrowIfNull(row);
            return SelfModifying(row);
        }

        /// <summary>
        /// Whether the row reads its own target as well as writing it, read off the vendor template the row stores:
        /// <c>%P = %P + %S</c> names the target twice, <c>%P = %S</c> once. The one place this is decided; every
        /// caller outside this walk goes through <see cref="IProgramUsageAnalysis.IsSelfModifying"/>.
        /// </summary>
        private static bool SelfModifying(ProjectElement action)
        {
            string name = action.GetAttribute("name") ?? string.Empty;
            int first = name.IndexOf("%P", StringComparison.Ordinal);
            return first >= 0 && name.IndexOf("%P", first + 2, StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// The NEAREST enclosing program, or null when the element is not inside one — walked one parent at a time
        /// rather than by asking for each program tag in turn, because "the nearest of three tags" is not what
        /// three separate ancestor queries answer: a command inside a sub-program would be attributed to the
        /// enclosing <c>program_simple</c> or to the sub-program depending on which tag happened to be asked about
        /// first. Found by arming: a seeded change to the top-level attribution left its test green, because the
        /// attribution was already landing on the parent.
        /// </summary>
        private static ProjectElement? Enclosing(ITopologyAnalysis topology, ProjectElement row)
        {
            for (ProjectElement? current = row; current is not null; current = topology.Parent(current))
            {
                if (ProgramTags.Contains(current.Tag))
                {
                    return current;
                }
            }

            return null;
        }

        private static void Add(
            ImmutableArray<VariableUsage>.Builder usages,
            ITopologyAnalysis topology,
            ProjectElement program,
            ProjectElement row,
            string attribute,
            VariableUsageKind kind)
        {
            if (topology.ByToken(row.GetAttribute(attribute)) is { } variable)
            {
                usages.Add(new VariableUsage(program, row, variable, kind));
            }
        }

        private static bool Has(HashSet<string> set, ProjectElement variable)
        {
            ArgumentNullException.ThrowIfNull(variable);
            return variable.GetAttribute("id") is { Length: > 0 } id && set.Contains(id);
        }
    }
}
