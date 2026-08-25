# ADR-002: Thick SDK, thin apps — two service tiers own the business logic and validation; GUI/CLI frontends present it

## Status

Decided — 2026-07-19. Records the standing architecture, in force since the SDK's early versions; documented
retrospectively.

Amended — 2026-07-20 (facade single-door rulings), 2026-08-02 (crudarch: two execution doors) and
2026-08-24 (validation folded in as question B, renaming this file) — the substance of each is in the Decision.

Revisit triggers: (a) a vendor firmware/WSDL revision changing the controller's service surface — re-price the 1-1
mirror; (b) a frontend that cannot reference the SDK in-process (non-.NET or remote) — that calls for a hosted
service boundary, not more tiers, and validation then rides that same boundary rather than frontend copies; (c) a
second frontend re-implementing read-side *projection* (not display interpretation, which is frontend-owned by
design) — the query/projection gap turning into duplication; (d) an application service accreting
frontend-specific types or per-frontend behavior variants; (e) a frontend needing rule variance that the
host-owned `app.*` problem family cannot express — per-frontend rule policy would end the single-truth property;
(f) end-user text in a second language — single-language sentence templates then need a localization strategy
before more surfaces bind them; (g) a second frontend growing validation
composition glue the dependency rules cannot see — evidence that the facade's door inventory is incomplete.

## Decision at a glance

All device integration, protocol handling, project-file logic and business logic live in the single shared
`ihcclient` library, split into an API-service tier that mirrors the controller's SOAP services 1-1 behind SDK
models and an application-service tier of tech-agnostic, use-case-tailored business facades — one per application
type, each a uniform, consistent, easy-to-understand high-level entry point that does the hard work over the
deliberately more general lower-level APIs. **Business logic** here is the whole of it, not just workflow:
validation as one rule set with one catalogue of coded problems, the undo/redo history with dirty and version
state, and the legality of every edit — all authored once and reached only through the facade doors. The SDK
states these as GUI-free *facts*, never as interface: it knows of no menu, toolbar or window, and answers "may
this command run on this target, and if not, why" without assuming anyone is drawing it. Every application and
tool above the SDK is then a thin GUI or command-line shell that wires presentation to a facade, renders verdicts
and sentences whole, and turns those facts into an interface — enablement, layout, graphics, interactivity — which
is the part the SDK deliberately does not own.

## Context

**Current state — layering** (2026-07-19; version 0.8.1, `net10.0`; source-referenced SDK, no NuGet package):

- `ihcclient` is the SDK every first-party project sits on and the bottom of the reference graph; 15 projects
  reference it — 1 application, 5 utilities, 2 examples, 6 test suites, and `shared/ihc_appbootstrap`, the
  Avalonia bootstrap library the two GUIs share. `ihc_httpproxyrecorder` and `ihc_project_io_extractor` do not.
- API tier: 15 service classes (`ihcclient/src/api/services/`), one per generated SOAP contract, each delegating to
  a private nested `SoapImpl` adapter; shared contract `IIHCApiService` (`serviceBase.cs:15`). The generated
  `Ihc.Soap.*` layer (~17k lines) is referenced by nothing outside `ihcclient` except white-box unit tests.
- App tier: 4 services — `AdminAppService`, `InformationAppService`, `LabAppService` (`Ihc.App`) and
  `ProjectAppService` (`Ihc.Vis`, facade of the offline project engine) — under `IIHCAppService`/`AppServiceBase`
  (`src/app/services/serviceBase.cs`), composing API-service interfaces and auto-authenticating on demand.
- Frontends are measurably thin: `ihc_admin` is argument parsing plus `AdminAppService` calls; `ihc_lab`'s
  view-model synchronizes GUI state with `LabAppService`; `ihc_openvisual` routes every mutation through the
  `Ihc.Vis.Session` command layer (each command executing via `project.Edit()`) driven from an Avalonia-free
  session wrapper — obtaining each command from the `ProjectAppService.Commands` gateway and executing it through
  the `IProjectDocument` port from `ProjectAppService.OpenDocument`, with the stateless `ProjectAppService.Apply`
  serving one-shot callers (see Decision).
