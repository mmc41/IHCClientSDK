# ADR-004: Compile-time symbol bans carry the rules they can express exactly; architecture tests carry the rest

## Status

Decided — 2026-08-17. Refines ADR-002, which recorded that enforcement of the layering rules is partial,
and ADR-003, whose confirmation names a compile-time namespace ban alongside the architecture tests. Neither
is superseded.

Revisit triggers: (a) a migrated ban acquires a local waiver, since the rules moved here were moved on the
premise that they carry none; (b) the analyzer stops supporting namespace-level entries, which is what makes
a migrated ban cover symbols added later; (c) a second GUI or SDK assembly joins the repository, changing
what "whole-project" scope means for both mechanisms.

## Decision at a glance

Each architecture rule is enforced in exactly one place. A rule moves to a compile-time symbol ban when the
ban expresses it exactly — a namespace or type entry, covering the whole project and any member added later.
Every other rule stays an ArchUnitNET fitness test, because a symbol ban cannot express it at all.

## Context

**Current state** (2026-08-17; 18 projects, `net10.0`; both mechanisms present):

- The architecture suite holds 80 rules over two IL models (the SDK and the GUI assembly), run on all three
  desktop platforms in CI. It reads IL, so it sees dependency direction, type hierarchy, member-signature
  closures, construction sites, field retention and markup-authored constructions.
- **33 of those 80 rules are rules about the rules** — seeded-violator positive controls and vacuity guards.
  They exist because a scan that stops matching anything keeps passing.
- A compile-time ban mechanism is in place repo-wide: one banned-symbol file applied to every project that
  does not opt out through a property it sets itself.
- CI runs one sequential job per platform; the architecture verdict is the seventh of ten steps, behind
  restore, a whole-solution build and the unit-test leg. A compile diagnostic surfaces at the build step,
  and in the editor before that.
- Observed on repeated cold builds: adding the ban analyzer to a project changes build wall-clock by less
  than measurement noise, while an architecture verdict over the same code costs a second project build plus
  a test-host run on top of the build being compared.

**Decision forces**: two mechanisms now cover overlapping ground. Without a rule for choosing between them,
both grow, rules get enforced twice, and it stops being clear which one is authoritative.

**Reversibility**: two-way door. A ban entry and a fitness test are each a few lines, and a rule can move
back by restoring the test. No published artefact depends on the choice.

**Assumptions**:

| Assumption | Type | Confidence | Source | Validation trigger |
| --- | --- | --- | --- | --- |
| Contributors read a compile error at the offending line sooner than a test failure in a report | operational | high | industry norm; the repo already invests in inner-loop feedback | a violation reaching CI despite a local build |
| The migrated rules genuinely need no exemption | technical | medium | each is a layering ban with no recorded exception | the first request for a waiver |
| Namespace-level ban entries keep covering symbols added later | technical | high | the mechanism matches on the namespace, not a member roster | an analyzer release that narrows matching |
| The suite's remaining rules stay beyond a symbol ban's reach | technical | high | classification of every rule against the ban's expressiveness | a rule simplifying into a plain reference ban |

**Constraints**:

| Constraint | Category | Provenance |
| --- | --- | --- |
| A symbol ban applies to a whole project; it cannot hold for only some types within one | technical | given |
| A source analyzer sees C# only; markup-authored constructions are invisible to it | technical | given |
| The SDK uses context-capture pervasively and deliberately, so any such ban must be project-scoped | technical | given |
| Sole-maintainer capacity | organizational | given |

## Evaluation Criteria

Priority order (highest first). The first is decisive: a mechanism that cannot express a rule is not an
option for that rule, whatever it scores elsewhere.

1. **Rule fidelity** — expresses the rule exactly, including code the rule must reach and members added later.
2. **Failure mode** — whether enforcement can stop working while still reporting success.
3. **Feedback locality and latency** — where the violation appears and how much work precedes the verdict.
4. **Exemption discipline** — whether a violation can be waived locally, and how visible the waiver is.
5. **Maintenance cost** — what must be kept in sync as the code evolves.

## Options

### 1. Symbol ban where the ban expresses the rule exactly; fitness test everywhere else (chosen)

Each rule sits in one mechanism. A rule qualifies for a ban when it is a whole-project prohibition on a
namespace or a type — complete by construction, so members added later are covered without editing anything.
Rules needing a roster derived from our own evolving API, an exemption for named types or members, or
visibility into markup stay fitness tests. A migrated ban keeps one anchored pin that its target symbols
still resolve, so a rename cannot quietly empty it.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Rule fidelity | 5/5 | Each rule is placed in the mechanism that states it exactly; nothing is approximated to fit |
| Failure mode | 4/5 | A ban entry cannot pass vacuously the way a scan can, and the resolve-pin closes the rename hole; the rules that stay tests still need their detector twins |
| Feedback locality and latency | 4/5 | Migrated rules report at the offending line during a build already being run; the majority that stay tests are unchanged |
| Exemption discipline | 3/5 | A migrated ban can be waived locally by a suppression, where a fitness test cannot be waived at all |
| Maintenance cost | 4/5 | Each rule is maintained once, and a migrated rule also retires its armed-detector twin; the cost is that contributors must know two mechanisms exist |
| | **Total: 20/25** | **Trade-offs**: trades absolute unwaivability for locality on the rules that can afford it; adds a second mechanism to learn |

