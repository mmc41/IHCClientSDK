using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CsCheck;
using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Places a catalog product into a real project, composes its dialog, and runs the batch-vs-sequence
    /// metamorphic law over it. Shared setup rather than per-test setup because the same law is swept across the
    /// WHOLE built-in catalog: the base project is parsed once and every product is placed into that one snapshot.
    /// </summary>
    internal static class ProductDialogHarness
    {
        private const string BaseProjectPath = "testdata/projects/project3-KompleksWired.vis";

        // Wide enough to interleave several fields in one submit, small enough that the whole-catalog sweep stays
        // inside the suite's runtime ceiling.
        private const int MaxEditsPerSubmit = 4;

        private static Project? _baseProject;

        internal static ProjectAppService App => new(TestSetup.Settings);

        /// <summary>The shared base project, parsed once. Safe as a plain memo: the suite is
        /// <c>[assembly: NonParallelizable]</c>, and a <see cref="Project"/> is immutable once loaded.</summary>
        internal static async Task<Project> BaseProjectAsync() => _baseProject ??= await App.Load(BaseProjectPath);

        /// <summary>Places <paramref name="definition"/> in the first locality and composes the dialog it would
        /// raise. Placement happens ONCE, here — both metamorphic paths then start from this one snapshot, so the
        /// product's id is identical between them by construction rather than by the allocator happening to agree.
        /// </summary>
        internal static async Task<PlacedProduct> PlaceAsync(ProductDefinition definition)
        {
            Project baseProject = await BaseProjectAsync();
            var session = new ProjectDocumentSession();
            session.Open(baseProject);
            EditOutcome<ElementId> placed = session.Apply(
                new AddProduct(baseProject.Groups.First().Id!.Value, definition));
            return placed.Status == EditStatus.Committed
                ? new PlacedProduct(session.Current!, placed.Value, App.GetProductDialog(session.Current!, placed.Value), null)
                : new PlacedProduct(baseProject, default, null, placed.Reason ?? placed.Status.ToString());
        }

        internal static async Task<PlacedProduct> PlaceAsync(string productIdentifier) =>
            await PlaceAsync(App.GetAvailableProducts().First(p => p.ProductIdentifier == productIdentifier));

        /// <summary>
        /// The law: ONE <see cref="ApplyProductDialog"/> carrying N independent edits must leave the same project
        /// as N single-edit submits of the same edits in the same order. Returns the number of editable fields the
        /// product offered, so a sweep can report what it actually exercised rather than assume.
        /// </summary>
        internal static int CheckBatchEqualsSequence(PlacedProduct placed, long iter)
        {
            ImmutableArray<DialogDescriptorField> fields = placed.EditableFields;
            if (fields.Length > 0)
            {
                Sessions(placed.Project).SampleMetamorphic(
                    EditBatches(fields).Metamorphic<ProjectDocumentSession>(
                        edits => string.Join(" + ", edits.Select(e => $"{e.Attribute}='{e.Value}'")),
                        (session, edits) => SubmitAsOneDialog(session, placed.ProductId, edits),
                        (session, edits) => SubmitOneFieldAtATime(session, placed.ProductId, edits)),
                    equal: SameSerializedBytes,
                    print: Describe,
                    iter: iter,
                    threads: 1);
            }
            return fields.Length;
        }

        // ── the T002 pattern, applied ───────────────────────────────────────────────────────────────
        // Carrier, equality, generator shape and threads:1 all follow CompositeCommandMetamorphicTests; see its
        // class remarks for why each is what it is. Only the two paths and the parameter differ.

        private static Gen<ProjectDocumentSession> Sessions(Project project) =>
            Gen.Const(() =>
            {
                var session = new ProjectDocumentSession();
                session.Open(project);
                return session;
            });

        private static bool SameSerializedBytes(ProjectDocumentSession a, ProjectDocumentSession b) =>
            ProjectSerializer.Serialize(a.Current!).AsSpan()
                .SequenceEqual(ProjectSerializer.Serialize(b.Current!));

        private static string Describe(ProjectDocumentSession session) =>
            session.Current is { } project ? $"luid={project.LastUniqueId}" : "<no project>";

        private static void SubmitAsOneDialog(
            ProjectDocumentSession session, ElementId productId, ProductDialogEdit[] edits) =>
            Submit(session, productId, [.. edits]);

        private static void SubmitOneFieldAtATime(
            ProjectDocumentSession session, ElementId productId, ProductDialogEdit[] edits)
        {
            foreach (ProductDialogEdit edit in edits)
            {
                Submit(session, productId, [edit]);
            }
        }

        /// <summary>
        /// Applies one submit and REFUSES TO IGNORE a refusal. Without this the property has a silent failure mode:
        /// a generated value the dialog rejects leaves both paths' projects untouched, the two compare equal, and
        /// the iteration passes having tested nothing. Every value <see cref="ValueFor"/> produces is supposed to
        /// satisfy its field's own rule and range, so a refusal means the GENERATOR is wrong — which is worth
        /// hearing about loudly rather than as a suspiciously green sweep. (Committed is not assertable instead:
        /// an empty batch, or a value that happens to equal what the field already holds, legitimately reports
        /// NoChange.)
        /// </summary>
        private static void Submit(ProjectDocumentSession session, ElementId productId, ProductDialogEdit[] edits)
        {
            EditOutcome outcome = session.Apply(new ApplyProductDialog(productId, [.. edits]));
            if (outcome.Status == EditStatus.Refused)
            {
                throw new InvalidOperationException(
                    "the dialog refused a generated value, so this iteration would have compared two untouched "
                    + $"projects and proved nothing: {outcome.Reason}");
            }
        }

        /// <summary>
        /// Independent edits by construction (D09): <c>Gen.Shuffle</c> hands out n DISTINCT fields, so no edit can
        /// overwrite a field another edit in the same batch already wrote. That is what makes the two paths
        /// comparable — a batch validates every edit against the PRE-EDIT dialog, so two edits to one field, or an
        /// edit that changes which fields the dialog offers, would legitimately part company.
        /// </summary>
        private static Gen<ProductDialogEdit[]> EditBatches(ImmutableArray<DialogDescriptorField> fields)
        {
            DialogDescriptorField[] pool = [.. fields];
            return Gen.Int[0, Math.Min(MaxEditsPerSubmit, pool.Length)].SelectMany(count =>
                Gen.Select(Gen.Shuffle(pool, count), Gen.Int[0, int.MaxValue].Array[count],
                    (picked, seeds) => Compose(picked, seeds)));
        }

        private static ProductDialogEdit[] Compose(DialogDescriptorField[] picked, int[] seeds)
        {
            var edits = new ProductDialogEdit[picked.Length];
            for (int i = 0; i < edits.Length; i++)
            {
                edits[i] = new ProductDialogEdit(picked[i].Target, picked[i].Attribute, ValueFor(picked[i], seeds[i]));
            }
            return edits;
        }

        // Characters a free-text field may hold. Two alphabets rather than one filtered set, so a whitespace-banning
        // rule is satisfied by CONSTRUCTION — a generate-then-discard loop would quietly thin the sample instead.
        private static readonly char[] TextAlphabet = "abcdeæøå 0129".ToCharArray();
        private static readonly char[] NoWhitespaceAlphabet = "abcdeæøå0129".ToCharArray();

        /// <summary>
        /// A value the field will ACCEPT, derived from one generated int. Built to satisfy the field's own rule and
        /// range rather than generated and filtered: every value the property submits must be legal, or the
        /// property would spend its iterations watching both paths refuse identically and prove nothing.
        /// </summary>
        internal static string ValueFor(DialogDescriptorField field, int seed)
        {
            if (field.Control == DialogControlKind.Checkbox)
            {
                return (seed & 1) == 0 ? "yes" : "no";
            }
            if (field.Control == DialogControlKind.ComboFixed)
            {
                // A CLOSED list, so the sample is drawn FROM it. Free text here would submit values the
                // attribute cannot hold — which is not what this property is about, and which the dialog would
                // never let an installer produce.
                string[] tokens = [.. field.Suggestions];
                return tokens.Length > 0 ? tokens[(int)((uint)seed % (uint)tokens.Length)] : string.Empty;
            }
            if (field.Control == DialogControlKind.Number)
            {
                // long arithmetic: a field declaring a range near int.MaxValue would overflow the span in int and
                // yield a negative modulus — an out-of-range value the dialog then refuses, which is exactly the
                // silent-vacuity case Submit guards against. Cheaper to not produce it in the first place.
                long low = field.Minimum ?? 0;
                long high = field.Maximum is { } declared && declared >= low ? declared : low + 999;
                return (low + (seed % (high - low + 1))).ToString(CultureInfo.InvariantCulture);
            }

            DialogValueRule? rule = field.Rule;
            string prefix = rule?.CountryCodeRequired == true ? "+45" : string.Empty;
            int shortest = Math.Max(rule?.MinLength ?? 1, prefix.Length + 1);
            int longest = Math.Max(Math.Min(rule?.MaxLength ?? 12, 24), shortest);
            char[] alphabet = rule?.WhitespaceAllowed == false ? NoWhitespaceAlphabet : TextAlphabet;

            var body = new char[shortest - prefix.Length + seed % (longest - shortest + 1)];
            uint state = (uint)seed;
            for (int i = 0; i < body.Length; i++)
            {
                state = (state * 1664525) + 1013904223;
                body[i] = alphabet[state % (uint)alphabet.Length];
            }
            return prefix + new string(body);
        }
    }

    /// <summary>A product placed in a project, with the dialog it raises — or the reason it could not be placed.</summary>
    internal sealed record PlacedProduct(
        Project Project, ElementId ProductId, ProductDialogDescriptor? Dialog, string? UnplaceableReason)
    {
        internal bool Placed => UnplaceableReason is null;

        /// <summary>The fields a submit may write: everything the dialog offers that is not read-only. Read-only
        /// fields are excluded because <see cref="ApplyProductDialog"/> refuses them — including them would test
        /// the refusal, not the law.</summary>
        internal ImmutableArray<DialogDescriptorField> EditableFields =>
            Dialog is null ? [] : [.. Dialog.AllFields.Where(f => !f.ReadOnly)];
    }
}