- Enforcement is largely mechanical, across two mechanisms that ADR-004 divides by whether a ban can state the
  rule exactly. ArchUnitNET (`tests/safe_architecture_tests/`) pins `Ihc.Vis` ↛ `Ihc.Soap`
  (`IhcClientArchitectureTests.cs:465`), SDK ↛ Avalonia (`:551`), the downward tier direction
  (`ApiServiceLayer_DoesNotDependOn_AppLayer`, `:542`) with its upward complement (`AppLayer_DoesNotDependOn_Soap`,
  `:489`), and the GUI rules that need sight of our own evolving API — view-models ↛ Avalonia, no command
  construction, no GUI call to the stateless facade, views never driving the session
  (`OpenVisualArchitectureTests.cs:108,138,165,532`). Compile-time banned symbols carry the whole-namespace GUI
  prohibitions that moved out of the suite on 2026-08-17: `System.Xml`, `Ihc.Vis.Io`, `Ihc.Vis.Editing`,
  `Ihc.Vis.Reporting` and the concrete `Ihc.Vis.Session.ProjectDocumentSession`
  (`applications/ihc_openvisual/BannedSymbols.txt`), plus repo-wide `Ihc.Soap` (root `BannedSymbols.txt`) — with an
  architecture test pinning that those ban targets still resolve, since a ban entry is a string.
- What stays review-only is the *absence of complex logic* in a frontend — a complexity property neither mechanism
  can judge (`ARCHITECTURE.md`, design challenge 7).
- Two documented deviations are resolved or retiring: command-selection and legality logic that had accumulated in
  OpenVisual's `ProjectWorkflow` and view-models (design challenge 7) has moved into the SDK — the
  `ProjectAppService.Commands` gateway mints the commands and the `IProjectDocument` port runs them for the GUI,
  leaving `ProjectWorkflow` with document lifecycle only — and `ihc_project_io_extractor`'s standalone `.vis`
  parser is deprecated. Frontend-owned *display interpretation* of vendor values is not a deviation but a ruling
  (see Decision); the standalone `ihc_httpproxyrecorder` operates below the SDK by design.
- ADR-001 (threading and concurrency model) builds on this structure; the "SDK must not reference a GUI framework" rule it
  cites as a standing invariant is owned here.

**Current state — validation** (2026-08-24; the validation engine present on the working branch):

- A catalogue-driven rule engine (`Ihc.Vis.Validation`) executes one registered rule set for the whole-project
  run; the same findings render into the documentation reports (`FullModeShapes.cs:100`), and the save path
  re-runs validation below any frontend (`ProjectAppService.cs:517-519`).
- The facade exposes the doors frontends consume: `Validate`/`ValidateCategorized` (`ProjectAppService.cs:913,940`),
  the interactive `IProjectDocument` port from `OpenDocument` (`:219`), whose `CanApply`/`Preview` are the legality
  probes an interactive frontend uses; the same-named stateless probes `ProjectAppService.CanApply`/`Preview`
  (`:272,289`), which serve one-shot callers only; dialog descriptors (`GetProductDialog`, `:639`), finished report
  bytes (`:963`), and the `Commands` factory gateway (`:197`).
- OpenVisual consumes them as designed: drag-drop and tree operations ask the *document's* `CanApply` on
  factory-minted commands (`TreeDragDropController.cs:62,83`, `MainWindowViewModel.cs:1460` — `session.CanApply`,
  never the stateless facade the GUI is banned from); dialogs bind SDK descriptors and field rules
  (`ProductDialogViewModel.cs:77-85`) and SDK-declared numeric bounds through one forwarder
  (`Views/NumericFieldBounds.cs:25`) that replaced hard-coded XAML clamps; SDK refusal sentences are forwarded,
  never restated (`TreeDragDropController.cs:59-63`).
- The dependency direction is machine-enforced: the GUI cannot run the engine or read the problem catalogue
  (`ValidationLayerArchitectureTests.cs:172`), cannot construct commands or drive the session from views
  (`OpenVisualArchitectureTests.cs:138,532`), and engine diagnostics cannot become installer-facing text (`:455`).
