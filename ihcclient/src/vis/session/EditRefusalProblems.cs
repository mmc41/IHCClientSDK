using Ihc.Vis.Model;
using Ihc.Vis.Problems;

namespace Ihc.Vis.Session
{
    /// <summary>
    /// The coded problems the session layer hands back when it cannot produce what was asked for — the three
    /// conditions where a door used to return a bare <c>bool</c> or an empty result and left the caller to invent
    /// the sentence (T043).
    /// <para>
    /// One typed factory per code, taking that code's declared argument slots as real parameters: a wrong argument
    /// count or type does not compile at the call site, which is the whole arity-and-type gate. Each returns a
    /// problem whose Danish message is already complete, because binding is the PRODUCER's job — a presentation
    /// path renders the message as it stands and never re-derives it.
    /// </para>
    /// <para>
    /// These are <see cref="Problem"/>s rather than <see cref="EditVerdict"/>s because none of them is a verdict on
    /// a command: two are the catalog failing to yield the thing a command would be built from, so there is no
    /// command yet to judge. The sentences live here, beside the codes, for the reason
    /// <see cref="EditRefusalCodes"/> gives — the session layer may know the problem contract and must not know
    /// the validation engine, so it cannot look a label up from the catalogue. A test pins that each sentence and
    /// its catalogue entry's template are the same words.
    /// </para>
    /// </summary>
    public static class EditRefusalProblems
    {
        /// <summary>
        /// No library function block in the catalog declares that master type, so the insert command cannot be
        /// minted (US-018).
        /// </summary>
        /// <param name="masterType">The master type asked for, as given.</param>
        public static Problem LibraryBlockMissing(string masterType) =>
            new(EditRefusalCodes.LibraryBlockMissing,
                LibraryBlockMissingRefusal(masterType),
                EquatableArray.Create<ProblemArgument>([new ProblemArgument("masterType", masterType)]),
                "No library function block in the catalog declares that master type.");

        /// <summary>
        /// The catalog carries no product with that identifier — or carries it more than once and the display name
        /// did not decide, which the resolver refuses rather than guessing (D22).
        /// </summary>
        /// <param name="identifier">The <c>product_identifier</c> asked for, as given.</param>
        public static Problem CatalogProductMissing(string identifier) =>
            new(EditRefusalCodes.CatalogProductMissing,
                CatalogProductMissingRefusal(identifier),
                EquatableArray.Create<ProblemArgument>([new ProblemArgument("identifier", identifier)]),
                "The catalog carries no product with that identifier, or carries it more than once and the "
                + "display name did not decide which.");

        /// <summary>
        /// A required field is blank. The DECISION belongs to <see cref="Validation.BlankPolicy"/>'s constraint and
        /// the SENTENCE to this code, which is what makes the app's three former blank gates one condition with one
        /// answer. It carries no argument: "this field is empty" needs no datum.
        /// </summary>
        public static Problem ValueRequired() =>
            new(EditRefusalCodes.ValueRequired,
                ValueRequiredRefusal,
                EquatableArray<ProblemArgument>.Empty,
                "A field that must carry a value was submitted blank.");

        /// <summary>The case criterion names no state of the switch's enumerator type.</summary>
        /// <param name="value">The criterion the user typed.</param>
        /// <param name="type">The enumerator type the switch is keyed on.</param>
        public static Problem CaseValueNotAState(string value, string type) =>
            new(EditRefusalCodes.CaseValueNotAState,
                CaseValueNotAStateRefusal(value, type),
                EquatableArray.Create<ProblemArgument>(
                    [new ProblemArgument("value", value), new ProblemArgument("type", type)]),
                "The case criterion is not a state of the switch's enumerator type.");

        /// <summary>The target is not the kind of element this command edits, naming what was expected.</summary>
        /// <param name="noun">What the command needed, in Danish, as the guard's own sentence splices it.</param>
        public static Problem TargetWrongKind(string noun) =>
            new(EditRefusalCodes.TargetWrongKind,
                TargetWrongKindRefusal(noun),
                EquatableArray.Create<ProblemArgument>([new ProblemArgument("noun", noun)]),
                "The target exists but is not the kind of element this command edits.");

        /// <summary>The project already holds the one modem it may hold (US-013).</summary>
        public static Problem ModemLimit() =>
            new(EditRefusalCodes.ModemLimit,
                ModemLimitRefusal,
                EquatableArray<ProblemArgument>.Empty,
                "The project already contains a modem and may hold at most one.");

        /// <summary>The Danish sentence for <see cref="LibraryBlockMissing"/>, with its one datum spliced in.</summary>
        internal static string LibraryBlockMissingRefusal(string masterType) =>
            $"Ingen biblioteks-funktionsblok med master type '{masterType}'.";

        /// <summary>The Danish sentence for <see cref="CatalogProductMissing"/>.</summary>
        internal static string CatalogProductMissingRefusal(string identifier) =>
            $"Intet katalogprodukt med identifikator '{identifier}'.";

        /// <summary>The Danish sentence for <see cref="ValueRequired"/>.</summary>
        internal const string ValueRequiredRefusal = "Feltet skal udfyldes.";

        /// <summary>The Danish sentence for <see cref="CaseValueNotAState"/>.</summary>
        internal static string CaseValueNotAStateRefusal(string value, string type) =>
            $"Værdien '{value}' er ikke en tilstand i enumeratortypen '{type}'.";

