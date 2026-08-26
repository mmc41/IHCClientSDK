# Problem catalogue — authoring requirements

What you must supply to add a new item to the problem catalogue: a **fatal error**, an **error**, a
**warning**, an **information item**, or a **host operation outcome**. This is a requirements spec for the
DATA and its formats — it is not an inventory, and nothing here needs updating when a code is added.

The compiled declarations are the truth:

- **SDK** — [`ihcclient/src/vis/validation/ProblemCatalogEntries.*.cs`](../../../ihcclient/src/vis/validation/),
  every condition about a `.vis` project, a `.def`/`.ifb` definition, or an SDK operation.
- **This app** — [`Services/HostProblemCatalog.cs`](../Services/HostProblemCatalog.cs), the reserved
  `app.openvisual.*` family.

Per-row evidence, rationale and the generated row index live in the SDK's master artifact,
[`ihcclient/docs/problem-catalogue.md`](../../../ihcclient/docs/problem-catalogue.md). Read it before
classifying a row; edit the declarations, never its generated tables.

---

## 1. Which catalogue owns the item

A code whose first dotted segment is `app` is host-owned; every other code is the SDK's, and
`ProblemCode.IsHostOwned` is the one predicate that answers it.

| You are adding | Owner | Declare in | May set |
| --- | --- | --- | --- |
| A statement about a `.vis` project (error / warning / information) | SDK | `ProblemCatalogEntries.ProjectFindings.cs` | category, disposition, faces, target, shape, slots |
| A statement about a `.def`/`.ifb` definition file | SDK | `ProblemCatalogEntries.CatalogDefinitions.cs` | same |
| A condition that refuses an SDK operation (fatal) | SDK | `ProblemCatalogEntries.ProjectFindings.cs` (the cause) and/or `ProblemCatalogEntries.cs` (the operation head) | category on the cause only |
| A precondition that refuses one edit command | SDK | `ProblemCatalogEntries.EditRefusals.cs` | nothing but slots |
| An outcome of an action **this app** could not carry through | This app | `Services/HostProblemCatalog.cs` | slots only |

**A host authors no findings.** A finding is a statement about the `.vis` file, the SDK owns the file, and a
second opinion minted in an app is how two catalogues start disagreeing. Every `app.openvisual.*` entry is an
operation outcome with no category, no severity and no face; `HostProblemCatalogTests.AHostAuthoredFindingIsRejected`
fails any entry that breaks this, and the entry it rejects is schema-legal — the rule is about ownership.

---

## 2. The item kinds and the axes each one sets