- Among the projects above, the headless visual test suite drives the real facade as a second, UI-free frontend.
- At decision time, boundary residue existed and marks the failure shape this decision closes off: one GUI-owned
  composition gluing an SDK decision to an SDK sentence, and a handful of gesture gates carrying GUI-authored
  refusal text beside SDK verdicts.

**Decision forces**: the vendor ships no SDK, and the generated SOAP bindings (positional parameter names, WSDL
artifacts, churn whenever the WSDL changes) are unfit as a public surface; multiple heterogeneous frontends must
behave identically, while logic in a view-model is headlessly untestable, unreachable from console tooling, and
re-implemented by the next frontend; the test policy forbids harming a live controller, so business logic must run
against faked controller I/O; a sole maintainer cannot afford drifting copies (repo pattern priority: DRY before
KISS); and part of the domain — the `.vis` project engine — mirrors no controller service at all, so a 1-1 tier
alone cannot house it. For validation specifically, vendor parity requires one answer per question — a dialog, a
menu gate, a findings list, a report, and a future frontend must agree, and each Danish sentence must be authored
exactly once; and validation correctness is pinned by byte-exact characterization oracles that only a UI-free
engine can regenerate.

**Reversibility**: one-way door on both counts — the tier seam carries the whole controller-free test strategy, all
consumers, and the documentation rules, so relocating logic or collapsing tiers is repo-wide restructuring; and the
validation placement decides where every future rule, threshold, and sentence lands, so moving validation out of
the SDK later means re-homing the rule set, its oracles, and every consumer.

**Assumptions**:

| Assumption | Type | Confidence | Source | Validation trigger |
| --- | --- | --- | --- | --- |
| All frontends consume the SDK in-process as .NET | business | high | repo scope — source-referenced C# library | a non-.NET or remote-frontend requirement |
| Application types stay few; app services stay tech-agnostic | technical | medium | team experience — four app services to date | an app service acquiring frontend-specific types |
| The controller's SOAP surface is effectively frozen | environmental | high | team experience — legacy vendor platform | a firmware/WSDL revision |
| The thin-frontend rule is enforceable by review and tests | operational | medium | `ARCHITECTURE.md`, design challenge 7 | the read-side projection gap reaching a second frontend |
| The surface-policy/legality line holds | operational | medium | machine-checked since 2026-08-24: gate-enabled implies `CanApply.Ok`, asserted over the registry rows mintable without user input (`GateAgreesWithCanApplyTests`). Its first run fired this row's own trigger -- three paste-gate states enabled what the SDK refuses -- and the gate was corrected to probe `CanApply` | a gate found encoding a legality fact in a direction the assertion cannot see (a gate STRICTER than the SDK, which stays policy) |
| No frontend needs rule variance beyond host-owned `app.*` refusals | technical | medium | one shipping GUI to date | an app-specific rule request |
| Danish remains the only end-user language | environmental | high | product specification | a localization requirement |

**Constraints**:

| Constraint | Category | Provenance |
| --- | --- | --- |
| The controller speaks SOAP only; modern .NET has no built-in SOAP client stack | technical | given |
| Vendor file formats (`.vis`/`.def`/`.ifb`) are a closed compatibility contract | technical | given |
| Tests must be incapable of harming a live controller | organizational | chosen — standing repo policy |
| The session layer must not depend on the validation engine | technical | chosen — commit paths stay engine-free |
| Problem-code families are fixed: SDK findings and `edit.*` refusals vs host `app.*` | technical | chosen — ownership decidable from the code |
| End-user finding/refusal text is rendered whole, never re-derived | technical | chosen — sentence governance |
| Sole-maintainer capacity | organizational | given |

## Evaluation Criteria

Two questions are decided here, and each has its own criteria: **A — where business logic lives** (the tiering) and
**B — where validation lives** (the same seam, applied to the rule set). Both orderings follow the repo's stated
DRY-before-KISS priority.

