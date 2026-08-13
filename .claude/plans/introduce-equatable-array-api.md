# Introduce structural sequence equality across the Ihc.Vis API

## 1. Original Prompt

> Create a plan for this change (API compatability is not an issue)

## 2. Background

Several public SDK value types store ordered data in `ImmutableArray<T>` or `IReadOnlyList<T>`. The collection objects are immutable, but their own `Equals` implementation is identity-based, so compiler-generated record equality does not compare their elements. Five product-dialog records compensate with handwritten `Equals`/`GetHashCode` pairs. That duplication already drifted when `DialogDescriptorField.ColumnSpan` was omitted, and every later scalar member—such as `ProductDialogModel.TitleSuffix`—must be added independently to the declaration, equality method, and hash method.

The SDK already centralizes sequence comparison in the internal `ImmutableArrayValue` helper, but callers must still enumerate every record member manually. The intended change is to introduce one public ordered, immutable, structurally equatable collection value and use it for equality-bearing sequence members throughout the public `Ihc.Vis` API. Compiler-generated record equality can then include every current and future stored member automatically.

API compatibility is explicitly not a constraint. The implementation should therefore prefer the clean public model over compatibility constructors, duplicate properties, or adapters whose only purpose is retaining the old signatures.

The worktree is currently dirty, including in product-dialog files that this refactor will touch. Execution must preserve those in-progress changes, especially the new `DialogFieldModel.HidesUnresolvedResourceKey` behavior, and must not reset, restore, or overwrite unrelated user work.

## 3. Analysis

### Current findings

- `ihcclient/src/vis/model/ImmutableArrayValue.cs` implements ordered content equality and hashing, including the established rule that `default` and empty arrays mean the same logical value.
- Handwritten sequence-aware equality currently exists in:
  - `ProjectElement`;
  - `GrammarAttr`, `GrammarDeclaration`, and `CatalogGrammar`;
  - `ProjectValidationResult`;
  - `ProductDialogModel`, `DialogGroupModel`, `ProductDialogDescriptor`, `DialogDescriptorGroup`, and `DialogDescriptorField`.
- Public records with stored sequence members and compiler-generated reference equality currently include:
  - `DataTableView`, `EnumTypeView`, `DataTablesModel`, `DatalineModuleMap`, and `ModuleAddressMap` in `ProjectProjections.cs`;
  - `CompositeCommand.Parts`;
  - `ApplyProductDialog.Edits`;
  - `AttrInfo.AllowedValues`;
  - `AddStandaloneEnumType.States`;
  - `UpdateEnumStates.Added` and its init-only `Relabels` member.
- `SessionQueryTests` explicitly works around the current `DataTableView.Rows` identity equality by projecting scalars. This is evidence that the issue affects API behavior outside the product-dialog types.
- Other collection-valued records use different mathematical semantics:
  - `DefinitionDocumentation.Resources` is a map and requires order-independent key/value equality;
  - `FunctionBlockDefinition.ExplicitCloseIds` and `ProjectChangeSet` members are sets and require order-independent equality;
  - `Project` deliberately excludes `InlineDtdBlocks` from logical equality.
  These must not be forced through an ordered-array abstraction.

### Scope

Included:

1. Add one public `EquatableArray<T>` value type for ordered immutable sequences in `Ihc.Vis.Model`.
2. Migrate stored ordered-sequence members on public `Ihc.Vis` value records/classes to that type.
3. Convert the three validated grammar value classes (`GrammarAttr`, `GrammarDeclaration`, `CatalogGrammar`) to sealed record classes so their scalar and `EquatableArray<T>` fields receive compiler-generated equality while retaining their private constructors and validated factories.
4. Delete sequence-specific handwritten equality/hash implementations made redundant by the wrapper.
5. Correct public records that currently have accidental reference equality for immutable ordered collections.
6. Add a build-time regression test that detects raw ordered collection backing fields on public `Ihc.Vis` records.
7. Update downstream SDK, OpenVisual, and test call sites.

Excluded:

- Arrays and immutable collections used as local algorithm storage, builders, parser results, static lookup tables, or ordinary method return values that do not participate in a value object's equality.
- Internal reporting shape records unless a concrete equality use is found during compilation; changing unused internal equality is not required for the public API goal.
- Set and map equality. Existing specialized implementations remain until a separately justified `EquatableSet<T>` or `EquatableDictionary<TKey,TValue>` change is planned.
- Mutable byte/file payload APIs and legacy reflection metadata outside `Ihc.Vis`, such as `LabAppService.ResultFileContent.Bytes` and `FieldMetaData.SubTypes`. Replacing those would change ownership and mutability semantics, not merely equality.
- `Project`'s custom equality, which intentionally omits serialization provenance.
- Product specification and user-story documents: this is implementation architecture (HOW), not product behavior (WHAT).

### `EquatableArray<T>` design

Create `ihcclient/src/vis/model/EquatableArray.cs` with these properties:

- `public readonly struct EquatableArray<T>` implementing `IReadOnlyList<T>` and `IEquatable<EquatableArray<T>>`.
- Backed by `ImmutableArray<T>`; copying the wrapper does not copy elements.
- Ordered equality using `EqualityComparer<T>.Default`.
- Ordered content hashing using the same comparer and element order.
- `default(EquatableArray<T>)` behaves exactly like `Empty`; do not expose representation-level `IsDefault` state that would distinguish equal logical values.
- A collection builder so C# collection expressions such as `[]` and `[a, b]` work directly.
- A small read surface sufficient for API callers and existing code: `Empty`, `Count`, `Length`, `IsEmpty`, indexer, allocation-free pattern enumeration, `Contains`, and `IndexOf`.
- An implicit, allocation-free conversion from `ImmutableArray<T>` to `EquatableArray<T>` for SDK builders/parsers that already materialize immutable arrays.
- An explicit `AsImmutableArray()` escape hatch that returns a normalized empty `ImmutableArray<T>` without allocation. Do not add an implicit conversion in the other direction; keeping that boundary visible avoids overload ambiguity and hidden dependence on the concrete backing type.
- Factories from `IEnumerable<T>`/`ReadOnlySpan<T>` must snapshot into immutable storage. No constructor may retain a mutable array or list.
- Do not mirror all of `ImmutableArray<T>`. Where mutation-style immutable operations are genuinely needed, call `AsImmutableArray()` explicitly or add a narrowly justified wrapper operation with tests.

### Migration decisions

1. **Core tree and grammar first.** Migrate `ProjectElement.Attrs`/`Children`, `GrammarAttr.EnumTokens`, `GrammarDeclaration.Attrs`, and `CatalogGrammar.Declarations`. These establish nested structural equality and exercise the highest-volume tree traversal path.
2. **Validation next.** Migrate `ProjectValidationResult.Errors`/`Findings`; retain `Warnings` as a computed view, returning `EquatableArray<string>` only if doing so avoids an otherwise redundant conversion at its call sites.
3. **Product dialogs.** Migrate all six stored arrays (`Groups`, `Parts`, descriptor `Groups`, `Fields`, `Widgets`, and `Suggestions`) and remove all five handwritten pairs. `SuggestionsOrEmpty` becomes redundant because the wrapper normalizes default; delete it and read `Suggestions` directly unless a real semantic distinction appears.
4. **Read projections.** Migrate the collection-bearing records in `ProjectProjections.cs`, then replace scalar-projection equality workarounds with direct record equality assertions.
5. **Commands and attribute metadata.** Migrate `CompositeCommand.Parts`, `ApplyProductDialog.Edits`, `AttrInfo.AllowedValues`, `AddStandaloneEnumType.States`, `UpdateEnumStates.Added`, and `UpdateEnumStates.Relabels`. Construction must snapshot caller-owned mutable lists so commands remain immutable after creation.
6. **Remove the old helper only after the final reference disappears.** `ProjectChangeSet.SelfAndIdlessEqual` should compare the new value arrays directly or use sequence comparison appropriate to its partial-tree algorithm; it must not retain `ImmutableArrayValue` merely as a compatibility bridge.
7. **Keep deliberate custom equality visible.** Add concise comments/tests around `Project`, `DefinitionDocumentation`, and `FunctionBlockDefinition` so a later cleanup does not mistake their domain-specific exclusions or set/map semantics for leftover duplication.

### Regression guard

Add an SDK architecture/equality policy test that:

- discovers public record types under the `Ihc.Vis` namespace root;
- inspects declared instance backing fields, including private compiler-generated fields;
- fails when a stored ordered sequence uses `ImmutableArray<>`, an array, or `IReadOnlyList<>` instead of `EquatableArray<>`;
- ignores computed collection properties because they have no backing field;
- does not flag sets, maps, static lookup collections, method parameters, or method-local arrays;
- contains a seeded positive control proving the detector fails on a test-only record with a raw collection field.

