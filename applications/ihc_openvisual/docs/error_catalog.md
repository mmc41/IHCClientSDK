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
| A fault in the TOOL rather than in anything it describes | SDK, or this app | `ProblemCatalogEntries.InternalFaults.cs`, or `Services/HostProblemCatalog.cs` | slots only |

**A host authors no findings.** A finding is a statement about the `.vis` file, the SDK owns the file, and a
second opinion minted in an app is how two catalogues start disagreeing.

A host entry is therefore one of exactly **two** shapes, both with no category, no severity and no face:

| Host shape | `Disposition` | `Kind` | Builder in `HostProblemCatalog` | What it records |
| --- | --- | --- | --- | --- |
| **Host outcome** | `Refusal` | `OperationOutcome` | `Outcome(code, template, diagnostic, slots…)` | An action this app declined or could not carry through |
| **Host internal fault** | `NotApplicable` | `InternalFault` | `Fault(code, template, diagnostic, slots…)` | This app, or a layer under it, breaking |

Pick by what the row RECORDS, not by how it is surfaced: a fault shown in a dialog is still a fault. A row
that says the tool broke — a handler that threw, a discarded platform exception, a dead telemetry pipeline, a
validation run that failed — takes the fault shape. Declaring one as an outcome passes `CatalogInvariants`
VACUOUSLY: the biconditional in [§2](#2-the-item-kinds-and-the-axes-each-one-sets) compares a row against its
own declaration, so a row that mislabels itself is never caught by it.

`HostProblemCatalogTests` fails any entry that is neither shape, and fails a host-authored FINDING against a
seeded entry that is schema-legal — the rule there is about ownership. A retired row is declared on the shape
it would have had: retirement reserves the id, it does not excuse the declaration from being true.

---

## 2. The item kinds and the axes each one sets

| Item | `Section` | `Category` | `Disposition` | `Kind` | `Faces` | `Severity` (derived) |
| --- | --- | --- | --- | --- | --- | --- |
| **Fatal error** — cause | `ProjectFindings` / `CatalogDefinitionFindings` | one of eight | `Refusal` | `OperationOutcome` | `None` | — |
| **Fatal error** — operation head | `OperationOutcomes` | `null` | `Refusal` | `OperationOutcome` | `None` | — |
| **Edit precondition** | `OperationOutcomes` | `null` | `Refusal` | `EditPrecondition` | `None` | — |
| **Error** | `ProjectFindings` / `CatalogDefinitionFindings` | one of eight | `Error` | `UserContentRule` or `SchemaSerializationGuard` | at least one | `Error` |
| **Warning** | same | one of eight | `Warning` | same | at least one | `Warning` |
| **Information** | same | one of eight | `Info` — see [§11.2](#112-information--the-fourth-disposition) | same | at least one | `Info` |
| **Host outcome** | `OperationOutcomes` | `null` | `Refusal` | `OperationOutcome` | `None` | — |
| **Internal fault** | `OperationOutcomes` | `null` | `NotApplicable` | `InternalFault` | `None` | — |

**An internal fault is the one kind that says nothing about a project.** Every other row above describes the
`.vis` file, a definition file, or an action the user asked for. This one describes the tool failing — a rule
that crashed, an operation that threw, a platform boundary that discarded an exception, a telemetry pipeline
that is down. It therefore sets `Disposition = NotApplicable` rather than `Refusal`: refusing is a thing an
operation does about a request, and there was no request. `CatalogInvariants` enforces the whole row as a
biconditional — an entry is `InternalFault` exactly when it declares no category, no faces, no refusal and
`NotApplicable` — so a fault cannot acquire a category by accident and a finding cannot borrow this kind.

**It carries an ORIGIN, which no other item does.** `Sdk`, `Host` or `Platform` — the engine, this application,
or the layer beneath it. A support question can then say whose code failed without reading the sentence, and the
Danish sentence is free to describe the consequence instead of the blame.

**It is excluded from the findings export by design.** A findings file is a statement about the project, and a
software fault is not part of the project; the export's grammar requires a category and a severity on every
finding, and an internal fault has neither. What replaces the export for these is the panel's own bulk copy —
see [the Problemer panel story](stories/18-problems-panel.md).

Six consequences of this table are load-bearing:

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
  wording, not the limit of what a refusal can block. Which operations a row blocks is declared on the entry as
  `RefusedOperations`; see [§11.1](#111-fatal--a-declared-fact-not-a-fourth-severity).
- **An edit precondition is a `Refusal` too, and is not one of those.** It refuses ONE command — *the target no
  longer exists*, *the target is not the kind this command edits* — and leaves the file operations and the
  transfers alone. Same disposition, same `Faces.None`, a different blast radius: declare it in
  `ProblemCatalogEntries.EditRefusals.cs` and do not reach for the operation-head shape.
- **A row that both refuses and reports is declared `Error`.** `attr-undeclared` reports at validate and
  refuses the save; its refusal comes from the operation's own entry with this row as the cause, which is why
  the disposition axis needs no fourth value.
- **`Severity` is derived from `Disposition`**, never declared, so the two cannot disagree. `Refusal` has no
  severity, and no severity means "refused".
- **A controller-firmware defect is an ordinary `Error` or `Warning`, never a `Refusal`.** A project that is
  valid in every respect but drives the controller into a firmware or shipped-block defect must still raise a
  finding, so the user meets the guardrail before committing the design. Nothing in the SDK refuses, though:
  the file opens, edits, saves and uploads. `Refusal` measures whether an SDK OPERATION can proceed, and every
  one of them can — so such a row declares an empty `RefusedOperations` and grades on
  [§7.1](#71-firmware-bounds)'s rule.

Together these give the findings LIST its four tiers. Three of them are populated; `Info` is declarable and no
row declares it yet. [§11](#11-the-list-tiers-and-how-each-derives) states how each one derives.

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
| 4 | `Disposition` | `CatalogDisposition` | `Error`, `Warning`, `Info`, `Refusal`, `NotApplicable`. The finding tiers, a refusal, and — for a row that is neither, i.e. a fault in the tool — `NotApplicable`; see [§11.2](#112-information--the-fourth-disposition). |
| 5 | `Kind` | `RuleKind` | `UserContentRule`, `SchemaSerializationGuard`, `EditPrecondition`, `OperationOutcome`, `InternalFault`. `InternalFault` is required exactly when `Disposition` is `NotApplicable`, and refused otherwise. |
| 6 | `Faces` | `RuleFaces` | `None` for anything realised at a throw site; `WholeProject` and/or `DialogMetadata` for a registered rule. A registered rule declaring `None` is refused. |
| 7 | `Target` | `RuleTarget` | `(tag, attribute)` — e.g. `new RuleTarget("product", "address")`. A **null tag** means one of two things, decided by the attribute: with one it is the **wildcard**, *this attribute on whatever element the rule reports*; without one it is the project as a whole. Rejected when the schema registry knows the tag and not the attribute — and a wildcard is rejected when **no** registered element declares the attribute, so a typo cannot register as a rule that silently never fires. |
| 8 | `Shape` | `FindingShape` | `OneFinding` (one repair clears everything), `OnePerOccurrence` (the usual choice for a content row — write it out, because the enum's zero value is `OneFinding` and `default` here silently means that), `PrimaryWithRelated` (one repair, but the user must see every site). |
| 9 | `Slots` | `EquatableArray<ProblemArgumentSlot>` | See [§6](#6-declared-argument-slots). `default` when the sentence needs no data. |
| 10 | `MessageTemplate` | `string` | See [§5](#5-the-danish-message-template). |
| 11 | `Status` | `ProblemCodeStatus` | Optional, `Active` by default. See [§12](#12-changing-retiring-and-ruling-out). |

**Why a multi-tag row needs the wildcard, and what a declared `Target` then does.** Some rows are genuinely about
one attribute on several element types — a terminal's `cable_colour` is reported on both `dataline_input` and
`dataline_output`, and no single tag names it. Declaring one of the two would look right and quietly exclude half
the row's sites, so such a row declares `RuleTarget(null, attribute)` instead. Both engine faces honour it:
`RuleSet.ForTarget` returns a wildcard row for a concrete `(tag, attribute)` query, and the whole-project executor
walks every registered element type that declares the attribute rather than returning early.

A declared attribute is then **projected onto every finding the row emits**, as `ValidationFinding.TargetAttribute`.
That is what carries the fact across the layer boundary: a host may not read this catalogue, so a frontend that
wants to take the user to the FIELD a finding is about reads it from the finding. Declaring a target moves no
oracle — it changes what a finding carries, not which findings are produced — but it is a claim, and the sweeps
check it: one asserts that every emitting row reports an element whose tag really declares the attribute, and the
host-side one asserts that each declaring row reaches an editable field or is listed with a reason why it cannot.

Then the init-only fields:

| Field | Type | Required | Format |
| --- | --- | --- | --- |
| `Diagnostic` | `string?` | Yes in practice | The **English** engine sentence. Goes to the log, never to a user. Binds the same `{slot}` names as the Danish template. |
| `Evidence` | `EvidenceMark` | Recommended | `Unknown`, `Authored` (the condition is reachable — produced against the live vendor tool or observed in a real installation), `Refused` (the vendor tool will not author it, so the state arrives only by import or by hand). Metadata, never a gate. |
| `Thresholds` | `EquatableArray<DeclaredThreshold>` | When the predicate compares a number | See [§7](#7-thresholds). |
| `RequiresControllerLimits` | `bool` | When the row needs a target controller's capability limits | Such a rule is absent from the default project-only profile: it does not run and does not report, rather than guessing. |
| `RequiresLibrary` | `bool` | When the row can only be decided against the library a placed block claims | Same posture: skipped when no `ILibraryBlockSource` was supplied. |
| `FirmwareBound` | `DeclaredFirmwareBound?` | When the row's condition is a controller-firmware or shipped-block errata | The release that fixed the defect — see [§7.1](#71-firmware-bounds). **The opposite posture from the two flags above**: the row runs and reports with NO firmware supplied, and a declared `ValidationProfile.Firmware` can only WITHHOLD a finding whose fix that target is already past. There is deliberately no `RequiresFirmware` flag, and adding one would be the defect: an enabling flag skips the row exactly when nobody has named a controller, which is while the project is being designed. |
| `RefusedOperations` | `EquatableArray<ProblemCode>` | When the row also REFUSES an operation | The operation heads it refuses, from `OperationCodes.All` — `io.load`, `io.save`, `edit.open`, `import.catalog`, `bridge.download`, `bridge.upload`. Empty for the great majority. `CatalogInvariants.Check` refuses a code that is not a head. Declare what a site actually raises. problem-catalogue §4's **Blocks** column is generated FROM this field and publishes one word per head — `Open`, `Save · Export`, `Edit-open`, `Import`, `Download`, `Upload` — so the column can express every declaration, and adding a seventh head is a decision about how §4 words it rather than something a renderer settles by omission. Two consumers: the finding carries it (`ValidationFinding.RefusedOperations`), and the panel derives its *Fatale fejl* tier from it — see [§11](#11-the-list-tiers-and-how-each-derives). **It pairs with `Error` or `Refusal`, and that is now ENFORCED**: `CatalogInvariants.Check` reports `RefusedOperationOnAdvisoryDisposition` for a `Warning`/`Info` row that declares one, and `RefusalWithoutRefusedOperation` for a `Refusal` content row that declares none — a refusal has to say which operation it stops. The other way in is closed too: `ValidationProfile.SeverityFor` **throws** when an override demotes a refusing row below `Error`, rather than flooring it silently. |

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

Every raiser honours it, including the `.def`/`.ifb` builders: they carry a literal COPY of the entry's Danish
sentence beside the code — they may not read the catalogue — and `DefinitionLabelDriftTests` holds the copy
equal to the entry. The English moves to `ProjectValidationFinding.Diagnostic`, beside it rather than instead of
it. **One raise site is exempt**, `identity-missing` at `FunctionBlockDefinitionBuilder`: the code is raised by
two builders about two different conditions and its template describes only the product one, so that site keeps
an accurate English sentence rather than a false Danish one until the code is SPLIT. The exemption is named and
justified in the drift test — see [§9](#a-defifb-definition-finding).

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

A number **two rows** compare against stays on both entries: each declares its own threshold, under its own
name, with its own citation, and each rule reads its OWN entry — never a neighbour's. What the two must not do
is write the figure twice. Bind one constant declared beside the entries in `ProblemCatalogEntries`, say so in
both evidence texts, and pin the pair equal in a test: two rows are two statements about one fact, so raising
one alone is not a smaller change but a silent re-classification of the other. `MaximumRs485Components` /
`Rs485MaxComponents` and the two `SupportedVersionMajor` declarations are the shipped examples.

### 7.1 Firmware bounds

A row whose condition is a controller-firmware or shipped-block errata declares the release that fixed the
defect, on the same principle as a threshold: a version that code COMPARES is data on the entry, never a
literal in a rule body.

`new DeclaredFirmwareBound(name, fixedIn, confidence, evidence)`:

| Field | Format |
| --- | --- |
| `Name` | What the bound is about; the entry's predicate comment refers to it. |
| `FixedIn` | `ControllerFirmwareVersion?`. **`null` means no release is known to fix the defect**, and such a row is never withheld, however new the target. |
| `Confidence` | The same three grades. A vendor CLAIM that a release fixed something, unverified here, is `VendorRecommendation`; `VendorDocumented` is for a bound this repository can check. |
| `Evidence` | The citation, or the explicit unconfirmed note for an authored one. |

A separate type from `DeclaredThreshold` because a version is not a number: that record's `Value` is a
`double`, and `3.3.21` crammed into one would compare wrongly and read as a quantity.
`ControllerFirmwareVersion.TryParse` is lenient at the head and strict at the tail — it reads `03.03.33`,
`CTR.R.03.03.44` and `v3.3.21`, and refuses a single component or a garbled tail, because a half-read version
silently narrows a real finding away.

**Severity follows the firmware, not the tone of the evidence:**

| What the source establishes about the defect | Disposition |
| --- | --- |
| Still present in the newest firmware the source knows — or the "fix" was dropping the feature, so no upgrade helps | `Error` |
| Fixed in a named release | `Warning` — the installation may already be past it |
| One field report, a v2-only observation with no v3 measurement, or contradicted evidence | `Warning` |

This does not bend §2's rule that an Error's consequence must hold whatever the author intended. That rule
tests DESIGN INTENT, not environment: *this project drives an affected dimmer through scenario recall* is true
or false in the bytes, whatever the author meant by it.

**Narrowing, not enabling — the third declared context, and the one that runs backwards.**
`RequiresControllerLimits` and `RequiresLibrary` are enabling: absent context, and the rule does not run and
does not report, because counting against a limit nobody supplied would be a guess. A firmware bound is the
reverse. The row runs with no context at all, and `ValidationProfile.Firmware` — when a caller knows the
target — can only WITHHOLD a finding whose fix that target is past. It can never add one. Undeclared is
therefore the strict reading, so a caller who knows nothing is never told less than one who knows something.

**It must not reach `ValidationProfile.CanEvaluate`.** The findings export publishes that predicate's negatives
as rules it could not run for want of context. A row narrowed away by a firmware target WAS evaluable, and
listing it there would tell the reader the caller withheld context this row never needed.

**No row declares a bound yet.** The mechanism ships inert, the way `CatalogDisposition.Info` did
([§11.2](#112-information--the-fourth-disposition)) — so it moved no oracle byte and no population pin.
`FirmwareNarrowingTests.TheMechanismIsInertUntilARowDeclaresABound` asserts that, and **that test is to be
deleted in the diff that adds the first errata row.**

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

**A controller-firmware errata row is one of these, with no extra procedure.** Declare `FirmwareBound`
([§7.1](#71-firmware-bounds)), leave `RefusedOperations` empty, and author the rule in the module for its
subject like any other. Do not add a `Requires*` flag for it, and do not reach for the fatal-error procedure
below: nothing in the SDK refuses, and the row is an ordinary finding whose consequence lands on the
controller.

### A `.def`/`.ifb` definition finding

**A different procedure, and not an oversight.** A definition builder REPORTS findings from its `Build()`, and
`ValidationLayerArchitectureTests` L4 bars it from the executor ports — so it cannot look a row up. There is no
`RuleBuilder` and no registration; the builder raises the finding directly.

1. Add the declaration to `ProblemCatalogEntries.CatalogDefinitions.cs`, and the member to its
   `CatalogDefinitionFindings` array. Declare `RuleFaces.WholeProject`, as all eleven existing rows do and as §2
   requires of every Error and Warning. What these rows lack is the REGISTRATION, not the face: `ProjectRules`
   never sees them, and nothing walks a `.def` on the executor's behalf.
2. Raise it at the builder — `ProductDefinitionBuilder` or `FunctionBlockDefinitionBuilder` — as a **literal**
   `ValidationSeverity` beside the **literal** code string, in the pre-catalogue style those builders keep, with
   the entry's Danish sentence as the `Message` and the English in `Diagnostic`. The Danish is a literal COPY:
   this layer may not read the catalogue, which is the same constraint a refusing site works under (§9's
   *An SDK fatal error*), and the same answer.
3. Keep the severity copies equal. The raised severity and the entry's disposition are two independent
   statements of one decision, and `CatalogCompletenessTests.EveryDefinitionFindingsRaisedSeverityMatchesItsEntry`
   is what compares them — the findings recording cannot, because the project corpus never validates a
   `.def`/`.ifb`. It **provokes** every code rather than scanning source text, so its reach is all eleven. (It used
   to regex-scan for a severity enum beside a string literal, which reached four: `CatalogGrammarAdvisor` mints
   its six `grammar-*` rows through one shared `Warn` helper whose single `new(ValidationSeverity.Warning, …)`
   names no code, so the pattern never matched one of them.) Add your provocation to
   `DefinitionFindingProbe.Provoked` — `TheProbeReachesEveryCatalogDefinitionCode` fails until you do.
4. **Keep the Danish copy equal to the entry's template.** `Build()` throws `ProjectValidationException`, whose
   aggregate carries each finding's `Message` and `Diagnostic` through to the user and the log respectively, so
   the sentence you write at the raise site is the sentence a user reads.
   `DefinitionLabelDriftTests.EveryDefinitionFindingSaysWhatItsEntrySays` is the gate, and it carries **no
   exceptions**: every raiser is held to its entry's template unconditionally. It did carry one —
   `identity-missing` at `FunctionBlockDefinitionBuilder`, keeping English because the entry's template
   describes the PRODUCT condition and would be false of a block. That was always a symptom of one code
   carrying two conditions, and the repair was the split: the block condition is now
   `block-identity-missing` ("Mangler blokidentitet"), and the exemption went with it. If a template cannot be
   true of your raiser's condition, SPLIT the code — do not ask for an exception.

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
5. **Declare `RefusedOperations` on the CAUSE**, naming the head your identity refuses under. This is not
   optional bookkeeping: `RefusalLabelDriftTests.EveryRowDeclaresExactlyTheOperationsItsRegistriesRefuse`
   compares the declarations against what the registries raise, and admits no exception in that direction — a
   new identity whose cause does not declare its operation fails the suite. The reverse — declaring a refusal
   nothing raises yet — is legal only for a `Refusal` row on that test's named list, with its reason written
   out. Declaring the head also feeds §4's generated **Blocks** column and the panel's *Fatale fejl* tier, so a
   row that reports as well as refusing lands in the right tier without anyone maintaining a second list.

### A host outcome (`app.openvisual.*`)

Two edits in [`Services/HostProblemCatalog.cs`](../Services/HostProblemCatalog.cs), a third where the outcome is
bound as a `Problem`, then **two outside it** — a declared code with no site is exactly the defect
`EveryHostCodeIsShownBySomeSite` exists to catch.

1. `HostProblemCodes` — a `ProblemCode` property with a doc-comment saying what the app could not carry
   through.
2. `HostProblemCatalog` — the entry through `Outcome(code, template, diagnostic, slots…)` or
   `Fault(code, template, diagnostic, slots…)`, whichever shape [§1](#1-which-catalogue-owns-what) says the row
   is, and the member added to the `Current` list.
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

Run `dotnet test tests/safe_project_tests/safe_project_tests.csproj` for an SDK item and for a host item
alike — both catalogues' gates live there, including both halves of the report oracles a **DOC**-category row
moves — and `tests/safe_architecture_tests/safe_architecture_tests.csproj` for either. Add
`tests/safe_visual_tests/safe_visual_tests.csproj` only when the row changes something a window renders.

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
| A definition finding's raised severity equals its entry's disposition, over all eleven definition codes — provoked, not scanned ([§9](#a-defifb-definition-finding)) | `CatalogCompletenessTests.EveryDefinitionFindingsRaisedSeverityMatchesItsEntry`, with its reach pinned by `TheProbeReachesEveryCatalogDefinitionCode` |
| A definition builder's Danish copy equals its entry's template — unconditionally, with no exceptions | `DefinitionLabelDriftTests` |
| A declared refused operation is one of the six operation heads | `CatalogInvariants.Check`, via `ProblemCatalogTests.ADeclaredRefusalMustNameAnOperationHead` |
| A `Warning`/`Info` row declares NO refused operation, and a `Refusal` content row declares at least one | `CatalogInvariants.Check` (`RefusedOperationOnAdvisoryDisposition`, `RefusalWithoutRefusedOperation`), armed both ways by `ProblemCatalogTests.TheInvariantsCatchEachViolationTheyName` |
| A profile override cannot demote a refusing row below `Error` — it throws rather than flooring | `ValidationProfile.SeverityFor`, via `WholeProjectValidatorTests.ARowThatRefusesAnOperationCannotBeOverriddenBelowError` |
| A firmware-bounded row reports with no target declared; a declared target only ever withholds, never creates; and narrowing stays out of the evaluability axis the export publishes | `FirmwareNarrowingTests` |
| An export's `@error_tiers` cannot contradict its `@severities` | `FindingExportWriter.Write` (`ArgumentException`), via `FindingExportWriterTests.AnErrorTierFilterThatContradictsTheSeveritiesIsRefused` |
| What a row declares it refuses is what its registries actually raise | `RefusalLabelDriftTests.EveryRowDeclaresExactlyTheOperationsItsRegistriesRefuse` |
| problem-catalogue §4's **Severity** and **Blocks** columns match the declarations, no refusing row has left that section, and every row published Fatal names an operation | `CatalogTableRenderingTests.TheSeverityColumnMatchesWhatTheRowsDeclare`, `TheBlocksColumnMatchesWhatTheRowsDeclare`, `EveryRowThatRefusesAPublishedOperationAppearsInSectionFour`, `EveryRowPublishedAsFatalNamesTheOperationItRefuses` |
| Every operation head has exactly one published word, so a wider declaration cannot make that column lossy | `CatalogTableRenderingTests.EveryOperationHeadHasExactlyOnePublishedLabel` |
| The shipped grammar's `operationHead` enumeration and its `arg_*` vocabulary equal the SDK's own heads and the catalogue's declared slots | `FindingSchemaParityTests` |
| A findings-export oracle conforms to the shipped grammar, `blocks` and `error_tiers` included | `tests/ValidateFindingsOracles.targets`, imported by `safe_project_tests` and run **before Build** — a schema-invalid oracle fails `dotnet build` rather than a test, and it is the only gate on that grammar |
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
| [`tests/testdata/reports/`](../../../tests/testdata/reports/) `full-*` — **DOC-category rows only** | `[Explicit] ReportOracleTests.Regenerate_TheTxtOracles` (the `*.txt` oracles) and `[Explicit] ReportHtmlOracleTests.Regenerate_TheHtmlOracles` (the `*.html` ones) — both in `safe_project_tests`, the suite that owns the reports | The Fuld reports render the DOCUMENTATION category as their appendix, so a DOC row moves both formats |

The generated parts of `problem-catalogue.md` are rewritten by three `[Explicit]` regenerators, all in
`CatalogTableRenderingTests`: `Regenerate_TheCatalogueIndex` (the appendix), `Regenerate_TheCategoryTable`
(§1's per-category counts, one column per disposition) and `Regenerate_SectionFoursGeneratedCells` (§4's
**Severity** and **Blocks** cells, rendered from the row's disposition and its `RefusedOperations`). The last one
rewrites both cells in one pass because they are two halves of one statement, and moving either alone can leave
the pair contradicting §7. Adopting any diff means explaining every changed line by a rule that changed in the
same edit.

---

## 11. The list tiers, and how each derives

The list shows **internal errors, fatal errors, errors, warnings and information messages** — every tier ships.
This section states how each one derives.

| Tier | Danish | Derives from | Status |
| --- | --- | --- | --- |
| Fatal error | *Fatale fejl* | `Severity == Error` **and** `RefusedOperations` is not empty | Ships (§11.1) |
| Error | *Fejl* | `Disposition.Error` → `ValidationSeverity.Error` | Ships |
| Warning | *Advarsler* | `Disposition.Warning` → `ValidationSeverity.Warning` | Ships |
| Information | *Information* | `Disposition.Info` → `ValidationSeverity.Info` | Ships (§11.2) |

The Danish column names the GROUP, which is what a specification describes. The application labels one
finding at a time — the filter chip and every row's Alvor cell read `ProblemsPanelViewModel.TierLabel`, which
is singular throughout: *Fatal fejl*, *Fejl*, *Advarsel*, *Information*.

### 11.1 Fatal — a declared fact, not a fourth severity

Twenty-three rows declare a refusal. Only five of them can ever be a row in a loaded project's list:

| What they refuse | Rows | Declared as | A list row? |
| --- | --- | --- | --- |
| **Open** — `load-*` | 12 | `Refusal` / `Faces.None` | **No.** The project never opened, so there is no list |
| **Import · Download · Upload · the write target** — `import-catalog-*`, `import-controller-no-project`, `export-controller-declined`, `save-target-unwritable`, `save-roundtrip-mismatch` | 6 | `Refusal` / `Faces.None` | **No.** Outcomes of an attempt, not properties of project content |
| **Save · Export · Edit-open** — `element-undeclared`, `attr-undeclared`, `attr-latin1`, `attr-required`, `id-duplicate-token` | 5 | **`Error` / `Faces.WholeProject`** | **Yes — the *Fatale fejl* tier is exactly these five** |

**What splits them is the disposition, not the published cell.** A `Refusal` row reports nothing, so it has no
finding to list however catastrophic it is; a row that REPORTS and also refuses is an ordinary finding that
happens to stop an operation. (`load-truncated` is the eighteenth `Refusal` row and declares `io.load` like
every other refusal, even though it is `RuledOut` — the head is a property of the CONDITION, not of the
wiring, and `RefusalWithoutRefusedOperation` requires it. What its status buys is absence from §4, not
silence about which operation it would stop — see [§12](#12-changing-retiring-and-ruling-out).)

problem-catalogue.md §4 publishes 21 of the 23 as *Fatal error*, and the two it does not are worth knowing
about. `load-truncated` is simply absent, on its `RuledOut` status. The other is published, and reads
differently: that Severity cell follows §2's definition of the term — the file lifecycle and the two controller
transfers — so `id-duplicate-token`, which refuses `edit.open` and nothing else, reads *Error* there while the
panel lists it under *Fatale fejl*. **The panel derives its tier from the declaration, never from a published
cell**, so the two answer different questions and neither is stale. Both cells are now RENDERED
(`Regenerate_SectionFoursGeneratedCells`), so the difference is derived and reviewable rather than retyped.

**Why a declaration and not prose.** The fact used to live only in §4's **Blocks** column, hand-written and
drifted twice: `attr-required`'s cell was corrected by hand from *"Error | —"* to *"Fatal error | Save · Export"*,
and `root-version` published *"Fatal error | Open"* while `LoadRefusalTests` records that no member of
`LoadRefusalCodes` carries that cause, so nothing refused the open. The row is raised — `StructureRules`
reports it — but the refusal its cell published had no site. That column was also narrower than the operation
set, so a row blocking only the edit-open, or only a controller transfer, had nowhere to say so at all. Both
of §4's generated cells are rendered from the declaration today, and every head has its own published word.

**What ships is the declaration; no fourth `ValidationSeverity` was added:**

1. `RefusedOperations` on the entry — the operation heads this row refuses, empty for the rows that refuse
   nothing. **The vocabulary is the head set, not §4's Blocks column**: `io.load`, `io.save`, `edit.open`,
   `import.catalog`, `bridge.download`, `bridge.upload`. `edit.open` is the head the column could not express
   when the field was added, and the one a row already needed — `attr-undeclared` refuses the edit-open as well
   as the save, and its declaration said so in a comment precisely because §4 had nowhere to put it. Point 3
   below is how the column caught up. The declarations are DERIVED
   from the registries that raise each refusal, and `RefusalLabelDriftTests` holds the two equal in both
   directions, so this is not a second hand-kept copy of the prose column.
2. The panel tier derives as `Severity == Error && RefusedOperations is not empty` → *Fatale fejl*, and the
   fact reaches the host ON the finding (`ValidationFinding.RefusedOperations`), never by the GUI reading the
   catalogue, which L5 forbids. The panel keys its tiers on its own `ProblemsTier`, not on `ValidationSeverity`
   — two tiers share one severity, which a severity-keyed table cannot express.
3. §4's **Blocks** column is generated from the declaration and compared by test, with an `[Explicit]`
   regenerator beside the other two. It publishes ONE WORD PER HEAD, so the view can express every
   declaration; a gate fails a head with no word rather than letting the column omit it. Rendering only the
   four file-lifecycle labels was tried first and was a mistake worth recording: `id-duplicate-token` then
   published `—` while the panel listed it as fatal and the export wrote `blocks="edit.open"` — one
   declaration with three published answers, which is the ambiguity generating the column was meant to end.
   The changeover moved five cells: `root-version` lost the *Open* it never refused; `attr-undeclared` gained
   the *Edit-open* half §4 had nowhere to put; `id-duplicate-token` gained *Edit-open*; and the two controller
   rows moved off *Import*/*Export* onto *Download*/*Upload*, which had put one word on two unrelated
   operations three rows apart in one table. It also exposed a defect in the neighbouring column, since a row
   published *Fatal error* with no operation violates §7's first MUST; `root-version`'s Severity cell now
   reads *Error*, which is what its declaration always said.

Why a declared fact and not a severity member: `CatalogDisposition`'s own rationale is that *"fatal" was
carrying two unrelated meanings* — **the operation cannot proceed** versus **catastrophic in effect** — and a
fourth severity re-merges them. The two are genuinely independent: `save-roundtrip-mismatch` refuses the save
and is no finding at all, while `attr-undeclared` refuses the save **and** is a finding. Declaring the refusal
also costs no `ValidationSeverity` ordinal change (the enum is public API) and moves no `@severity` byte in the
findings oracles, and it gives the send gate a better question than *"any Error"* to ask. The shipped gate
still asks that one — `app.openvisual.validation-errors-block-send` withholds the transfer on any Error
finding — but the fact it would need in order to name the blocked operation now travels on the finding,
which is the whole point of a row that is fatal to one operation and to nothing else.

**The gate asks a second question beside that one.** `app.openvisual.validation-incomplete-blocks-send`
withholds the transfer while the latest completed run carried FAULTS — a rule crashed, so the checklist
reached no verdict. It is a separate row rather than a widening of the one above because the two ask the
reader for different things: the errors sentence says repair what the panel lists, and here the panel's list
is precisely what cannot be trusted. A faulted run that found nothing would otherwise refuse the transfer
while asking for zero repairs. The SDK refuses the same condition one layer down under its own
`save-validation-incomplete`, so an upload is stopped whether or not a host gate ran.

**The open-blockers are a different surface.** Listing them means a *"this file would not open"* view over
one refusal chain, not the project findings list. Do not fold them into the panel. (Eleven of the twelve
`io.load` declarations can actually stop an open; the twelfth, `load-truncated`, is `RuledOut` and never
raised — see [§12](#12-changing-retiring-and-ruling-out).)

### 11.2 Information — the fourth disposition

`CatalogDisposition.Info` ships, appended after `Refusal` so no existing ordinal moved — declaration order is
therefore not tier order, which the member's own doc-comment says. `ProblemCatalogEntry.Severity` maps it to
`ValidationSeverity.Info`, `ValidationGate.Infos` answers it beside `Errors` and `Warnings`, and §1's
per-category table carries an **Information** column. The Problemer panel was already plumbed for the tier.

**Shipped rows declare it**, and problem-catalogue.md collects them in its own §5b rather than in §5.
The two sections are separated by what the reader is asked to do: §5's rows are advisory because the author
has to judge them, which is what its *Why it may be fine* column states, and an Information row asks for no
judgement at all — so §5b carries no such column. Reclassifying an EXISTING row into Information is still a
separate edit with its own oracle diff; that is what made the enum addition itself oracle-neutral.

To author one: declare `CatalogDisposition.Info` on the entry like any other disposition, and add the row to
§5b rather than to §4 or §5. Two gates cover the derivation, from opposite ends:

- `ProblemCatalogTests.EveryDispositionDerivesItsSeverityWhetherOrNotARowDeclaresIt` seeds the whole
  disposition axis, so the mapping is total by construction and a fifth disposition added without a severity
  fails on the day it is added rather than on the day a row first declares it.
- `CatalogCompletenessTests.EveryRecordedFindingCarriesItsEntrysSeverityAndCategory` checks the other
  direction over the recording, comparing each finding against `entry.Severity` — **the catalogue's own
  derivation, never a copy of it** in the test. An earlier version did keep a local mapping, and a helper that
  sent every non-Error disposition to `Warning` would have failed the first Info finding the corpus witnessed
  on the TEST's stale copy rather than on anything the row did. That helper is gone; reading `entry.Severity`
  is what replaced it.

*(Corrected 2026-08-27: this section previously named a
`CatalogCompletenessTests.TheExpectedSeverityMappingCoversEveryFindingDisposition`, which does not exist —
the fix it describes was made by deleting the local mapping, not by adding a test.)*

The first row to declare it was `product-s0-instrument-only` (ADR), which reports that a placed S0 meter is a
read-out instrument rather than an automation source. It is witnessed by the three authentic corpus files that
carry an `s0_device`, so it moved three finding lines in `tests/testdata/validation/` — and no root line,
because the export root's `severities` list is computed from the export options and already named `Info`.

### 11.3 How the export answers the fourth tier

The export answers it BOTH ways, because the two questions are different.

- **Per finding**, a `<finding>` whose row refuses an operation carries `blocks="io.save edit.open"` — the
  heads themselves, not §4's narrower labels. The schema enumerates the six, so a reader can switch on them
  exhaustively. Five oracle lines moved, each by the added attribute alone.
- **Per document**, the root always carries `error_tiers` — a list of `refusing`, `ordinary`, both, or neither.
  `@severities` cannot answer on its own: *Fatal fejl* and *Fejl* are both `Error`, so a list filtered to the
  refusing half and a list holding every error record the same severity set. A producer with no such split
  emits the value derived from `@severities`, so the two agree by construction; a producer that DOES split has
  the pair enforced instead — `FindingExportWriter.Write` throws `ArgumentException` when including either
  error tier disagrees with `Error`'s presence among the severities, so a reader never meets a file whose two
  statements about one filter contradict each other. All tiers off stays legal: an export of nothing is honest.

  It is a REQUIRED LIST rather than an optional flag, and that is the second design this attribute had. The
  first was a boolean emitted only when the two halves were filtered differently, so its ABSENCE carried the
  meaning "both included" — which buys byte-stable oracle root lines and costs correctness: every ordinary
  reading of an optional boolean (deserialising to `bool`, `(bool?)a ?? false`) turns that absence into
  `false`, handing a reader the exact opposite of the truth for the commonest file in the corpus. No amount of
  schema documentation fixes a default that lies, so the 18 oracle root lines were regenerated once instead.

The panel supplies both filter states rather than one flag (`FindingExportOptions.ErrorTiers`), so the writer
decides for itself whether they differ and the format's rule stays inside the format's definition.

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
