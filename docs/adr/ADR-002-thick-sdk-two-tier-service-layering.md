# ADR-002: Thick-SDK layering — two service tiers in ihcclient, thin GUI/CLI frontends

## Status

Decided — 2026-07-19. Records the standing architecture, in force since the SDK's early versions; documented
retrospectively.

Amended — 2026-07-20. Owner rulings on how the project-authoring facade realizes the single-entry-point ideal
(one discoverable door, command factories for every edit, the display-interpretation boundary) are folded into
the Decision section; the verbatim question/answer record lives in the facade-gap analysis of that date
(`tmp/refac2ana.md`, untracked).

Revisit triggers: (a) a vendor firmware/WSDL revision changing the controller's service surface — re-price the 1-1
mirror; (b) a frontend that cannot reference the SDK in-process (non-.NET or remote) — that calls for a hosted
service boundary, not more tiers; (c) a second frontend re-implementing read-side model interpretation — the
query/projection gap turning into duplication; (d) an application service accreting frontend-specific types or
per-frontend behavior variants.

## Decision at a glance

All device integration, protocol handling, project-file and business logic lives in the single shared `ihcclient`
library, split into an API-service tier that mirrors the controller's SOAP services 1-1 behind SDK models and an
application-service tier of tech-agnostic, use-case-tailored business facades — one per application type, each a
uniform, consistent, easy-to-understand high-level entry point that does the hard work (business logic and
controller integration) over the deliberately more general lower-level APIs. Every application and tool above the
SDK is then a thin GUI or command-line shell that only wires presentation to a facade.

## Context

**Current state** (2026-07-19; version 0.8.1, `net10.0`; source-referenced SDK, no NuGet package):

- `ihcclient` is the repository's only shared library and the bottom of the project-reference graph; 14 first-party
  projects consume it (1 application, 6 utilities, 2 examples, 5 test suites).
- API tier: 15 service classes (`ihcclient/src/api/services/`), one per generated SOAP contract, each delegating to
  a private nested `SoapImpl` adapter; shared contract `IIHCApiService` (`serviceBase.cs:15`). The generated
  `Ihc.Soap.*` layer (~17k lines) is referenced by nothing outside `ihcclient` except white-box unit tests.
- App tier: 4 services — `AdminAppService`, `InformationAppService`, `LabAppService` (`Ihc.App`) and
  `ProjectAppService` (`Ihc.Vis`, facade of the offline project engine) — under `IIHCAppService`/`AppServiceBase`
  (`src/app/services/serviceBase.cs`), composing API-service interfaces and auto-authenticating on demand.
- Frontends are measurably thin: `ihc_admin` is argument parsing plus `AdminAppService` calls; `ihc_lab`'s
  view-model synchronizes GUI state with `LabAppService`; `ihc_openvisual` routes every mutation through the
  `Ihc.Vis.Session` command layer (each command executing via `project.Edit()`) driven from an Avalonia-free
  session wrapper — with the command vocabulary slated to become discoverable from `ProjectAppService` (see
  Decision).
- Enforcement is partial: ArchUnitNET (`tests/safe_architecture_tests/`) pins `Ihc.Vis` ↛ `Ihc.Soap`,
  SDK ↛ Avalonia, and the OpenVisual GUI's thin-shell *dependency* boundary (GUI ↛ `Ihc.Soap`, GUI ↛ `System.Xml`,
  GUI ↛ `Ihc.Vis.Io`, GUI ↛ `Ihc.Vis.Editing`, view-models ↛ Avalonia). The downward service-tier direction and the *absence of complex logic* in the frontend
  (a complexity property ArchUnitNET cannot judge) remain review conventions (`ARCHITECTURE.md` invariants 4 and 9). Two deviations are documented, both being retired: command-selection
  and legality logic that accumulated in OpenVisual's `ProjectWorkflow` and view-models (`ARCHITECTURE.md`,
  design challenge 7) is slated to move into the SDK by a planned refactoring — *display* interpretation of
  model values, by contrast, stays frontend-owned by design (see Decision) — and `ihc_project_io_extractor`'s
  standalone `.vis` parser is deprecated; the standalone `ihc_httpproxyrecorder` operates below the SDK by
  design.