This turns the convention into compiler/test feedback for future members and records instead of relying on review memory.

### Implementation sequence and completion criteria

#### Step 1 — Establish baseline and protect the dirty worktree

- Record `git status --short` and `git diff --stat` in `tmp/equatable-array-refactor/baseline.md`.
- Copy every already-modified file that later phases will touch into a path-preserving `tmp/equatable-array-refactor/backup/` tree and record hashes in `backup-manifest.md`.
- Run the current focused equality tests and the explicit Release performance benchmark; record concise results in `verification.md`.
- Completion: baseline files exist and are non-empty; every dirty overlapping file has a verified backup; baseline tests/benchmark have a recorded outcome.

#### Step 2 — Implement and verify `EquatableArray<T>`

- Add the wrapper and collection builder.
- Add focused tests for default/empty equivalence, order sensitivity, nested elements, equal hash codes, collection expressions, indexing/enumeration, conversions, and immutable snapshot behavior.
- Verify no boxing/allocation is introduced in direct `foreach` enumeration by using a concrete enumerator; do not add a brittle absolute allocation threshold unless a stable existing harness supports it.
- Completion: focused wrapper tests pass and XML documentation describes equality, hashing, default normalization, and conversion behavior.

#### Step 3 — Migrate core model, grammar, and validation types

- Migrate `ProjectElement`, the three grammar types, and `ProjectValidationResult`.
- Convert grammar classes to sealed record classes without opening public construction or writable properties.
- Remove their now-redundant equality/hash implementations.
- Update builders, readers, writers, canonicalization, and validation call sites using collection expressions or `AsImmutableArray()` at concrete-array boundaries.
- Completion: existing project-model equality tests pass; catalog grammar round-trip tests pass; equal independently built trees/grammars/results hash equally; factories still reject invalid grammar states.

#### Step 4 — Migrate product-dialog models and descriptors

- Migrate all six dialog sequence members.
- Remove the five handwritten equality/hash pairs and obsolete default-normalization accessors/comments.
- Preserve all current dirty-worktree behavior, including `ColumnSpan`, `TitleSuffix`, `PresenceTag`, `ColumnMajor`, and `HidesUnresolvedResourceKey`.
- Add or strengthen equality tests demonstrating that each stored scalar/init member affects equality while independently built equal nested collections compare equal.
- Completion: dialog equality contains no member list; product dialog composer/preset/apply tests pass; OpenVisual dialog view-models compile against the new types.

#### Step 5 — Migrate the remaining public ordered-sequence value records

- Migrate read projections, command records, and `AttrInfo` listed above.
- Snapshot `IReadOnlyList<T>` command inputs at the gateway boundary; no command may retain caller-owned mutable list state.
- Replace test workarounds such as scalar projection in `SessionQueryTests` with direct record equality where that expresses the intended contract.
- Completion: two independently materialized read models and commands with equal contents compare and hash equal; changing order or one element makes them unequal; mutating an original input list after command construction does not change the command.

#### Step 6 — Remove legacy equality plumbing and add the guard

- Remove remaining sequence-only manual equality/hash implementations and delete `ImmutableArrayValue.cs` once `rg` confirms no references.
- Add the public-record backing-field policy test with its positive control.
- Confirm the deliberate project/set/map custom equality implementations remain and still pass their specialized tests.
- Completion: no production reference to `ImmutableArrayValue`; the guard passes on production types and demonstrably catches its seeded violator.

#### Step 7 — Downstream migration and documentation

- Update OpenVisual and any affected tests/utilities to consume `EquatableArray<T>` through its read interface or explicit backing-array escape hatch.
- Add a short `ARCHITECTURE.md` note under the immutable-model design challenge stating that stored ordered collections on public value records use `EquatableArray<T>` so record equality remains structural and future fields are compiler-covered.
- Do not edit `applications/ihc_openvisual/docs/product.md` or stories.
- Completion: solution builds with warnings as errors; no unnecessary adapter methods or compatibility-only overloads remain.

#### Step 8 — Full verification and audit