| Item | `Section` | `Category` | `Disposition` | `Kind` | `Faces` | `Severity` (derived) |
| --- | --- | --- | --- | --- | --- | --- |
| **Fatal error** — cause | `ProjectFindings` / `CatalogDefinitionFindings` | one of eight | `Refusal` | `OperationOutcome` | `None` | — |
| **Fatal error** — operation head | `OperationOutcomes` | `null` | `Refusal` | `OperationOutcome` | `None` | — |
| **Edit precondition** | `OperationOutcomes` | `null` | `Refusal` | `EditPrecondition` | `None` | — |
| **Error** | `ProjectFindings` / `CatalogDefinitionFindings` | one of eight | `Error` | `UserContentRule` or `SchemaSerializationGuard` | at least one | `Error` |
| **Warning** | same | one of eight | `Warning` | same | at least one | `Warning` |
| **Information** | same | one of eight | `Info` — see [§11](#11-the-four-list-tiers-and-what-each-still-needs) | same | at least one | `Info` |
| **Host outcome** | `OperationOutcomes` | `null` | `Refusal` | `OperationOutcome` | `None` | — |

Five consequences of this table are load-bearing:

- **`Fatal` is not a value.** There is no `Fatal` member anywhere in the code. "Fatal error" is the name for the
  `Refusal` disposition, and `CatalogDisposition`'s own rationale states what that axis measures: **the
  operation cannot proceed**. It never means *catastrophic in effect* — a dangling IDREF, a 24-bit id wrap — for
  which the ordinary `Error` is right, because the file still opens and the user must be able to repair it.
- **Fatal is fatal to an OPERATION, and WHICH operation is a separate fact.** The heads are `io.load`, `io.save`,
  `edit.open`, `import.catalog`, `bridge.download` and `bridge.upload`. A row can therefore be fatal to sending
  the project to the controller, or to opening it for editing, while the project itself opens, edits and saves
  normally. `attr-undeclared` is the live case: it refuses the save **and** the edit-open, and reports as an
  Error in between. A row that blocked only `bridge.upload` — the project is fine as a file, and the controller
  will not take it — is the same shape. problem-catalogue.md §2 defines the term against the FILE
  lifecycle alone — *cannot be opened, saved, exported or imported* — which is that document's published
  wording, not the limit of what a refusal can block. Which operations a row blocks is the fact an entry still
  cannot declare; see [§11.1](#111-fatal--declare-what-a-row-blocks-do-not-add-a-severity).
- **An edit precondition is a `Refusal` too, and is not one of those.** It refuses ONE command — *the target no
  longer exists*, *the target is not the kind this command edits* — and leaves the file operations and the
  transfers alone. Same disposition, same `Faces.None`, a different blast radius: declare it in
  `ProblemCatalogEntries.EditRefusals.cs` and do not reach for the operation-head shape.
- **A row that both refuses and reports is declared `Error`.** `attr-undeclared` reports at validate and
  refuses the save; its refusal comes from the operation's own entry with this row as the cause, which is why
  the disposition axis needs no fourth value.
- **`Severity` is derived from `Disposition`**, never declared, so the two cannot disagree. `Refusal` has no
  severity, and no severity means "refused".

Together these mean a findings LIST has three tiers today, not four, and only two of them can be populated.
[§11](#11-the-four-list-tiers-and-what-each-still-needs) states what a four-tier list needs.

### The eight categories

Classification only — there is no per-category configuration surface. Required on every finding row,
forbidden on every operation-outcome row (a biconditional the invariants check).

| `ValidationCategory` | Code | Covers |
| --- | --- | --- |
| `FileIntegrity` | INT | container, encoding, XML/DTD, ids, IDREFs, schema conformance, root invariants, the open/save/import/export operations |
| `Wiring` | WIR | follow-links between products and function blocks |
| `Logic` | LOG | function-block shape, programs, variables, flags, timers, enums |
| `Scenes` | SCN | scene resources and their member rows |
| `Addressing` | ADR | data-line addresses, wireless binding, dimmer channels, meters, modem |
| `DeviceSettings` | DEV | dimmer, shutter, backup, initial-value and accessibility settings |
| `Documentation` | DOC | names, identification codes, cable data, placement, report completeness |
| `ProjectStructure` | PRJ | localities, orphan blocks, housekeeping, controller fit |

A **DOC** row is additionally rendered as the appendix of the Fuld reports, so it moves report oracles —
see [§10](#10-gates-a-new-item-must-pass).

---

## 3. Required data — every field of an entry

`ProblemCatalogEntry` is a positional record. Supply the first ten in this order; the eleventh has a default.

| # | Field | Type | Format and allowed values |
| --- | --- | --- | --- |
| 1 | `Code` | `ProblemCode` | See [§4](#4-the-identifier). |
| 2 | `Section` | `ProblemCatalogSection` | `ProjectFindings`, `CatalogDefinitionFindings`, `OperationOutcomes`. |
| 3 | `Category` | `ValidationCategory?` | One of the eight, or `null`. Non-null **exactly when** `Section` is not `OperationOutcomes`. |
| 4 | `Disposition` | `CatalogDisposition` | `Error`, `Warning`, `Refusal` (and `Info` once [§11.2](#112-information--catalogdisposition-needs-a-fourth-member) lands). |
| 5 | `Kind` | `RuleKind` | `UserContentRule`, `SchemaSerializationGuard`, `EditPrecondition`, `OperationOutcome`. |
| 6 | `Faces` | `RuleFaces` | `None` for anything realised at a throw site; `WholeProject` and/or `DialogMetadata` for a registered rule. A registered rule declaring `None` is refused. |
| 7 | `Target` | `RuleTarget` | `(tag, attribute)` — e.g. `new RuleTarget("product", "address")`. `default` means the project as a whole. Rejected when the schema registry knows the tag and not the attribute. |
| 8 | `Shape` | `FindingShape` | `OneFinding` (one repair clears everything), `OnePerOccurrence` (the usual choice for a content row — write it out, because the enum's zero value is `OneFinding` and `default` here silently means that), `PrimaryWithRelated` (one repair, but the user must see every site). |
| 9 | `Slots` | `EquatableArray<ProblemArgumentSlot>` | See [§6](#6-declared-argument-slots). `default` when the sentence needs no data. |
| 10 | `MessageTemplate` | `string` | See [§5](#5-the-danish-message-template). |
| 11 | `Status` | `ProblemCodeStatus` | Optional, `Active` by default. See [§12](#12-changing-retiring-and-ruling-out). |

Then the init-only fields:

| Field | Type | Required | Format |
| --- | --- | --- | --- |
| `Diagnostic` | `string?` | Yes in practice | The **English** engine sentence. Goes to the log, never to a user. Binds the same `{slot}` names as the Danish template. |
| `Evidence` | `EvidenceMark` | Recommended | `Unknown`, `Authored` (the condition is reachable — produced against the live vendor tool or observed in a real installation), `Refused` (the vendor tool will not author it, so the state arrives only by import or by hand). Metadata, never a gate. |
| `Thresholds` | `EquatableArray<DeclaredThreshold>` | When the predicate compares a number | See [§7](#7-thresholds). |
| `RequiresControllerLimits` | `bool` | When the row needs a target controller's capability limits | Such a rule is absent from the default project-only profile: it does not run and does not report, rather than guessing. |
| `RequiresLibrary` | `bool` | When the row can only be decided against the library a placed block claims | Same posture: skipped when no `ILibraryBlockSource` was supplied. |
| `RefusedOperations` | — | **Not implemented** | Which operations the row blocks. Published today as §4's **Blocks** column (`Open`, `Save · Export`, `Import`, `Export`) in prose that nothing generates and nothing checks — and that column is narrower than the operation heads, so a row also refusing `edit.open` or a controller transfer has nowhere to say so. Required before a Fatal list tier can exist — see [§11](#11-the-four-list-tiers-and-what-each-still-needs). |

---

## 4. The identifier

| Family | Prefix | Used for |
| --- | --- | --- |
| Validation | *none* — bare kebab-case | Every catalogue row about file content: `name-empty`, `addr-dimmer-channel-duplicate`, `load-empty` |
| Edit | `edit.` | Session edit preconditions and refusals |
| Io | `io.` | Load and save operation heads |
| Import | `import.` | Catalog-file import outcomes |
| Bridge | `bridge.` | Controller download/upload outcomes |
| Internal | `internal.` | The SDK catch-all, `internal.unexpected` |
| App | `app.` | **Reserved for a host.** This app mints `app.openvisual.*` and nothing else. |

Format rules:

- Kebab-case segments: lowercase ASCII letters and digits, single interior hyphens, no empty segment.
  `ProblemCode.Parse` enforces it; `TryParse` is the non-throwing reader.
- **Speaking, not opaque.** The id names the condition, so it is readable in a filter, a log and an export.
- **Unique across every family and every section**, not merely within one. Checked over the SDK's and this
  app's declarations combined.
- **Permanent once published.** An id is never renamed and never re-pointed at a different condition. A
  speaking id that outgrows its condition is SPLIT and the old id retired.
- An id is a filtering and grouping key, **never a suppression key**. Suppression is foreclosed: no rule-level
  disable, no per-element accepted-store.

---

## 5. The Danish message template

The user-facing text, authored **once**, on the entry.

**One raiser does not honour that yet.** A `.def`/`.ifb` definition finding renders the ENGLISH sentence its
builder hands to `ProjectValidationFinding`; nothing binds the Danish template on its entry. Author the entry
by the rules below all the same, and read [§9](#a-defifb-definition-finding) before assuming a user sees it.

- **Danish**, and one whole unit: a short fixed label (*Mangler Id-kode*, *Ikke forbundet*, *Filen er tom*) or
  one complete sentence carrying `{slot}` placeholders (*Kanalen '{channel}' har ingen kanal-id.*).
- **Never assembled at render time.** A presentation path renders the message as it stands; it does not
  prefix it, append to it, or stitch it from fragments. This is why an argument carries data and never words:
  a spliced fragment would need translating too, and nothing would know to.
- Two conditions whose sentences differ in their **opening words** are **two codes**, not one code with a verb
  argument — building one sentence from a fragment plus a shared tail is exactly the render-time assembly the
  fixed-label convention forbids. `controller-required-send` and `controller-required-retrieve` are that pair.
- Opens on a capital letter or a `{placeholder}`. No leading or trailing whitespace, no double space, no tab,
  no `TODO`.
- Ends on a letter, a digit, a full stop, or a `}`. Never an exclamation mark.
- **A host row** is no longer than the SDK's longest active template — measured off the SDK at run time by
  `HostPhrasingStandardTests.NoHostSentenceIsLongerThanTheSdksLongest`, never typed. The SDK is the yardstick,
  not a population the bound applies to: lengthening an SDK template is legal, and is what moves the host's cap.
- Multi-line is allowed where the sentence hands over a location — `"…\nFilen ligger her:\n{path}"`.

The **English** sentence goes in `Diagnostic`, which is logged and never rendered beside the message. It is
the one place an exception's own text may land.

---

## 6. Declared argument slots

`new ProblemArgumentSlot(name, ProblemArgumentType.X)`, in template order.

| `ProblemArgumentType` | CLR type | Holds |
| --- | --- | --- |
| `ElementIdentity` | `string` | A `.vis` id token (`_0x2a`) or a parsed `ElementId` |
| `SchemaName` | `string` | An XML tag or attribute name — a schema identifier, not prose |
| `AuthoredName` | `string` | A user-authored name exactly as it stands in the project |
| `Integer` | `int` | A count, a bound, an address, a channel |
| `Number` | `double` | A non-integral number: a delay, a fade time |
| `AttributeValue` | `string` | A raw attribute value as it stands in the file |
| `Path` | `string` | A file-system path or stream name, as given |

There is deliberately no `Sentence`, `Phrase` or `Label` kind: an argument carries **data**, never words of
the source language.

Required of every slotted row:

- **Placeholders and declared slots are the same set.** A template naming an undeclared slot renders a visible
  `{gap}`; a slot no template names is a value nobody sees.
- **Declared order is the template's first-appearance order**, because the typed factory's parameters follow
  the declaration. A row whose slots run in a different order hands the factory's arguments to the wrong slots.
- The raising rule or factory binds **exactly** the declared slots — no extras, none omitted.

**How arity and type are enforced differs by raiser, and only one of the three is a compile error:**

| Raiser | Binds through | Gate |
| --- | --- | --- |
| Host family (`HostProblems`) | A typed factory per code — one real parameter per declared slot | **The compiler**, at the call site: a wrong count or type does not build |
| A refusing site below the engine | `RefusalIdentity.Binding(…)` | `RefusalLabelDriftTests` |
| **An SDK project rule** | `Arguments(("name", value), …)` — an **untyped** `(string, object)` helper | **Runtime only, over the corpus**: `ProblemArgumentArityTests.EveryRuleBindsExactlyTheArgumentsItsCatalogueRowDeclares` |

The shipped SDK catalogue has **no factory to reflect** — its rules bind at the raise site — so the
parameters-follow-the-slots property is exercised against a fixture, not the real rows. Two consequences for a
new project rule: its binding is checked only where the **corpus witnesses it**, so a row no fixture triggers
is effectively unchecked; and three rows are grandfathered as binding a name their entry does not declare
(`link-bijection`, `scene-bijection`, `luid-low`). A fourth such mismatch fails the gate.

---

## 7. Thresholds

Every number a predicate compares against is DATA on the entry, never a literal in a rule body — a number
written inline is invisible to review, cannot be cited, and cannot change without a code change.

`new DeclaredThreshold(name, value, confidence, evidence)`:

| Field | Format |
| --- | --- |
| `Name` | What the number means; the entry's predicate comment refers to it, and `RuleAuthoring.Threshold(catalog, code, name)` reads it. |
| `Value` | `double`. |
| `Confidence` | `VendorDocumented` (a hard limit from a datasheet or the tool's own bounds), `VendorRecommendation` (a recommendation, not a limit — which is why such a row is a **Warning**: an Error's consequence must hold whatever the author intended), `Authored` (no source states a number). |
| `Evidence` | The citation, or the explicit unconfirmed note for an authored one. |

A number that **both** a gesture and a rule must enforce does not belong on an entry. It lives below both, and
each reads it — otherwise the catalogue becomes the authority for a fact a dialog must enforce before any
validation runs.

---

## 8. The predicate — required prose on a finding row

A finding row's doc-comment **is** its specification, authored beside the declaration and reviewed in the same
diff. Supply, in this order:

| Line | Content |
| --- | --- |
| Lead | The condition as a consequence, in one sentence: what goes wrong in the finished installation. |
| `PREDICATE:` | The decidable condition — tags, attributes, values, comparisons. Enough to implement from without inventing anything. |
| `SUBJECT:` | What the rule walks. |
| `EXCLUSION:` | What is deliberately not reported, and why. Write `none` when there is none. |
| `LOCATION:` | The element the finding anchors to, plus the related sites for a `PrimaryWithRelated` row. |
| `ARGUMENTS:` | What each declared slot carries and why the reader needs it. |

An operation outcome writes `PREDICATE: none — it is raised, never detected.`

---

## 9. Wiring — the edits an item needs

### A `.vis` project finding (error, warning, information)

1. Add the declaration to `ProblemCatalogEntries.ProjectFindings.cs`, with the predicate doc-comment from §8.
2. Add the member to that file's `ProjectFindings` array.
3. Author the rule **once**, in the rule module for its subject
   (`ihcclient/src/vis/validation/*Rules.cs`), through `RuleBuilder`:
   `Rule(catalog, code, body)` for a traversal, or `.Constrain(…)` for a declarative rule that the dialog face
   can also read. Bind arguments with `Arguments(("name", value), …)`. Read shared facts from the run's
   analyses (`Ids`, `Topology`, `Usage`) — no rule walks the document twice.
4. Register the module in `ProjectRules.All` if it is new.

A traversal rule may declare `WholeProject` only. A rule declaring `DialogMetadata` must be declarative — a
traversal has nothing a dialog could bind to.

### A `.def`/`.ifb` definition finding

**A different procedure, and not an oversight.** A definition builder REPORTS findings from its `Build()`, and
`ValidationLayerArchitectureTests` L4 bars it from the executor ports — so it cannot look a row up. There is no
`RuleBuilder` and no registration; the builder raises the finding directly.

1. Add the declaration to `ProblemCatalogEntries.CatalogDefinitions.cs`, and the member to its
   `CatalogDefinitionFindings` array. Declare `RuleFaces.WholeProject`, as all ten existing rows do and as §2
   requires of every Error and Warning. What these rows lack is the REGISTRATION, not the face: `ProjectRules`
   never sees them, and nothing walks a `.def` on the executor's behalf.
2. Raise it at the builder — `ProductDefinitionBuilder` or `FunctionBlockDefinitionBuilder` — as a **literal**
   `ValidationSeverity` beside the **literal** code string, in the pre-catalogue style those builders keep.
3. Keep the severity copies equal. The raised severity and the entry's disposition are two independent
   statements of one decision, and `CatalogCompletenessTests.EveryDefinitionFindingsRaisedSeverityMatchesItsEntry`
   is what compares them — the findings recording cannot, because the project corpus never validates a
   `.def`/`.ifb`. **Know its reach:** it regex-scans for a severity enum beside a string LITERAL, so it covers
   only the four codes the two builders raise in that shape. `CatalogGrammarAdvisor` mints its six `grammar-*`
   rows through `Warn(code, …)`, which hands the code to one shared `new(ValidationSeverity.Warning, …)` — the
   scan never sees them, and extending the advisor buys no cover from this gate.
4. **Expect the builder's ENGLISH sentence to be the one shown.** `Build()` throws `ProjectValidationException`,
   whose aggregate copies each finding's `Message` verbatim; nothing binds the entry's Danish template on this
   path, and no gate holds that message equal to the entry's `Diagnostic` either — `identity-missing` already
   carries two different English sentences, one per builder. Author the Danish template and the `Diagnostic`
   properly all the same: the catalogue index publishes them, and they are what binds the day this path is
   wired to the entry.

### An SDK fatal error

1. Add the code member to the raising layer's own class — `LoadRefusalCodes`, `SaveRefusalCodes`,
   `EditRefusalCodes`, `EditOpenRefusalCodes`, `ImportRefusalCodes`, `BridgeRefusalCodes` — or reuse an
   existing head from `OperationCodes`.
2. Declare the entry: the **cause** in `ProblemCatalogEntries.ProjectFindings.cs` (bare id, category set,
   `Refusal`), the **operation head** in `ProblemCatalogEntries.cs` (dotted id, no category), an **edit
   precondition** in `ProblemCatalogEntries.EditRefusals.cs`. Add it to the matching section array.
3. Raise it at the site, composing operation over cause as a `ProblemChain` — or `ProblemAggregate` for a head
   over many items, as a failed save is.
4. A site in `Ihc.Vis.Session`, `Ihc.Vis.Io` or `Ihc.Vis.Editing` **may not read the catalogue**. It carries
   its own Danish sentence beside its code through `RefusalIdentity`, and `RefusalLabelDriftTests` keeps that
   copy equal to the entry's template. `ValidationLayerArchitectureTests` L2 and L3 enforce the dependency
   direction for Session and Io; `Ihc.Vis.Editing` holds the same discipline by its own declaration
   (`EditOpenRefusalCodes`) with the drift test as its gate.

### A host outcome (`app.openvisual.*`)

Two edits in [`Services/HostProblemCatalog.cs`](../Services/HostProblemCatalog.cs), a third where the outcome is
bound as a `Problem`, then **two outside it** — a declared code with no site is exactly the defect
`EveryHostCodeIsShownBySomeSite` exists to catch.

1. `HostProblemCodes` — a `ProblemCode` property with a doc-comment saying what the app could not carry
   through.
2. `HostProblemCatalog` — the entry through the `Outcome(code, template, diagnostic, slots…)` helper, and the
   member added to the `Current` list.
3. `HostProblems` — the typed factory, **where the site shows a bound `Problem`**: one parameter per declared
   slot, plus the originating `Exception` where there is one. `Detail(cause)` is the only place in this
   application that reads an exception's message, and it moves it into the diagnostic slot. Where the SDK raised
   a coded cause, frame it with `HostProblems.Narrate(framing, raised)` so the more specific SDK sentence is the
   one rendered. **A gate has no factory**, and that is not an omission: `app.openvisual.validation-errors-block-send`
   refuses at `CanApply` time through `EditVerdict.Refuse(code, HostProblemCatalog.ValidationErrorsBlockSend.MessageTemplate)`,
   which needs the template and not a `Problem`. Match the site's shape rather than adding a factory nothing calls.
4. **The raising call site** — the view-model or service that shows the outcome, whatever surface it uses: a
   dialog, a greyed menu row's tooltip, the status bar.
5. **A row in `MessageSiteRegisterTests`** naming that site, its code and its owner. The register is asserted
   as an exact set in both directions — **declared host codes against the codes the SITES show**, factories not
   being what it counts — so step 5 is not bookkeeping: without it the suite fails.

---

## 10. Gates a new item must pass

Run `dotnet test tests/safe_project_tests/safe_project_tests.csproj` for an SDK item,
`tests/safe_visual_tests/safe_visual_tests.csproj` for a host item,
`tests/safe_architecture_tests/safe_architecture_tests.csproj` for either, and
`tests/safe_unit_tests/safe_unit_tests.csproj` as well for a **DOC**-category row.

| Requirement | Where it is enforced |
| --- | --- |
| Id unique across every family and section; `Category` non-null ⟺ section is not `OperationOutcomes` | `CatalogInvariants.Check`, via `ProblemCatalogTests` and `HostProblemCatalogTests.IdsAreUniqueAcrossEveryFamilyNotMerelyWithinThisOne` |
| Every Active entry has a registered rule or a raiser referencing its code outside the declaration files | `CatalogCompletenessTests.EveryActiveEntryHasSomethingBehindIt` |
| An Active raisable row has words; an Active row with words has a raiser | `CatalogCompletenessTests.NoRowHasWordsWithoutARaiserOrARaiserWithoutWords` |
| Placeholders = declared slots; declared order = template order; the rule binds exactly them | `ProblemArgumentArityTests` |
| A finding carries its entry's severity and category | `CatalogCompletenessTests.EveryRecordedFindingCarriesItsEntrysSeverityAndCategory` |
| Registration is consistent — no duplicate code, no missing entry, no retired code implemented, exactly one body, a traversal serving only `WholeProject`, a declared shape the rule keeps, a known target | `RuleSet.Create` (`RuleRegistrationFault`), `RuleRegistrationTests` |
| A refusing site's Danish copy equals its entry's template | `RefusalLabelDriftTests` |
| Host text is Danish, the diagnostic ASCII, and the phrasing inside the SDK's own measured bounds | `HostProblemCatalogTests`, `HostPhrasingStandardTests` |
| Host entries are outcomes, never findings | `HostProblemCatalogTests.EveryHostEntryIsAnOperationOutcomeAndNeverAFinding` |
| `Ihc.Vis.Session` and `Ihc.Vis.Io` do not depend on the validation engine; the GUI runs no executor and does not read the SDK catalogue | `ValidationLayerArchitectureTests` L2/L3/L5 |
| The SDK declares no `app.*` code; the GUI declares no SDK-family code | `ProblemOwnershipArchitectureTests` |
| A definition finding's raised literal severity equals its entry's disposition — for the codes raised as a literal severity/code PAIR, which is the two builders' four and not the advisor's six `grammar-*` rows ([§9](#a-defifb-definition-finding)) | `CatalogCompletenessTests.EveryDefinitionFindingsRaisedSeverityMatchesItsEntry` |
| Every declared host code is shown by a registered site, and every site's code is declared | `MessageSiteRegisterTests.EveryHostCodeIsShownBySomeSite` |

**Exact population pins move with the row, and each is a hand-maintained number.** Update them in the same
edit or the suite fails on a count, not on your rule. The expected numbers are NOT reproduced here — read each
one off its assert, which is the only copy that can be wrong and be caught:

| Pin | Where | Moved by |
| --- | --- | --- |
| Project rows per section, and their Active / RuledOut / Retired split; definition rows | `ProblemCatalogTests.TheCatalogueTotalIsItsOwnEntryCountAcrossEverySection` | Any added, retired or ruled-out row |
| Distinct codes the corpus witnesses (`BaselineRuleIdCount`) | `ValidationCharacterizationTests.TheCorpusWitnessesExactlyTheBaselineCodeCount` | A rule whose condition the corpus triggers |
| Total findings across the findings oracle files | `FindingOracleCoverageTests.TheWholeCorpusIsLoaded` | Any change to what the corpus reports, including a re-scoped predicate |

Two committed oracle sets move with a new or changed rule, and both are **regenerated and diffed, never
hand-edited**:

| Oracle | Regenerate with | Note |
| --- | --- | --- |
| [`tests/testdata/validation/`](../../../tests/testdata/validation/) — one XML file per corpus case, holding every finding that case produces in production order | `[Explicit] ValidationCharacterizationTests.Regenerate_TheFindingsOracles`, then copy over the directory | A code newly witnessed by the corpus also moves `BaselineRuleIdCount` |
| [`tests/testdata/reports/`](../../../tests/testdata/reports/) `full-*` — **DOC-category rows only** | `[Explicit] ReportOracleTests.Regenerate_TheTxtOracles` (`safe_project_tests`, the `*.txt` oracles) and `[Explicit] ReportHtmlOracleTests.Regenerate_TheHtmlOracles` (`safe_unit_tests`, the `*.html` ones) | The Fuld reports render the DOCUMENTATION category as their appendix, so a DOC row moves both formats |

The generated tables inside `problem-catalogue.md` are rewritten by
`[Explicit] CatalogTableRenderingTests.Regenerate_TheCatalogueIndex` and `Regenerate_TheCategoryTable`. Adopting
any diff means explaining every changed line by a rule that changed in the same edit.

---

## 11. The four list tiers, and what each still needs

A findings list showing **fatal errors, errors, warnings and information messages** is two tiers short today.
This section states how each tier derives and what is missing.

| Tier | Danish | Derives from | Status |
| --- | --- | --- | --- |
| Fatal error | *Fatale fejl* | `Severity == Error` **and** the row refuses an operation | **Blocked** — the row cannot say it refuses one (§11.1) |
| Error | *Fejl* | `Disposition.Error` → `ValidationSeverity.Error` | Ships |
| Warning | *Advarsler* | `Disposition.Warning` → `ValidationSeverity.Warning` | Ships |
| Information | *Information* | `Disposition.Info` → `ValidationSeverity.Info` | **Blocked** — no entry can declare `Info` (§11.2) |

### 11.1 Fatal — declare what a row blocks, do not add a severity

Of the 22 rows problem-catalogue.md §4 publishes as *Fatal error*, only five can ever be a row in a loaded
project's list:

| What they refuse | Rows | Declared as | A list row? |
| --- | --- | --- | --- |
| **Open** — `load-*` | 11 | `Refusal` / `Faces.None` | **No.** The project never opened, so there is no list |
| **Import · Export · the write target** — `import-catalog-*`, `import-controller-no-project`, `export-controller-declined`, `save-target-unwritable`, `save-roundtrip-mismatch` | 6 | `Refusal` / `Faces.None` | **No.** Outcomes of an attempt, not properties of project content |
| **Save · Export** — `element-undeclared`, `attr-undeclared`, `attr-latin1`, `attr-required` — plus `root-version`, whose published cell says *Open* but which refuses nothing today | 5 | **`Error` / `Faces.WholeProject`** | **Yes — listed today, as Errors** |

That enumeration is over what §4 PUBLISHES as fatal, and §4 counts only the file lifecycle. A row that leaves
the project openable, editable and savable but blocks a controller transfer belongs in the last group too — it
lists today as an ordinary Error, and it is the case that most needs the declaration below, since no column
anywhere can currently say it.

So the requirement reduces to one question: should the rows in that last group render as *Fatale fejl* rather
than *Fejl*?

**They cannot today, because the fact is not declared.** §4 carries a **Blocks** column, but there is no such
field on `ProblemCatalogEntry` — it is hand-written prose that has already drifted twice: `attr-required`'s
cell was corrected by hand from *"Error | —"* to *"Fatal error | Save · Export"*, and `root-version` publishes
*"Fatal error | Open"* while `LoadRefusalTests` records that no member of `LoadRefusalCodes` carries that cause,
so nothing refuses the open. The ROW is raised — `StructureRules` reports it, which is why it is one of the five
above; what has no site is the refusal its cell publishes.

**Add the declaration; do not add a fourth `ValidationSeverity`.**

1. Add `RefusedOperations` to the entry — the operation heads this row refuses, empty for the rows that refuse
   nothing. **The vocabulary is the head set, not §4's Blocks column**: `io.load`, `io.save`, `edit.open`,
   `import.catalog`, `bridge.download`, `bridge.upload`. `edit.open` is the one the column cannot express and
   the one a row already needs — `attr-undeclared` refuses the edit-open as well as the save, and its
   declaration says so in a comment precisely because §4 has nowhere to put it. Specifying the field over the
   file lifecycle alone would rebuild the gap the field exists to close, and would leave a row that blocks only
   the controller transfer with no way to say it.
2. Derive the panel tier: `Severity == Error && RefusedOperations is not empty` → *Fatale fejl*.
3. Generate §4's **Blocks** column from the declaration and compare it by test, like the rest of the index —
   rendering the file-lifecycle heads it publishes today, so a wider declaration does not silently change the
   published table.

Why a declared fact and not a severity member: `CatalogDisposition`'s own rationale is that *"fatal" was
carrying two unrelated meanings* — **the operation cannot proceed** versus **catastrophic in effect** — and a
fourth severity re-merges them. The two are genuinely independent: `save-roundtrip-mismatch` refuses the save
and is no finding at all, while `attr-undeclared` refuses the save **and** is a finding. Declaring the refusal
also costs no `ValidationSeverity` ordinal change (the enum is public API) and moves no `@severity` byte in the
findings oracles, and it gives the send gate something better than *"any Error"* — it can name the blocked
operation, which is the whole point of a row that is fatal to one operation and to nothing else.

**The 11 open-blockers are a different surface.** Listing them means a *"this file would not open"* view over
one refusal chain, not the project findings list. Do not fold them into the panel.

### 11.2 Information — `CatalogDisposition` needs a fourth member

`ValidationSeverity.Info` exists and the Problemer panel is fully plumbed for it — tier, icon, toggle, the
label *Information*, sorting and the findings export. **No production rule emits one, and no catalogue entry
can declare one**, because `CatalogDisposition` has three members and `ProblemCatalogEntry.Severity` is derived
from it. Before the first information item can be authored:

1. Append `Info` to `CatalogDisposition` — appended, so existing ordinals do not move.
2. Map it in `ProblemCatalogEntry.Severity` to `ValidationSeverity.Info`.
3. Correct the doc-comment on `CatalogDisposition.Refusal`, which currently records that no entry can produce
   an Info finding.
4. Extend `ProblemCatalogTests.SeverityFollowsFromDispositionSoTheTwoCannotDisagree` to the fourth member.
5. Extend `CatalogCompletenessTests.Expected`, which maps **every** non-Error disposition to `Warning`. Miss
   this and the first Info finding the corpus witnesses fails
   `EveryRecordedFindingCarriesItsEntrysSeverityAndCategory` — comparing a recorded `Info` against an expected
   `Warning`.
6. Add the **Information** column to the category-count renderer in `CatalogTableRenderingTests`, and
   regenerate the table.

The change is additive: no existing row is reclassified, and no oracle bytes move until a row actually declares
`Info`. Reclassifying existing rows to Information is a separate edit with its own oracle diff.

### 11.3 The one decision a four-tier list still needs

The findings export mirrors the panel's tier filters in its `@severities` attribute. A Fatal tier derived from
`RefusedOperations` is a **presentation** grouping, so either the export gains a `blocks` attribute — moving
bytes on those five rows' findings — or the panel and the export end up with different tier vocabularies.
Settle that before building the tier. Taking the `ValidationSeverity.Fatal` route instead moves the same oracle
bytes anyway, and buys the weaker model.

---

## 12. Changing, retiring and ruling out

| `ProblemCodeStatus` | Meaning | Rule allowed? |
| --- | --- | --- |
| `Active` | Minted and reported normally | Required |
| `Retired` | No longer minted; the id stays reserved and is never reused for a different condition | Forbidden |
| `RuledOut` | Investigated, and never to be minted — the condition is not a defect, or the limit it assumes does not exist | Forbidden |

- **Nothing is ever deleted.** A retired entry keeps its id occupied, and the duplicate-code invariant is then
  what refuses to reuse it. There is no separate reserved-id list to fall out of sync.
- **There is no `Deprecated`.** A code is minted or it is not.
- A `RuledOut` row is positive knowledge: deleting it loses the finding that it is *not* a finding, and the
  next reader re-derives it.
- Reclassifying a row — a different disposition, a different category — is an ordinary edit to the
  declaration, plus the oracle diff it causes. Changing an id is not: split, and retire the old one.