- ADR-001 (UI-thread affinity) builds on this structure; the "SDK must not reference a GUI framework" rule it
  cites as a standing invariant is owned here.

**Decision forces**: the vendor ships no SDK, and the generated SOAP bindings (positional parameter names, WSDL
artifacts, churn whenever the WSDL changes) are unfit as a public surface; multiple heterogeneous frontends must
behave identically, while logic in a view-model is headlessly untestable, unreachable from console tooling, and
re-implemented by the next frontend; the test policy forbids harming a live controller, so business logic must run
against faked controller I/O; a sole maintainer cannot afford drifting copies (repo pattern priority: DRY before
KISS); and part of the domain — the `.vis` project engine — mirrors no controller service at all, so a 1-1 tier
alone cannot house it.

**Reversibility**: one-way door — the tier seam carries the whole controller-free test strategy, all consumers,
and the documentation rules; relocating logic or collapsing tiers is repo-wide restructuring.

**Assumptions**:

| Assumption | Type | Confidence | Source | Validation trigger |
| --- | --- | --- | --- | --- |
| All frontends consume the SDK in-process as .NET | business | high | repo scope — source-referenced C# library | a non-.NET or remote-frontend requirement |
| Application types stay few; app services stay tech-agnostic | technical | medium | team experience — four app services to date | an app service acquiring frontend-specific types |
| The controller's SOAP surface is effectively frozen | environmental | high | team experience — legacy vendor platform | a firmware/WSDL revision |
| The thin-frontend rule is enforceable by review and tests | operational | medium | `ARCHITECTURE.md`, design challenge 7 | the read-side gap reaching a second frontend |

**Constraints**:

| Constraint | Category | Provenance |
| --- | --- | --- |
| The controller speaks SOAP only; modern .NET has no built-in SOAP client stack | technical | given |
| Vendor file formats (`.vis`/`.def`/`.ifb`) are a closed compatibility contract | technical | given |
| Tests must be incapable of harming a live controller | organizational | chosen — standing repo policy |
| Sole-maintainer capacity | organizational | given |

## Evaluation Criteria

Priority order (highest first) — the built-in conflict is reuse/testability (favoring a dedicated logic tier)
against consumer simplicity (favoring fewer concepts); the ordering follows the repo's stated DRY-before-KISS
priority:

1. **Frontend reuse** — identical behavior available to GUI, console, and future frontends without duplication.
2. **Testability at safe seams** — business logic exercisable headlessly, fakes only at controller I/O.
3. **Vendor-churn isolation** — WSDL artifacts invisible to consumers and frontends.
4. **Consumer simplicity** — direct, unopinionated access for integrators wanting only the controller API.
5. **Solo-maintainability** — cost of carrying and evolving the structure with one maintainer.

## Options

### 1. Thick SDK, two service tiers, thin frontends (chosen)

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

### 2. Thin SDK — API mirror only; business logic in each frontend

The SDK stops at tier 1. Admin change-tracking, information aggregation, lab invocation, and project workflows are
implemented per application, in view-models or app-local helpers.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Frontend reuse | 1/5 | Every frontend re-implements; nine non-test consumers exist today |
| Testability | 2/5 | Logic lands in UI-adjacent per-app code; headless coverage needs per-app rigs — the documented read-side accumulation shows this failure mode in miniature |
| Churn isolation | 5/5 | The mirror tier still encapsulates WSDL artifacts |
| Consumer simplicity | 3/5 | Smallest SDK surface, but every consumer must build behavior before getting value |
| Solo-maintainability | 1/5 | N drifting copies of the same logic under one maintainer |
| | **Total: 12/25** | **Trade-offs**: least SDK code; total system cost grows with every frontend |

### 3. Single rich tier — use-case logic folded into the API services

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

## Decision

Keep and formalize the standing structure (option 1):

- `ihcclient` is the sole shared library and the bottom of the dependency graph. All device integration, protocol
  handling, project-file and business logic lives there — never in a frontend.
