#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// Which of the three mutually exclusive last-unique-id faults a project has, decided ONCE.
    /// <para>
    /// The three are exclusive on purpose and the exclusivity is a product decision, not an implementation
    /// accident: a token that never parsed must not ALSO report "0x0 is below the highest counter", because that
    /// second sentence is derived from a phantom zero and reads as a distinct fault when it is noise. Deciding
    /// the chain here, once, is what stops three independently-written rules from all firing.
    /// </para>
    /// </summary>
    public enum LastUniqueIdFault
    {
        /// <summary>The high-water mark is well-formed and at least the highest counter present.</summary>
        None,

        /// <summary>Absent, or below the highest counter present — the next minted id would collide.</summary>
        BelowHighWaterMark,

        /// <summary>Not a <c>_0x</c> hex token, so no further id can be minted from it.</summary>
        Malformed,

        /// <summary>Above the 24-bit counter ceiling: the id space is exhausted.</summary>
        AboveCeiling,
    }

    /// <summary>
    /// What ONE walk over the ids establishes, read by eight rules.
    /// <para>
    /// This is the case that justifies a shared analysis at all. The shipped validator computes a token-to-element
    /// map, a counter set and a maximum in a single pass feeding four id rules, then threads the same map into the
    /// per-attribute pass for the dangling-reference rule and the maximum into the high-water-mark rules. Without
    /// a shared home each of those eight rules would re-walk the document, and the two that need the walk's
    /// ORDER — first holder wins — would each have to re-derive it identically.
    /// </para>
    /// <para>
    /// FIRST HOLDER WINS, in document order, and that is load-bearing rather than incidental: it is what makes
    /// "which of these two elements is the duplicate" a stable answer instead of one that depends on which rule
    /// asked first.
    /// </para>
    /// </summary>
    /// <summary>
    /// One id collision, as the reader must see it: the FIRST holder of the colliding value and every other
    /// element that carries it, in document order.
    /// </summary>
    /// <param name="Primary">The first holder — the site a finding is anchored to.</param>
    /// <param name="Related">The other holders, in document order. Never empty for a real collision.</param>
    public sealed record DuplicateIdGroup(ProjectElement Primary, EquatableArray<ProjectElement> Related);

    public interface IIdAnalysis
    {
        /// <summary>Whether any element carries this id token. The dangling-reference rule's whole question.</summary>
        /// <param name="token">The raw <c>_0x</c> token an IDREF attribute names.</param>
        bool IsKnownToken(string token);

        /// <summary>The elements that are NOT the first holder of their id token, in document order.</summary>
        EquatableArray<ProjectElement> DuplicateTokenHolders { get; }

        /// <summary>The elements that are NOT the first holder of their id COUNTER, in document order.</summary>
        EquatableArray<ProjectElement> DuplicateCounterHolders { get; }

        /// <summary>
        /// The same token collisions as <see cref="DuplicateTokenHolders"/>, GROUPED: each collision as its first
        /// holder plus the others. A rule reporting one finding per collision needs the first holder too — it is
        /// the site the reader repairs against — and the two holder lists deliberately do not contain it.
        /// <para>
        /// Derived in the same pass rather than re-grouped by a rule: "first holder wins, in document order" is
        /// stated once here, and a rule re-deriving it would be a second answer to which element is the duplicate.
        /// </para>
        /// </summary>
        EquatableArray<DuplicateIdGroup> DuplicateTokenGroups { get; }

        /// <summary>The counter collisions, grouped the same way. Members share a COUNTER and differ in token.</summary>
        EquatableArray<DuplicateIdGroup> DuplicateCounterGroups { get; }

        /// <summary>The highest id counter any element carries, or 0 when none does.</summary>
        long MaxCounter { get; }

        /// <summary>Which of the three exclusive high-water-mark faults holds, decided once.</summary>
        LastUniqueIdFault LastUniqueId { get; }
    }

    /// <summary>
    /// Where an element SITS: its parent, its nearest ancestor of a given tag, and the element an id token names.
    /// <para>
    /// The project tree is immutable and carries no parent pointers, so any rule that has to answer "which
    /// locality is this pin in" or "which block owns this half" needs a map. Eight wiring rules need exactly that,
    /// which is what makes it a shared analysis rather than a helper inside one of them.
    /// </para>
    /// </summary>
    public interface ITopologyAnalysis
    {
        /// <summary>The element's parent, or null for the root.</summary>
        /// <param name="element">The element to look up.</param>
        ProjectElement? Parent(ProjectElement element);

        /// <summary>
        /// The nearest ancestor with the given tag, or the element itself when it already has it; null when no
        /// ancestor does.
        /// </summary>
        /// <param name="element">Where the walk starts.</param>
        /// <param name="tag">The tag to walk up to.</param>
        ProjectElement? NearestAncestorOrSelf(ProjectElement element, string tag);

        /// <summary>The element carrying this raw <c>id</c> token, or null — first holder wins, as elsewhere.</summary>
        /// <param name="idToken">The raw <c>_0x</c> token an IDREF names.</param>
        ProjectElement? ByToken(string? idToken);
    }

    /// <summary>
    /// The analyses one run computes AT MOST ONCE, which any rule may read.
    /// <para>
    /// NAMED members, not a type-keyed lookup. One rule is one code, which is right for IDENTITY and wrong for
    /// WORK: several rules legitimately share one walk. Naming each analysis also makes the COUNT visible, which
    /// is the thing worth watching — a second lookup-shaped mechanism would hide how much shared state the engine
    /// had grown.
    /// </para>
    /// <para>
    /// It holds exactly what has a consumer. Further analyses arrive with the rules that need them, and an
    /// analysis with no reader is a walk nobody asked for.
    /// </para>
    /// </summary>
    public interface IProjectAnalyses
    {
        /// <summary>Id tokens, counters, the high-water mark and its fault — one walk, feeding eight rules.</summary>
        IIdAnalysis Ids { get; }

        /// <summary>Parent pointers, ancestor walks and id resolution — one walk, feeding the eight wiring rules.</summary>
        ITopologyAnalysis Topology { get; }

        /// <summary>
        /// What every program row touches — one walk, feeding the eleven dataflow rows. Declared once so two rules
        /// cannot disagree about whether <c>%P = %P + 1</c> reads its target.
        /// </summary>
        IProgramUsageAnalysis Usage { get; }

        /// <summary>
        /// Every element in the project, in document order — the walk itself, done ONCE.
        /// <para>
        /// This is the cheapest and least interesting of the analyses and it is the one that mattered most to
        /// measure: T067's benchmark found whole-corpus validation allocating 12.6x its baseline while the rule
        /// population grew 3.2x, because almost every rule opened with its own
        /// <c>Root.DescendantsAndSelf().Where(...)</c> — one tree walk and one iterator chain per rule, per run. The
        /// ids, topology and usage analyses already shared their derived facts; nothing shared the enumeration they
        /// were all derived from.
        /// </para>
        /// <para>
        /// MIGRATION COMPLETE, and re-measured. The first pass converted 9 of the 16 rule files; the remaining 7
        /// — plus <c>WholeProjectValidator</c>'s own scan-order map and its constraint face — were converted
        /// after, removing roughly twenty further whole-document walks. <c>PerfBaselineBenchmark</c>'s
        /// whole-corpus <c>ValidateCategorized</c> allocation went from <b>7562 KB to 6323 KB</b> (−16%) with the
        /// median wall time unchanged inside noise (27.0 ms → 28.3 ms, p95 35.5 ms → 34.0 ms), and the
        /// characterization oracle did not move. Allocation was the target; the walk was never the hot path.
        /// </para>
        /// <para>
        /// ONE CAVEAT for a rule converted later: <see cref="WithTag"/> is order-safe for a SINGLE tag only.
        /// Concatenating two tag buckets emits every element of the first tag before any of the second, and the
        /// executor's sequence tiebreak carries a rule's emission order into its findings — so a rule over two or
        /// more tags filters <see cref="Elements"/> instead.
        /// </para>
        /// </summary>
        EquatableArray<ProjectElement> Elements { get; }

        /// <summary>
        /// Every element carrying the given tag, in document order — the shape most rules actually want, so the
        /// per-rule filter goes away with the per-rule walk. An unknown tag answers empty.
        /// </summary>
        /// <param name="tag">The element tag to select.</param>
        EquatableArray<ProjectElement> WithTag(string tag);
    }

    /// <summary>The analyses over one project, computed lazily and at most once per run.</summary>
    internal sealed class ProjectAnalyses : IProjectAnalyses
    {
        private readonly Lazy<IIdAnalysis> ids;
        private readonly Lazy<ITopologyAnalysis> topology;
        private readonly Lazy<IProgramUsageAnalysis> usage;
        private readonly Lazy<ImmutableArray<ProjectElement>> elements;
        private readonly Lazy<Dictionary<string, ImmutableArray<ProjectElement>>> byTag;

        public ProjectAnalyses(Project project)
        {
            ids = new Lazy<IIdAnalysis>(() => IdAnalysis.Of(project));
            topology = new Lazy<ITopologyAnalysis>(() => TopologyAnalysis.Of(project));

            // The usage walk resolves ids, so it is built ON the topology analysis rather than beside it — one
            // token map for both, and the lazy chain keeps a run that needs neither from paying for either.
            usage = new Lazy<IProgramUsageAnalysis>(() => ProgramUsageAnalysis.Of(project, topology.Value));

            elements = new Lazy<ImmutableArray<ProjectElement>>(
                () => [.. project.Root.DescendantsAndSelf()]);

            // Built on the materialised list rather than a second walk, and grouped rather than filtered per
            // lookup: a rule asking for one tag pays a dictionary hit, not a scan of the whole document.
            byTag = new Lazy<Dictionary<string, ImmutableArray<ProjectElement>>>(() =>
            {
                Dictionary<string, ImmutableArray<ProjectElement>.Builder> groups = new(StringComparer.Ordinal);
                foreach (ProjectElement element in elements.Value)
                {
                    if (!groups.TryGetValue(element.Tag, out ImmutableArray<ProjectElement>.Builder? group))
                    {
                        groups[element.Tag] = group = ImmutableArray.CreateBuilder<ProjectElement>();
                    }

                    group.Add(element);
                }

                return groups.ToDictionary(pair => pair.Key, pair => pair.Value.ToImmutable(), StringComparer.Ordinal);
            });
        }

        public IIdAnalysis Ids => ids.Value;

        public ITopologyAnalysis Topology => topology.Value;

        public IProgramUsageAnalysis Usage => usage.Value;

        public EquatableArray<ProjectElement> Elements => elements.Value;

        public EquatableArray<ProjectElement> WithTag(string tag) =>
            byTag.Value.TryGetValue(tag, out ImmutableArray<ProjectElement> found)
                ? found
                : ImmutableArray<ProjectElement>.Empty;
    }

    /// <summary>
    /// The one walk that records where every element sits.
    /// <para>
    /// KNOWN DUPLICATION, with a reason: <c>Ihc.Vis.Reporting.TreeIndex</c> builds the same two maps for the report
    /// builders. The layer model forbids Validation from depending on Reporting (and the direction cannot be
    /// reversed — a report is a consumer), so the choice was between this twenty-line walk and lifting the
    /// primitive into a lower layer, which would edit the report path for no behavioural gain. The walk is stated
    /// once HERE for every rule that needs it, which is the duplication that actually mattered.
    /// </para>
    /// </summary>
    internal sealed class TopologyAnalysis : ITopologyAnalysis
    {
        private readonly Dictionary<ProjectElement, ProjectElement> parents;
        private readonly Dictionary<string, ProjectElement> byToken;

        private TopologyAnalysis(
            Dictionary<ProjectElement, ProjectElement> parents, Dictionary<string, ProjectElement> byToken)
        {
            this.parents = parents;
            this.byToken = byToken;
        }

        public ProjectElement? Parent(ProjectElement element) =>
            element is not null && parents.TryGetValue(element, out ProjectElement? parent) ? parent : null;

        public ProjectElement? NearestAncestorOrSelf(ProjectElement element, string tag)
        {
            ProjectElement? current = element;
            while (current is not null && current.Tag != tag)
            {
                current = Parent(current);
            }

            return current;
        }

        public ProjectElement? ByToken(string? idToken) =>
            idToken is not null && byToken.TryGetValue(idToken, out ProjectElement? element) ? element : null;

        internal static ITopologyAnalysis Of(Project project)
        {
            // Reference identity, not value equality: two distinct elements can carry identical content, and a
            // value-keyed map would silently give one of them the other's parent.
            Dictionary<ProjectElement, ProjectElement> parents = new(ReferenceEqualityComparer.Instance);
            Dictionary<string, ProjectElement> byToken = new(StringComparer.Ordinal);
            Walk(project.Root, parents, byToken);
            return new TopologyAnalysis(parents, byToken);
        }

        private static void Walk(
            ProjectElement element,
            Dictionary<ProjectElement, ProjectElement> parents,
            Dictionary<string, ProjectElement> byToken)
        {
            if (element.GetAttribute("id") is { } token)
            {
                byToken.TryAdd(token, element);   // first holder wins, as the id analysis decides it
            }

            foreach (ProjectElement child in element.Children)
            {
                parents[child] = element;
                Walk(child, parents, byToken);
            }
        }
    }

    /// <summary>The one walk over the ids.</summary>
    internal sealed class IdAnalysis : IIdAnalysis
    {
        private readonly HashSet<string> tokens;

        private IdAnalysis(
            HashSet<string> tokens,
            EquatableArray<ProjectElement> duplicateTokenHolders,
            EquatableArray<ProjectElement> duplicateCounterHolders,
            EquatableArray<DuplicateIdGroup> duplicateTokenGroups,
            EquatableArray<DuplicateIdGroup> duplicateCounterGroups,
            long maxCounter,
            LastUniqueIdFault lastUniqueId)
        {
            this.tokens = tokens;
            DuplicateTokenHolders = duplicateTokenHolders;
            DuplicateCounterHolders = duplicateCounterHolders;
            DuplicateTokenGroups = duplicateTokenGroups;
            DuplicateCounterGroups = duplicateCounterGroups;
            MaxCounter = maxCounter;
            LastUniqueId = lastUniqueId;
        }

        public EquatableArray<ProjectElement> DuplicateTokenHolders { get; }

        public EquatableArray<ProjectElement> DuplicateCounterHolders { get; }

        public EquatableArray<DuplicateIdGroup> DuplicateTokenGroups { get; }

        public EquatableArray<DuplicateIdGroup> DuplicateCounterGroups { get; }

        public long MaxCounter { get; }

        public LastUniqueIdFault LastUniqueId { get; }

        public bool IsKnownToken(string token) => tokens.Contains(token);

        internal static IdAnalysis Of(Project project)
        {
            HashSet<string> tokens = new(StringComparer.Ordinal);
            HashSet<int> counters = [];
            ImmutableArray<ProjectElement>.Builder duplicateTokens = ImmutableArray.CreateBuilder<ProjectElement>();
            ImmutableArray<ProjectElement>.Builder duplicateCounters = ImmutableArray.CreateBuilder<ProjectElement>();
            long maxCounter = 0;

            // The first holder of each value, so a collision can be reported as ONE finding anchored at the site
            // the reader repairs against. Insertion-ordered, so the groups come out in document order too.
            Dictionary<string, ProjectElement> firstByToken = new(StringComparer.Ordinal);
            Dictionary<int, ProjectElement> firstByCounter = [];
            Dictionary<string, List<ProjectElement>> othersByToken = new(StringComparer.Ordinal);
            Dictionary<int, List<ProjectElement>> othersByCounter = [];
            List<string> collidedTokens = [];
            List<int> collidedCounters = [];

            foreach (ProjectElement element in project.Root.DescendantsAndSelf())
            {
                if (element.GetAttribute("id") is not { } token)
                {
                    continue;
                }

                if (!tokens.Add(token))
                {
                    // A duplicate token is not examined further: its counter and type code are the FIRST holder's
                    // business, and reporting them again would say the same collision twice.
                    duplicateTokens.Add(element);
                    if (!othersByToken.TryGetValue(token, out List<ProjectElement>? sharing))
                    {
                        othersByToken[token] = sharing = [];
                        collidedTokens.Add(token);
                    }

                    sharing.Add(element);
                    continue;
                }

                firstByToken[token] = element;

                if (!ElementId.TryParse(token, out ElementId id))
                {
                    continue;
                }

                if (!counters.Add(id.Counter))
                {
                    duplicateCounters.Add(element);
                    if (!othersByCounter.TryGetValue(id.Counter, out List<ProjectElement>? sharing))
                    {
                        othersByCounter[id.Counter] = sharing = [];
                        collidedCounters.Add(id.Counter);
                    }

                    sharing.Add(element);
                }
                else
                {
                    firstByCounter[id.Counter] = element;
                }

                if (id.Counter > maxCounter)
                {
                    maxCounter = id.Counter;
                }
            }

            return new IdAnalysis(
                tokens,
                duplicateTokens.ToImmutable(),
                duplicateCounters.ToImmutable(),
                [.. collidedTokens.Select(t => new DuplicateIdGroup(firstByToken[t], [.. othersByToken[t]]))],
                [.. collidedCounters.Select(c => new DuplicateIdGroup(firstByCounter[c], [.. othersByCounter[c]]))],
                maxCounter,
                FaultOf(project.LastUniqueId, maxCounter));
        }

        /// <summary>
        /// The three exclusive faults, decided in the order the shipped validator decides them: absent, then
        /// unparseable, then above the ceiling, then below the mark. Order is the whole point — a later branch
        /// derived from an earlier one's failure is the noise this chain exists to prevent.
        /// </summary>
        private static LastUniqueIdFault FaultOf(string? token, long maxCounter)
        {
            if (token is null)
            {
                return maxCounter > 0 ? LastUniqueIdFault.BelowHighWaterMark : LastUniqueIdFault.None;
            }

            if (!HexToken.TryParseValue(token, out long value))
            {
                return LastUniqueIdFault.Malformed;
            }

            if (value > IdAllocator.CounterCeiling)
            {
                return LastUniqueIdFault.AboveCeiling;
            }

            return value < maxCounter ? LastUniqueIdFault.BelowHighWaterMark : LastUniqueIdFault.None;
        }
    }
}