### 2. Keep every rule as an architecture test

The status quo. One mechanism, one place to look, and its IL reach covers every rule the repository has
expressed — including the ones no analyzer can see. Its cost is structural: every scan needs a companion
rule proving it can still fail, and the verdict arrives only after a separate build and a test host.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Rule fidelity | 5/5 | IL inspection expresses every rule in the suite, markup-authored constructions included |
| Failure mode | 2/5 | A scan that matches nothing keeps passing, which is why two fifths of the suite is rules about the rules |
| Feedback locality and latency | 2/5 | The verdict needs a second build and a test host, and lands in a test report rather than on the offending line |
| Exemption discipline | 5/5 | No local waiver exists; a rule holds or the suite fails |
| Maintenance cost | 2/5 | Every rule carries the standing obligation of a detector that proves it still detects |
| | **Total: 16/25** | **Trade-offs**: maximum reach and unwaivability, paid for in verdict latency and in rules that exist only to guard other rules |

### 3. Both mechanisms for every expressible rule

Add the bans for local feedback and keep the corresponding tests as a backstop. The fastest signal available,
and the test's anchor keeps guarding the rename case. Every such rule then exists in two places that nothing
compares, which is the shape the repository's build-policy files were centralised to eliminate.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Rule fidelity | 5/5 | The test carries the rule; the ban adds an earlier partial signal |
| Failure mode | 4/5 | The test's anchors still break on a rename, and the ban cannot go vacuous |
| Feedback locality and latency | 5/5 | Violations surface at the edit and are still checked in full afterwards |
| Exemption discipline | 5/5 | A local suppression silences the ban but not the test |
| Maintenance cost | 1/5 | Each rule is stated twice, in two syntaxes, with no mechanism comparing them, and neither is clearly authoritative |
| | **Total: 20/25** | **Trade-offs**: buys the best feedback and keeps unwaivability, at the price of duplicated rules that drift apart silently |

## Decision

Adopt option 1: one rule, one mechanism.

- The qualifying test is fidelity, not convenience. A rule migrates only when a namespace or type entry
  states it completely and for the whole project, so that symbols added later are covered without anyone
  remembering to add them. A rule that would need a hand-maintained roster of our own evolving API is not a
  candidate: the roster silently ages, and a rule that quietly stops covering new cases is worse than one
  that reports late.
- Rules whose value depends on being unwaivable stay fitness tests. A compile-time ban can be suppressed at
  the call site, which is acceptable for a layering prohibition — the waiver is visible where it is taken —
  but changes the meaning of a ban documented as admitting no exception.
- Option 3 scores as well on the criteria and is rejected on the one where it does not: a rule stated twice
  in two syntaxes, with nothing comparing the copies, drifts. This repository already centralised its build
  policy and package versions for that reason, and the same argument applies to a rule.
- The migrated set is therefore small — the whole-project layering prohibitions — and the suite keeps the
  large majority of its rules, including every one that reasons about hierarchy, signatures, retention,
  construction sites or markup.

Confidence: high — the split follows from what each mechanism can express, which was established by
classifying every existing rule rather than by preference. Top unresolved uncertainty: whether the migrated
bans attract local suppressions in practice; the first one is the signal that a rule was misplaced.

## Implications

### Positive

- A violation of a migrated rule appears at the offending line during a build already being run, rather than
  in a later test report.
- Each migrated rule retires its armed-detector twin, so the mass of rules that exist only to prove other
  rules still work shrinks with the migrated set.
- A ban entry naming a symbol nobody uses is inert; it cannot report success while having stopped matching.

### Negative

- Migrated rules become locally waivable by a suppression comment, where a fitness test admits no waiver at
  all — a real weakening, accepted only for prohibitions that carry no documented exception.
- A ban entry is a string, so renaming a banned namespace empties the ban. The anchored resolve-pin is what
  keeps that from being silent, and it is an obligation the fitness tests carried implicitly.
- Two mechanisms now exist for one concern, so a contributor adding a rule must first decide which applies,
  and a reader looking for "what enforces this" has two places to look.

### Neutral

- The rules that stay tests are unchanged in scope, reach and cost.
- Both mechanisms already fail the build, so neither changes whether a violation is advisory or fatal.

## Confirmation

- The migrated prohibitions are enforced by the build itself: a violation is a compile error under the
  repository's warnings-as-errors policy.
- An anchored pin asserts the banned targets still resolve, so a rename cannot empty a ban unnoticed.
- Code-review checklist: a new rule is added to one mechanism, and the choice follows the fidelity test.

## Consultation

Ruled by the repository owner and sole maintainer on 2026-08-17, on a classification of all 80 existing
rules against what a symbol ban can express, and on observed build and test wall-clock for both mechanisms.
The owner settled the migrated scope and ruled against keeping duplicate enforcement. No external
stakeholders were consulted — the project is single-maintainer.