        /// <summary>The Danish sentence for <see cref="TargetWrongKind"/>, as the shared guards splice it.</summary>
        internal static string TargetWrongKindRefusal(string noun) => $"Målet er ikke {noun}.";

        /// <summary>
        /// The Danish sentence for <see cref="EditRefusalCodes.TargetMissing"/>, as the shared guards splice it —
        /// the peer of <see cref="TargetWrongKindRefusal"/>, and the words the <c>edit.target-missing</c> entry
        /// declares.
        /// <para>
        /// The noun is the SUBJECT of the sentence, so it is definite and capitalized ("Produktet",
        /// "Lokaliteten") — which is not the indefinite form a tag guard names ("en funktionsblok"). A guard that
        /// has only the indefinite noun therefore passes <c>"Målet"</c> and says the true thing about it, rather
        /// than splicing a noun the sentence cannot carry.
        /// </para>
        /// </summary>
        internal static string TargetMissingRefusal(string noun) => $"{noun} findes ikke længere.";

        /// <summary>The subject <see cref="TargetMissingRefusal"/> takes when the guard knows only an indefinite
        /// noun — the same "Målet" the wrong-kind sentence uses, so one guard speaks with one voice.</summary>
        internal const string TargetSubject = "Målet";

        /// <summary>The Danish template for <see cref="EditRefusalCodes.FieldOutOfRange"/> — both bounds.</summary>
        internal const string FieldOutOfRangeRefusal = "Feltet '{field}' skal være mellem {minimum} og {maximum}.";

        /// <summary>The Danish template for <see cref="EditRefusalCodes.FieldBelowMinimum"/> — minimum only.</summary>
        internal const string FieldBelowMinimumRefusal = "Feltet '{field}' skal være mindst {minimum}.";

        /// <summary>The Danish template for <see cref="EditRefusalCodes.FieldAboveMaximum"/> — maximum only.</summary>
        internal const string FieldAboveMaximumRefusal = "Feltet '{field}' skal være højst {maximum}.";

        /// <summary>The Danish template for <see cref="EditRefusalCodes.FieldNotANumber"/> — no number at all.</summary>
        internal const string FieldNotANumberRefusal = "Feltet '{field}' skal være et helt tal. '{value}' er ikke et tal.";

        /// <summary>
        /// The refusal a bounded field earns when what was submitted is not a number, with its sentence already
        /// bound. Beside <see cref="FieldBounds"/> because it answers the other half of the same question: that
        /// one asks whether a number is within its bounds, this one whether there is a number to ask about.
        /// </summary>
        /// <param name="field">The field's caption, as the dialog shows it.</param>
        /// <param name="value">The offending value, as submitted.</param>
        internal static (ProblemCode Code, string Message) FieldNotANumber(string field, string value) =>
            (EditRefusalCodes.FieldNotANumber,
                ProblemTemplate.Bind(FieldNotANumberRefusal,
                [
                    new ProblemArgument("field", field),
                    new ProblemArgument("value", value),
                ]));

        /// <summary>
        /// Which of D05's three bound refusals a submitted number earns, and its sentence already bound — or null
        /// when the number is within its bounds.
        /// <para>
        /// The SHAPE of the field's bounds chooses the code, so each row binds exactly the slots it declares and
        /// no site has to compose prose. The three consts above are the templates the catalogue entries are built
        /// from, which is what lets a drift test compare the two copies; this method binds them.
        /// </para>
        /// <para>
        /// There is deliberately no fourth arm for "neither bound". A field declaring no bound cannot be out of
        /// them, and the caller returns before reaching here — an arm for it would be a sentence nothing could
        /// ever show, which is the defect this split exists to end.
        /// </para>
        /// </summary>
        /// <param name="field">The field's caption, as the dialog shows it.</param>
        /// <param name="minimum">The declared lower bound, when the element declares one.</param>
        /// <param name="maximum">The declared upper bound, when the element declares one.</param>
        /// <param name="number">The submitted value, already parsed.</param>
        internal static (ProblemCode Code, string Message)? FieldBounds(
            string field, int? minimum, int? maximum, int number) =>
            (minimum, maximum) switch
            {
                ({ } min, { } max) when number < min || number > max => (
                    EditRefusalCodes.FieldOutOfRange,
                    ProblemTemplate.Bind(FieldOutOfRangeRefusal,
                    [
                        new ProblemArgument("field", field),
                        new ProblemArgument("minimum", min),
                        new ProblemArgument("maximum", max),
                    ])),
                ({ } min, null) when number < min => (
                    EditRefusalCodes.FieldBelowMinimum,
                    ProblemTemplate.Bind(FieldBelowMinimumRefusal,
                    [
                        new ProblemArgument("field", field),
                        new ProblemArgument("minimum", min),
                    ])),
                (null, { } max) when number > max => (
                    EditRefusalCodes.FieldAboveMaximum,
                    ProblemTemplate.Bind(FieldAboveMaximumRefusal,
                    [
                        new ProblemArgument("field", field),
                        new ProblemArgument("maximum", max),
                    ])),
                _ => null,
            };

        /// <summary>
        /// The Danish sentence for <see cref="ModemLimit"/> — the rule AND its remedy, which is the registered
        /// difference from the reference application's own wording.
        /// </summary>
        internal const string ModemLimitRefusal =
            "Et projekt må højst indeholde ét modem. Fjern det eksisterende modem, før du tilføjer et nyt.";
    }
}