- Run the controller-free suites listed in the Verification Plan.
- Re-run the Release performance benchmark and compare its median/p95 results with the recorded baseline, treating large repeatable regressions in tree-heavy paths as a blocker.
- Review the final diff specifically for handwritten member lists, accidental set/map conversion, mutable collection retention, unrelated formatting churn, and loss of dirty-worktree changes.
- Completion: all required tests/builds pass; benchmark is not materially regressed after a repeat run; audit findings are recorded and resolved.

## 4. Source/Rationale

- The initiating review found that `DialogDescriptorField.Equals` omitted `ColumnSpan` and that adding `ProductDialogModel.TitleSuffix` required another manual equality/hash edit.
- `ProductDialogDescriptor.cs` and `ProductDialogModel.cs` contain five handwritten equality/hash pairs solely because `ImmutableArray<T>` compares by backing-array identity.
- `ImmutableArrayValue.cs` documents the existing logical contract: ordered comparison and `default == empty`.
- `ProjectModelEqualityTests` already asserts nested structural equality and default/empty equivalence.
- `SessionQueryTests` documents a current equality workaround for `DataTableView.Rows`.
- Repository design priorities favor DRY, KISS, immutable modern C#/.NET 10 value types, and compiler-enforced invariants.
- API compatibility was explicitly declared out of scope by the user.

No external research or product-behavior change is required.

## 5. Impact Areas

| Area | Expected files/components | Risk | Effect |
| --- | --- | --- | --- |
| Shared value primitive | New `ihcclient/src/vis/model/EquatableArray.cs` | Medium | Establishes equality/hash/default/enumeration semantics used throughout the public value model. |
| Core project tree | `ProjectElement.cs` and tree reader/writer/editor call sites | High | Hot and pervasive path; type substitutions ripple broadly and may affect traversal performance. |
| Catalog grammar | `GrammarAttr.cs`, `GrammarDeclaration.cs`, `CatalogGrammar.cs`, builders/parsers/writers | High | Equality simplification must preserve validated construction and catalog fidelity. |
| Validation | `ProjectValidationResult.cs`, validator consumers | Medium | Public result shape changes; warnings/errors/findings behavior must remain identical. |
| Product dialogs | `ProductDialogModel.cs`, `ProductDialogDescriptor.cs`, composer, presets, GUI view-model | High | Removes the original drift mechanism while overlapping current user changes. |
| Read projections | `ProjectProjections.cs`, session query tests/callers | Medium | Fixes currently accidental reference equality. |
| Session commands | `CompositeCommand.cs`, `ProductDialogCommands.cs`, `MetadataCommands.cs`, gateways | Medium | Must snapshot mutable inputs and preserve command behavior/history. |
| Attribute metadata | `AttrInfo.cs`, property-grid consumers | Low | Read-only collection surface changes without changing attribute behavior. |
| Policy tests | `safe_project_tests` and/or `safe_architecture_tests` | Medium | Enforces future use and needs a positive control to avoid a vacuous test. |
| Downstream GUI | `applications/ihc_openvisual`, `safe_visual_tests` | Medium | Compile-time type changes; no intended UI behavior change. |
| Architecture documentation | `ARCHITECTURE.md` | Low | Records the cross-cutting value-collection convention; no product docs change. |

## 6. Potential Negative Effects

| Risk | Detection | Mitigation |
| --- | --- | --- |
| Wrapper equality and hash disagree | Equal-value/hash tests, default/empty tests | Implement both over the same normalized ordered enumeration and comparer. |
| Default wrapper throws during ordinary reads | Default-instance tests for every read member | Normalize through one private backing-array accessor used by `Count`, indexer, and enumeration. |
| Tree traversal slows due to interface boxing or repeated conversions | Before/after Release benchmark; inspect hot `foreach` paths | Provide a concrete enumerator and allocation-free `AsImmutableArray()`; avoid LINQ at newly hot boundaries. |
| Wrapper grows into a duplicate `ImmutableArray<T>` API | API review and final diff audit | Keep a minimal read surface; use explicit backing-array access for uncommon transforms. |
| Mutable lists remain captured by commands | Mutation-after-construction tests | Snapshot all `IReadOnlyList<T>` inputs when creating `EquatableArray<T>`. |
| Grammar factories can be bypassed after class-to-record conversion | Compile/API review and invalid-construction tests | Keep constructors private and properties get-only; do not introduce public init setters. |
| Deliberate map/set/project equality gets unintentionally rewritten | Specialized existing equality tests | Maintain an explicit exclusion list and do not apply an ordered wrapper to unordered or excluded state. |
| `default` versus empty becomes observably different despite equality | API inspection and default tests | Do not expose `IsDefault`; return normalized empty backing storage. |
| Hash codes change | Test only equal-hash contract, never exact hash values | Treat hash codes as process-local implementation details; do not persist them. |
| Current user edits are lost or interleaved incorrectly | Baseline diff, backups, final diff audit | Back up overlapping dirty files, patch narrowly, and never run destructive git operations. |
| Architecture guard is vacuous or over-broad | Seeded violating record; allowlisted computed/non-sequence cases | Inspect backing fields, restrict to public `Ihc.Vis` records, and prove both fail/pass behavior. |