**A — layering.** Priority order (highest first); the built-in conflict is reuse/testability (favoring a dedicated
logic tier) against consumer simplicity (favoring fewer concepts):

1. **Frontend reuse** — identical behavior available to GUI, console, and future frontends without duplication.
2. **Testability at safe seams** — business logic exercisable headlessly, fakes only at controller I/O.
3. **Vendor-churn isolation** — WSDL artifacts invisible to consumers and frontends.
4. **Consumer simplicity** — direct, unopinionated access for integrators wanting only the controller API.
5. **Solo-maintainability** — cost of carrying and evolving the structure with one maintainer.

**B — validation placement.** Priority order (highest first); the built-in conflict is single-truth consistency
against frontend flexibility: the more validation the SDK owns, the less a frontend can vary or hotfix locally:

1. **Single truth** — every surface (dialog, gate, findings list, report, next frontend) gives one verdict and
   one sentence per question.
2. **Headless testability** — rules and sentences exercisable, and their oracles regenerable, without any UI.
3. **Next-frontend cost** — what a new GUI, console, or service frontend pays for full validation.
4. **Boundary enforceability** — how much of the placement machines can police.
5. **Frontend flexibility** — a frontend's freedom over presentation and its local iteration speed.

## Options

### Question A — where business logic lives

#### A1. Thick SDK, two service tiers, thin frontends (chosen)

All integration and logic in `ihcclient`. Tier 1: one service class per controller SOAP service, operations
mirrored 1-1 and raised to SDK models and async idioms, generated bindings consumed only via private adapters,
plus a small set of cross-cutting helpers (session, cookies, long-poll streaming). Tier 2: tech-agnostic
application services, one per application type, composing tier-1 interfaces; SOAP-less engines sit behind tier-2
facades. Frontends hold presentation and wiring only; consumers pick their tier.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Frontend reuse | 5/5 | 14 consumers share one implementation; OpenVisual ships as UI plus session orchestration only |
| Testability | 5/5 | Fake `IIHCApiService`, run real app services — the entire safe-test suite stands on this seam |
| Churn isolation | 5/5 | `Ihc.Soap.*` referenced nowhere outside the SDK except white-box tests |
| Consumer simplicity | 4/5 | Raw tier-1 access stays direct and unopinionated, but consumers face two tiers plus public engine surfaces |
| Solo-maintainability | 4/5 | Changes localize per tier; cost: controller features touch two tiers, and 15 mirror wrappers are carried |
| | **Total: 23/25** | **Trade-offs**: two-tier toll on every controller-facing feature; thin-frontend rule needs active policing |

#### A2. Thin SDK — API mirror only; business logic in each frontend

The SDK stops at tier 1. Admin change-tracking, information aggregation, lab invocation, and project workflows are
implemented per application, in view-models or app-local helpers.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Frontend reuse | 1/5 | Every frontend re-implements; nine non-test consumers exist today |
| Testability | 2/5 | Logic lands in UI-adjacent per-app code; headless coverage needs per-app rigs — the documented command-selection accumulation in OpenVisual shows this failure mode in miniature |
| Churn isolation | 5/5 | The mirror tier still encapsulates WSDL artifacts |
| Consumer simplicity | 3/5 | Smallest SDK surface, but every consumer must build behavior before getting value |
| Solo-maintainability | 1/5 | N drifting copies of the same logic under one maintainer |
| | **Total: 12/25** | **Trade-offs**: least SDK code; total system cost grows with every frontend |

#### A3. Single rich tier — use-case logic folded into the API services

One service layer: each controller service class also carries application behavior (auto-authentication, change
tracking, aggregation). No separate app tier.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Frontend reuse | 4/5 | Logic is shared, but only where it maps to one controller service — cross-service use cases (admin settings span user and configuration services) and the SOAP-less `.vis` engine have no home |
| Testability | 2/5 | Logic and wire I/O live in the same class; faking controller I/O then fakes away the business logic, so the seam must move inside classes |
| Churn isolation | 3/5 | WSDL churn lands in classes that also hold business rules |
| Consumer simplicity | 3/5 | One tier, but raw calls carry opinions (auto-auth, tracking) integrators cannot decline, and the 1-1 map to the controller's services blurs |
| Solo-maintainability | 2/5 | 15 classes of mixed transport and use-case concerns |
| | **Total: 14/25** | **Trade-offs**: fewest types; in practice a second tier re-emerges for cross-service and SOAP-less logic |