- **API-service tier**: one service class per controller web service, operations mirrored 1-1 and exposed through
  SDK-owned models and async idioms, plus a small set of cross-cutting helpers. Generated SOAP artifacts are
  consumed only through private per-service adapters (anti-corruption layer) and never appear in public
  signatures.
- **Application-service tier**: tech-agnostic backends, each targeted at a type of application (administration,
  controller information, lab exploration, project authoring). Each is a **use-case-tailored business facade** — a
  uniform, high-quality, maintainable, easy-to-understand high-level entry point that does all the hard work
  (business logic and controller integration) so its frontend is left with simple wiring, while the lower-level
  APIs it composes stay deliberately more general and directly usable by advanced consumers. `ProjectAppService`
  is the exemplar: the single, consistent door for IHC project (`.vis`) CRUD, tailored to that use case and built
  to be the backend `ihc_openvisual` — and future project-related apps — sit on. How that single door is realized
  for *authoring* (owner rulings, 2026-07-20; realized): `ProjectAppService` stays **one discoverable door** — it
  exposes the stateless `ProjectCommands` gateway (`ProjectAppService.Commands`) so command discovery starts at
  exactly one class, while `Ihc.Vis.Session` command objects remain the sole mutation vocabulary beneath it; the
  edit vocabulary is a **complete, uniform set of SDK command factories** — a frontend obtains every command from a
  factory and never constructs one directly (uniform discoverability outranks the no-trivial-delegation rule on
  this published surface; a reflection test enforces completeness and the `CompositeCommand` exclusion); and the
  facade with the SDK read surface exposes **raw values plus legality, selection and mutation**, while *display*
  interpretation of vendor values (labels, display defaults, translations) is deliberately frontend-owned
  presentation policy. They compose API-service
  interfaces and SDK engines; dependencies point strictly downward — API services never know application services.
  Cross-service and SOAP-less domain logic lives at this level or below.
- **Frontends** — applications, utilities, examples — contain presentation and wiring only; logic worth testing or
  reusing is pushed down into the SDK. Consumers choose their entry level: the API tier for direct controller
  access, the application tier for ready-made behavior; selected lower-level engine surfaces stay public for
  advanced use.

Confidence: high — this documents an implemented architecture validated by every consuming project and by the
controller-free test strategy built on the tier seam; the counterfactual options are scored against failure modes
already observable in-repo. Top unresolved uncertainty: whether read-side projection logic pushes down as cleanly
as writes — the refactoring that moves it into the SDK is planned but has not landed yet.

## Implications

### Positive

- Behavior parity across frontends by construction; a new frontend costs UI work only (long-term, cross-cutting).
- The controller-free test strategy falls out of the seam: fake the API tier, exercise real business logic
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

### Neutral

- Lower-level `Ihc.Vis` surfaces stay public — the application tier is the normal door, not the only one.
- Generated SOAP types remain technically `public` though out of contract (invariant 9's documented gap).

## Confirmation

- Architecture tests (ArchUnitNET in `safe_architecture_tests`, run by CI on all platforms): existing rules pin
  SDK ↛ Avalonia and `Ihc.Vis` ↛ `Ihc.Soap`; extend with an API-tier ↛ app-tier dependency rule.
- Code-review checklist: no `Ihc.Soap` types in public signatures; no business logic in view-models or
  `Program.Main` (vendor *display* interpretation is the sanctioned exception — frontend-owned by design);
  project-edit commands obtained only via the SDK command factories, never constructed in a frontend; test fakes
  only at `IIHCApiService`/`ICatalog` — application services always real.

## Consultation

Sole maintainer stated the decision and its two-tier structure when commissioning this record (2026-07-19).
Content grounded in `ARCHITECTURE.md`, `CLAUDE.md`, SDK doc comments, and a code inventory of all consuming
projects. Retrospective documentation of a standing decision; no other stakeholders.

2026-07-20: sole maintainer ruled the four facade-realization tradeoffs as explicit multiple-choice questions
(one discoverable door; factories for every command; display interpretation frontend-owned; add
`ExportFunctionBlock` and a `CreateNew` locality-language option now). Verbatim questions, options and rulings:
`tmp/refac2ana.md` §7 (untracked analysis record).