## 7. Verification Plan

### Focused behavioral checks

- `EquatableArray<T>`:
  - default equals empty and hashes equally;
  - independently allocated equal sequences compare equal;
  - order and element differences compare unequal;
  - nested record sequences recurse through value equality;
  - collection expressions and immutable-array input work;
  - caller-owned mutable input is copied;
  - `foreach`, indexer, `Count`/`Length`, `Contains`, and `IndexOf` work on empty/default/non-empty values.
- Migrated records:
  - every existing equality test remains green;
  - `ColumnSpan`, `TitleSuffix`, `ColumnMajor`, `PresenceTag`, and all other stored init properties change equality automatically;
  - projection and command records compare by nested content;
  - command input mutation cannot change a constructed command;
  - equal records always produce equal hashes.
- Guard:
  - production public `Ihc.Vis` records pass;
  - a test-only raw-sequence record is detected;
  - computed collection views and map/set members do not produce false positives.

### Static audits

```powershell
rg -n "ImmutableArrayValue" ihcclient tests applications
rg -n "public bool Equals\(|override int GetHashCode\(" ihcclient/src/vis
rg -n "ImmutableArray<|IReadOnlyList<|\[\]" ihcclient/src/vis
git status --short
git diff --stat
```

Review every remaining result against the explicit scope/exclusion list. The goal is not zero immutable arrays in the SDK; it is zero raw ordered-sequence backing fields in the targeted public value records and zero redundant handwritten member lists.

### Build and controller-free test gates

```powershell
dotnet build IHCClientSDK.sln
dotnet test tests/safe_project_tests/safe_project_tests.csproj
dotnet test tests/safe_unit_tests/safe_unit_tests.csproj
dotnet test tests/safe_architecture_tests/safe_architecture_tests.csproj
dotnet test tests/safe_visual_tests/safe_visual_tests.csproj
dotnet test tests/safe_lab_tests/safe_lab_tests.csproj
```

Do not run `safe_integration_tests`; this refactor does not require a controller.

### Performance gate

Run before Step 2 and after Step 7:

```powershell
dotnet test tests/safe_project_tests/safe_project_tests.csproj -c Release --filter "FullyQualifiedName~PerfBaselineBenchmark" -l "console;verbosity=detailed"
```

Record median and p95 values in `tmp/equatable-array-refactor/verification.md`. If a tree-heavy metric regresses materially, repeat once to rule out noise. A repeatable regression attributable to wrapper enumeration/conversion blocks completion until resolved or explicitly accepted by the user.

## 8. Rollback Strategy

1. Before implementation, create `tmp/equatable-array-refactor/backup/` with path-preserving copies of every touched file and a hash manifest. Verify all copies exist and are non-empty where the source is non-empty.
2. Roll back by phase, not by broad repository operation:
   - restore the affected phase's files from the backup with explicit `Copy-Item -LiteralPath` operations;
   - remove only newly created, explicitly named files from that phase after verifying their resolved paths remain under this repository;
   - re-run the focused tests for the last known-good phase.
3. Never use `git reset`, `git checkout`, `git restore`, or a broad recursive delete. Existing changes belong to the user.
4. If the user prefers version-control rollback, stop and ask them to perform it; the executing agent should provide the exact touched-file list and current diff summary.
5. Keep this plan and `verification.md` after rollback so the failure and last good checkpoint remain available for a later attempt.

## 9. State Files