### Question B — where validation lives

#### B1. Validation as SDK business logic behind the facade doors (chosen)

One rule set and one catalogue of coded problems with their sentences, executed and exposed only by the SDK.
Whole-project runs, legality probes, dialog descriptors with field metadata, and report rendering are facade
doors; frontends bind the value types those doors return and render verdicts whole. Surface policy — which
verdicts appear where, omit versus grey, availability wording — stays frontend-owned, as does the host's own
`app.*` problem family for host-only concerns.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Single truth | 5/5 | One rule set; every sentence declared once; save path, reports, and GUI consume the same findings |
| Headless testability | 5/5 | Engine is UI-free; characterization corpus and report oracles regenerate without a frontend |
| Next-frontend cost | 5/5 | The doors are already consumed by the shipping GUI and the headless suite; a new frontend wires, it does not implement |
| Boundary enforceability | 5/5 | Dependency direction and no-engine-in-GUI are machine-checked, and so is the policy-vs-legality line inside availability gates: gate-enabled implies `CanApply.Ok` is asserted over the registry rows mintable without user input. What remains unmechanised is the STRICT direction, which is policy and deliberately GUI-owned |
| Frontend flexibility | 3/5 | Rule changes ride the SDK's cadence; variance only through `app.*` refusals and surface policy |
| | **Total: 23/25** | **Trade-offs**: the SDK carries generalization and multi-face surface ahead of a second GUI |

#### B2. Frontend-owned validation over the raw model

The SDK exposes model and persistence; each application implements the checks it needs — dialog rules, gate
conditions, findings — in view-model or app-local code. The common smart-client shape.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Single truth | 1/5 | N drifting copies; wording re-authored per app; a repo precedent (the deprecated standalone `.vis` parser) shows the drift in miniature |
| Headless testability | 2/5 | Logic lands in UI-adjacent code; per-app rigs replace the shared oracle corpus |
| Next-frontend cost | 1/5 | Every frontend rebuilds every face before shipping value |
| Boundary enforceability | 2/5 | No shared boundary to pin; each app grows its own conventions |
| Frontend flexibility | 5/5 | Each app varies and hotfixes freely |
| | **Total: 11/25** | **Trade-offs**: fastest first-app iteration; total cost grows with every frontend, and the SDK's own save/report paths still need checks of their own — a split brain by construction |

#### B3. Shared frontend validation library

Validation implemented once, but above the SDK: a reusable library beside the applications (the repo already
shares bootstrap code between its two GUIs), consumed by each frontend.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Single truth | 3/5 | Frontends sharing the library agree, but the SDK's own consumers — the save gate, the upload gate, the report appendix — sit below it and would need a second implementation or none |
| Headless testability | 3/5 | Headless if kept view-model-free, but the oracle corpus and the engine's shared analyses live with the SDK |
| Next-frontend cost | 3/5 | Reuse reaches only frontends on the same stack and runtime |
| Boundary enforceability | 2/5 | A library-above-SDK boundary has no existing test battery, and the below-SDK consumers exert constant inversion pressure |
| Frontend flexibility | 4/5 | Closer to the apps; still one shared cadence among them |
| | **Total: 15/25** | **Trade-offs**: dependency inversion for below-SDK consumers is structural, not incidental; one truth ends up with two homes |

## Decision

Keep and formalize the standing structure (A1), and place validation inside it (B1).

**Layering — services and frontends:**

- `ihcclient` is the shared SDK and the bottom of the dependency graph. All device integration, protocol
  handling, project-file and business logic lives there — never in a frontend.