| File | Purpose | Updated when |
| --- | --- | --- |
| `.claude/plans/introduce-equatable-array-api.md` | Authoritative scope, decisions, inline step status, and resumption point | Before starting and after completing/blocking every step |
| `tmp/equatable-array-refactor/baseline.md` | Initial dirty-worktree summary, affected-file inventory, and baseline environment | Step 1 only, amended only if initial capture was incomplete |
| `tmp/equatable-array-refactor/backup-manifest.md` | Source/backup path pairs and hashes for recoverable rollback | When a new touched file is backed up before editing |
| `tmp/equatable-array-refactor/verification.md` | Concise command, result, test counts, failures, and benchmark median/p95 deltas | After each focused or full verification run |
| `tmp/equatable-array-refactor/backup/` | Path-preserving file copies; not a progress log | Before first edit to each file |

Progress must remain inline in this plan; do not create a separate `progress.txt`.

## 10. Progress Tracking

| Step | Description | Dependencies | Completion criterion | Status |
| --- | --- | --- | --- | --- |
| 1 | Capture baseline, back up overlapping dirty files, run baseline equality/performance checks | None | State files verified; dirty overlaps backed up; baseline recorded | pending |
| 2 | Implement and unit-test `EquatableArray<T>` | Step 1 | Wrapper contract tests pass | pending |
| 3 | Migrate core tree, grammar, and validation types | Step 2 | Focused model/grammar/validation tests pass | pending |
| 4 | Migrate product-dialog models/descriptors | Step 3 | No dialog equality member lists; dialog engine and GUI compilation pass | pending |
| 5 | Migrate remaining public sequence-bearing read models, commands, and `AttrInfo` | Step 3 | Structural equality and snapshot tests pass | pending |
| 6 | Delete legacy helper and add the public-record policy guard | Steps 4-5 | No helper references; armed guard passes | pending |
| 7 | Complete downstream migration and architecture note | Step 6 | Solution builds with warnings as errors; docs accurately state convention | pending |
| 8 | Run full controller-free verification, performance comparison, and final diff audit | Step 7 | All gates pass; no unresolved audit or benchmark regression | pending |

Status values are `pending`, `in-progress (<concise note>)`, `done (<verification summary>)`, or `blocked (<specific cause>)`. Update a row before work starts and immediately after its completion or blockage.

## 11. Context Budget Rules

| Rule | Threshold | Action |
| --- | --- | --- |
| Maximum implementation steps per session | 2 substantial steps | End at a verified step boundary; update this plan and `verification.md` first. |
| Maximum turns before quality check | 12; hard stop at 15 | Re-read scope/exclusions and current progress row; start a fresh session at 15. |
| Context utilization warning | 70% | Save concise results, update plan status, and prepare a session handoff. |
| Context utilization hard stop | 85% | Stop edits, persist state, verify written files, and end the session. |
| Build/test output retained in context | Failure summary plus final counts only | Store concise outcomes in `verification.md`; do not retain successful per-test logs. |
| Large diffs | Never load the entire repository diff | Use `git diff --stat`, then inspect only current-step files or targeted hunks. |
| Generated/catalog source | Never read full generated catalogs | Use targeted searches and compile errors; generated catalog contents are not part of this refactor. |
| Test suite failures | First relevant stack trace and failing test names | Save full output externally only if needed; keep actionable excerpts in context. |
| Prior-phase implementation details | Decision and verification summary only | Re-read source only when the current step modifies it. |

## 12. Session Startup Protocol

Each execution session begins with this five-step orientation, limited to five tool calls and three minutes:

1. Read this plan's Progress Tracking table and find the first row whose status is not `done`; read that step's detailed subsection and completion criterion.
2. Read only the tail/relevant headings of `baseline.md`, `backup-manifest.md`, and `verification.md` for the current phase. Do not reload old successful test logs.
3. Run `git status --short` and `git diff --stat`; compare them with the baseline and last completed-step annotation.
4. Verify the expected current-step artifacts exist (`EquatableArray.cs`, relevant backups, or test files as applicable) and that state files are non-empty.
5. Announce: `Resuming step N: <description>. Last completed: <step/result>. Next verification: <command>.`

If plan status and filesystem state disagree, stop, record the inconsistency as `blocked`, and ask the user rather than guessing. Exceeding the startup budget indicates missing/corrupt state or an overly broad read; narrow to the current step.

## 13. Observation Masking

After 5-10 turns, mask older tool output while preserving decisions and actionable evidence.

Preserve:

- the design contract for `EquatableArray<T>`;
- the inclusion/exclusion list;
- the most recent three tool outputs;
- all active compile errors, failed assertions, stack traces, and exact file paths;
- the dirty-worktree baseline and any overlap warnings;
- current benchmark baseline/delta values.

Summarize:

- broad `rg` inventories as counts plus the remaining relevant file list;
- successful builds/tests as command, pass count, duration, and status;
- completed diffs as files changed plus semantic outcome;
- old migration errors after their fixes are verified.

Never retain in main context:

- full generated catalog files;
- full successful test logs;
- full solution diffs spanning completed phases;
- binary `.vis`, `.def`, `.ifb`, report-oracle, or benchmark temporary contents.

Never summarize away an unresolved error, a path needed for rollback, or evidence that a current dirty change predates this refactor.

## 14. Quality Degradation Detection

Generic warning signs:

- repetitive suggestions or repeated searches already recorded as complete;
- forgetting earlier decisions or contradicting the scope;
- inventing APIs or file names instead of checking the current source;
- mixing up current and baseline behavior;
- more than 15 turns in one session.

Task-specific warning signs:

- treating every `ImmutableArray<T>` in the SDK as a migration target;
- applying ordered equality to sets or dictionaries;
- reintroducing handwritten equality member lists after adding the wrapper;
- exposing `IsDefault` despite the `default == empty` contract;
- adding a broad implicit wrapper-to-`ImmutableArray<T>` conversion without revisiting overload ambiguity;
- forgetting `UpdateEnumStates.Relabels`, body-declared init properties, or nested collection members;
- changing `Project` equality to include `InlineDtdBlocks`;
- overwriting `HidesUnresolvedResourceKey` or another pre-existing dirty change;
- claiming the guard works without running its seeded positive control;
- reading enormous generated files or full successful logs into context.

Recovery protocol:

1. Stop editing and summarize completed work, unresolved errors, and the exact next action.
2. Update this plan, `backup-manifest.md`, and `verification.md`; independently verify each write.
3. Ask the user to make a version-control checkpoint if desired; the executing agent must not mutate git state.
4. Start a new session using only this plan, the state-file summaries, and the current step's files.
5. If confusion remains, narrow work to one type family and one focused test command before continuing.

## 15. Phase Separation and Session Breaks

| Phase | Steps | Context boundary | Carry forward | Mandatory break trigger |
| --- | --- | --- | --- | --- |
| Foundation | 1-2 | Baseline plus wrapper contract only | Wrapper API, test results, backup manifest | After Step 2 if more than 12 turns were used |
| Core models | 3 | Tree/grammar/validation files and focused failures only | Migrated API decisions and performance notes | After core model tests pass |
| Public consumers | 4-5 | One family at a time: dialogs, then projections/commands | Compile fixes and equality outcomes | Between Steps 4 and 5 if context exceeds 70% |
| Enforcement | 6-7 | Remaining references, guard, downstream compile, architecture note | Guard result and solution build result | Before full verification if any unresolved warning exists |
| Final verification | 8 | Commands and concise results only | Final pass/fail summary and benchmark delta | End immediately after plan is marked done or blocked |

At every break: update the Progress Tracking row, append concise verification results, verify state files exist and are non-empty, run `git status --short`, and state the next resumption point. Do not begin another phase at or above 70% context utilization.

## 16. Per-Step Workflow Protocol

Before each step:

1. Mark the step `in-progress` in this plan and verify the plan file persists.
2. Run `git status --short` and compare with `baseline.md`.
3. Back up any newly touched file and update/verify `backup-manifest.md` before editing.
4. Read only the files and tests needed for that step.

During each step:

5. Apply narrow patches; avoid formatting unrelated code.
6. Run the smallest relevant test/build after each coherent type-family migration.
7. On failure, save only the failing test names and actionable error excerpts to `verification.md`.

After each step:

8. Verify every created/edited artifact exists and is non-empty where expected.
9. Run `git diff --stat` and inspect targeted hunks for the current step.
10. Append the exact verification command and concise result to `verification.md`; verify its line count increased.
11. Mark the step `done (<result>)` or `blocked (<cause>)` in this plan and verify the write.
12. Do not proceed if expected artifacts are absent, current dirty work was lost, or the focused gate is red.

No sub-agent or parallel task delegation is planned; shared-file overlap would add risk to this cross-cutting refactor.

This plan was created by plan-resumable-creator on 2026-08-13.