- **What "business logic" covers**, named so the boundary is decidable in the cases a frontend most often tries to
  keep: *validation* in all its faces (question B below); *undo/redo* — the history, its labels, and the
  dirty/version state, owned by the `IProjectDocument` session (the two execution doors below), never a
  frontend-side stack; *edit legality* — whether a given command may run on a given target and the coded reason
  when it may not, answered by `CanApply`/`Preview` on a factory-minted command: on the document for an
  interactive frontend, on the stateless facade for a one-shot caller; and the workflow, addressing,
  classification and project-file semantics beneath them. **The SDK states each of these as a fact, not as an
  interface**: the application tier is tech-agnostic and names no menu, toolbar, window or gesture — it does not
  know a GUI exists, it makes one possible. What a frontend keeps is exactly the interface those facts enable:
  turning a legality answer into an enabled, greyed or omitted item on a particular surface, that item's label
  wording and ordering — never the wording of an SDK sentence, which is rendered whole — and the display
  interpretation of vendor values.
- **API-service tier**: one service class per controller web service, operations mirrored 1-1 and exposed through
  SDK-owned models and async idioms, plus a small set of cross-cutting helpers. Generated SOAP artifacts are
  consumed only through private per-service adapters (anti-corruption layer) and never appear in public
  signatures.
- **Application-service tier**: tech-agnostic backends, each targeted at a type of application (administration,
  controller information, lab exploration, project authoring). Each is a **use-case-tailored business facade** — a
  uniform, high-quality, maintainable, easy-to-understand high-level entry point that does all the hard work
  (business logic and controller integration) so its frontend is left with simple wiring, while the lower-level
  APIs it composes stay deliberately more general and directly usable by advanced consumers. They compose
  API-service interfaces and SDK engines; dependencies point strictly downward — API services never know
  application services. Cross-service and SOAP-less domain logic lives at this level or below.
- **The authoring door** is the exemplar (owner rulings, 2026-07-20; realized). `ProjectAppService` is the single,
  consistent entry point for IHC project (`.vis`) CRUD, tailored to that use case and built to be the backend
  `ihc_openvisual` — and future project-related apps — sit on. It stays **one discoverable door**: it exposes the
  `ProjectCommands` gateway (`ProjectAppService.Commands`) so command discovery starts at exactly one class, while
  `Ihc.Vis.Session` command objects remain the sole mutation vocabulary beneath it; the edit vocabulary is a
  **complete, uniform set of SDK command factories** — a frontend obtains every command from a factory and never
  constructs one directly (uniform discoverability outranks the no-trivial-delegation rule on this published
  surface; a reflection test enforces completeness and the `CompositeCommand` exclusion); and the SDK read surface
  exposes **raw values plus legality, selection and mutation**, while *display* interpretation of vendor values
  (labels, display defaults, translations) is deliberately frontend-owned presentation policy.
- **Two execution doors** sit behind that one entry point (crudarch redesign, 2026-08-02). An interactive frontend
  calls `OpenDocument(Project, HistoryPolicy?, bool startClean)` and drives every edit through the returned
  `IProjectDocument` — one lock-serialized session per open file owning labelled undo/redo, dirty/version and
  change events (ADR-001 holds the threading contract). One-shot callers (console tools, tests) use the stateless
  `ProjectAppService.Apply`/`CanApply`/`Preview`, which run one command on a throwaway session; an architecture
  test bans the GUI assembly from calling those members, so an interactive edit cannot bypass the document. Either
  way commands are minted only by the `Commands` factories. In the GUI, command *availability* is single-sourced
  in a declarative registry (`CommandRegistry`) — presentation policy over SDK legality.
- **Frontends** — applications, utilities, examples — contain presentation and wiring only; logic worth testing or
  reusing is pushed down into the SDK. Consumers choose their entry level: the API tier for direct controller
  access, the application tier for ready-made behavior; selected lower-level engine surfaces stay public for
  advanced use.

**Validation — one rule set behind the same doors:**

- The SDK owns every validation face as business logic: whole-project findings, command/gesture legality,
  field-value rules together with the metadata a dialog binds, and the Danish sentence of every finding and
  refusal — declared once, as coded problems.
- A frontend reaches validation only through the facade doors — whole-project runs, legality probes and the
  document port, dialog descriptors and field metadata, report generation. New validation capability ships as a
  new or widened door, never as frontend code.
- A frontend binds validation *values* and renders their sentences whole. It never runs the engine, assembles
  rules, reads the catalogue, re-words a sentence, or composes SDK validation pieces into glue of its own — a
  composition worth having belongs in the facade as a door of its own.
- Deliberately frontend-owned, unchanged from the layering decision above: *display* interpretation of vendor
  values, and command *availability* as presentation policy over SDK legality — an availability gate may combine
  SDK-provided facts and legality probes, and may not encode a legality fact of its own.
- Host-only concerns refuse with the host's own `app.*` problem family; every other code is the SDK's.

Confidence: high on both questions.

*Layering* — this documents an implemented architecture validated by every consuming project and by the
controller-free test strategy built on the tier seam; the counterfactual options are scored against failure modes
already observable in-repo. **Two different things are called "read side", and they land on opposite sides of the
seam.** *Query/projection* — deriving a fact about the model — is business logic and pushes down like writes;
*display interpretation* — rendering a raw value into a label a human reads (display defaults, translations, the
live `%P`/`%S` operand substitution) — is presentation policy and stays frontend-owned by design. Top unresolved
uncertainty: whether the projection half pushes down as cleanly as writes. The write-side move has landed; the
read-side one is planned, and revisit trigger (c) watches for it duplicating into a second frontend.

*Validation* — the placement is implemented and exercised by the shipping GUI, the report path, and the headless
suites, and the counter-options score against failure modes already observed in-repo (the deprecated standalone
parser; the command-selection accumulation recorded above). The uncertainty this section carried — whether rule-declared
field metadata for fields the file's own grammar leaves unbounded reaches dialogs through the same door as
grammar-declared bounds, or ends up needing a second producer path — is **RESOLVED: the same door.**
`ProjectAppService.DescribeField(RuleTarget)` answers it, and no second producer path was needed, for a reason that
was already true before the member existed: grammar-declared bounds arrive at the dialogs AS a
`FieldConstraintMetadata` (`ElementView.DeclaredBounds`, read in `DialogReadViews`), so a rule-declared answer in
the same currency needs no new pipe — a caller that wants both merges two values of one type. The placement follows
from this ADR rather than from a cycle argument: composing a `RuleSet` with a target is business logic, the service
is where the SDK composes its faces, and no other type may hold the rule set. What remains open is narrower and is
recorded as such: the shipped dialogs still bind the descriptor's own `field.Rule` and `DeclaredBounds`, so
re-binding them to the merged metadata is future work, not a gap in the door.

## Implications

### Positive

- Behavior and wording parity across frontends and surfaces by construction; a new frontend costs UI work only —
  findings, legality, dialog metadata and report rendering come for wiring cost (long-term, cross-cutting).
- The controller-free test strategy falls out of the seam: fake the API tier, exercise real business logic
  (cross-cutting).
- Validation stays testable and regenerable headlessly; oracle-pinned correctness survives UI change
  (cross-cutting).
- WSDL churn is absorbed inside private adapters; frontends and consumers never see it (local, long-term).
- Integrators keep direct controller access while application authors get ready-made workflows.

### Negative

- Every controller-facing feature pays a two-tier toll — wrapper, models and logic in separate places (recurring
  per feature; permanent in aggregate).
- The 1-1 mirror commits the SDK to carrying wrappers for all 15 controller services, several rarely used and only
  partially implemented (long-term, local).
- Thin-frontend discipline is only partly machine-enforced; both documented deviations needed deliberate cleanup
  (a deprecation, a refactoring) rather than being prevented, and review-only compliance remains a drift risk as
  frontends multiply (cross-cutting; reversible per instance).
- "One application service per application type" has no growth gate; scope discipline is review-only (long-term).
- Every rule change rides the SDK; a frontend cannot vary or hotfix a rule locally — `app.*` refusals and surface
  policy are the only pressure valves (long-term, cross-cutting, irreversible while the placement stands).
- The policy-vs-legality line inside availability gates is machine-checked in ONE direction only: a gate that
  ENABLES what the SDK refuses now fails a test, but a gate that refuses what the SDK would allow is policy and
  stays unmechanised by design. So a gate can still accrete strictness silently, and the decision-time residue
  showed that legality accretion does happen — the assertion's first run caught three such states
  (recurring; reversible per instance).
- The SDK bears dialog-metadata and multi-face validation surface ahead of a second GUI existing (cost carried
  now, repaid at the next frontend).

### Neutral

- Lower-level `Ihc.Vis` surfaces stay public for advanced non-GUI consumers — the application tier is the normal
  door, not the only one, and the GUI's only one.
- Generated SOAP types remain technically `public` though out of contract (invariant 9's documented gap).
- Where a fact serves both a gesture gate and a rule (a capacity cap, a declared bound), it lives below both in
  the shared model, so neither side copies the other.

## Confirmation

- Architecture tests (ArchUnitNET in `safe_architecture_tests`, run by CI on all platforms): rules pin
  SDK ↛ Avalonia, `Ihc.Vis` ↛ `Ihc.Soap`, and the API-tier ↛ app-tier direction this record called for
  (`ApiServiceLayer_DoesNotDependOn_AppLayer`).
- Compile-time banned symbols (ADR-004), failing the build rather than a test: the GUI may not reach `System.Xml`,
  `Ihc.Vis.Io`, `Ihc.Vis.Editing`, `Ihc.Vis.Reporting` or the concrete `ProjectDocumentSession`, and nothing
  outside the SDK may bind `Ihc.Soap`. An architecture test pins that each ban target still resolves.
- Architecture fitness tests for the validation placement: the validation-layer direction rules and the OpenVisual
  battery — no engine execution or catalogue reads in the GUI, no command construction, views never drive the
  session, no GUI call to the stateless `Apply`/`CanApply`/`Preview` facade, engine diagnostics never become
  user-facing text.
- Refusal-label drift tests: sentence copies below the engine stay equal to the catalogue's templates.
- Code-review checklist: no `Ihc.Soap` types in public signatures; no business logic in view-models or
  `Program.Main` (vendor *display* interpretation is the sanctioned exception — frontend-owned by design);
  project-edit commands obtained only via the SDK command factories, never constructed in a frontend; test fakes
  only at `IIHCApiService`/`ICatalog` — application services always real; no GUI-authored legality or validation
  glue; new validation capability lands behind a facade door; verdict sentences rendered whole; no frontend-side
  undo/redo stack or dirty-state bookkeeping beside the document's; an enablement gate reads an SDK legality
  answer rather than restating the rule behind it.

## Consultation

Sole maintainer stated the decision and its two-tier structure when commissioning this record (2026-07-19).
Content grounded in `ARCHITECTURE.md`, `CLAUDE.md`, SDK doc comments, and a code inventory of all consuming
projects. Retrospective documentation of a standing decision; no other stakeholders.

2026-07-20: sole maintainer ruled the four facade-realization tradeoffs as explicit multiple-choice questions
(one discoverable door; factories for every command; display interpretation frontend-owned; add
`ExportFunctionBlock` and a `CreateNew` locality-language option now). Three of the four rulings overrode the
analyst's recommendation, and in one consistent direction: maximum uniformity and single-point discoverability on
the *operation* surface (the first two), against a hard raw-values-only boundary on the *read/display* surface (the
third). The rulings themselves are restated in the Decision above.

2026-08-24: sole maintainer commissioned the validation record and ruled its scope (validation, extending this
layering decision rather than restating it), then ruled that it belongs *inside* this record rather than beside
it — hence the merge and the rename. Grounded in `ARCHITECTURE.md`, `CLAUDE.md`, the OpenVisual product
specification, the architecture-test suites, and a code inventory of the facade doors and their GUI call sites.
The design was also re-tested adversarially against the planned future validation population, and the outcome was
that **every decision stands**, one normative sentence gaining a qualifier. Because the result merged into this
record, no ADR-005 was published, so that number stays free for the next decision.
