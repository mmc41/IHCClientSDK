# OpenVisual host problem appendix (`app.openvisual.*`)

This app owns a **reserved code family** and nothing else. The `.vis` project findings the SDK reports —
categories, severities, ids, Danish labels — moved to the SDK and live in
[`ihcclient/docs/problem-catalogue.md`](../../../ihcclient/docs/problem-catalogue.md). Every id there is
unchanged by the move; look for a `.vis` finding there, not here.

## What belongs here

A code whose first dotted segment is `app` is host-owned; every other code is the SDK's. That single rule is
what a scan reads, and it is recoverable from the code alone — `ProblemCode.IsHostOwned` is the one predicate
in the codebase that answers it.

OpenVisual mints into `app.openvisual.*`, and only for **operation outcomes** — something the app itself
refused or could not carry through, such as a transfer attempted with no controller connected
(`controller-required-send`) or a report that was written but could not be opened in a viewer
(`report-not-openable`).
A host never authors a *finding* about a project: findings are statements about the `.vis` file, the SDK owns
the file, and a second opinion about it minted in an app is how two catalogues start disagreeing.

## The rules this family follows

A reserved family buys a host its own code space, not an exemption from governance. The same schema and the
same checks apply here as to the SDK's own families:

- **Ids are unique across every family**, not merely within this one.
- **Every code has an entry.** A code minted with nothing behind it is the defect the completeness check
  exists to catch.
- **Arguments are declared, typed and arity-checked** — data (names, ids, numbers), never words or sentence
  fragments, so the message stays translatable as one unit.
- **User-facing text is Danish**, following the same phrasing standard as the SDK's. A row is one whole
  sentence, written on the entry — either fixed (*Rapporten kunne ikke vises.*) or carrying declared slots
  (*Funktionsblokken '{name}' kunne ikke gemmes som '{path}'.*), which the producer binds. What is forbidden is
  ASSEMBLY: a sentence stitched together at render time out of fragments, which is untranslatable as a unit and
  is why the argument contract carries data rather than words. The English engine sentence travels in the
  diagnostic slot and goes to the log.
- **Retirement reserves the id.** Nothing here is ever deleted and reused for a different condition.

## Rows

The rows are DECLARATIONS, in
[`Services/HostProblemCatalog.cs`](../Services/HostProblemCatalog.cs) — the code is the truth here exactly as
it is for the SDK's catalogue. The table below is rendered from those declarations and compared by a test
(`HostProblemCatalogTests.TheRenderedRowTableMatchesTheCheckedInAppendix`), so this document cannot fall behind
the code. Add a code by adding a declaration, then regenerate.

<!-- GENERATED: host rows — rendered from the declarations; do not edit by hand -->
Every code this app mints, as the code itself declares it. This table is RENDERED from
`applications/ihc_openvisual/Services/HostProblemCatalog.cs` and compared by a test, so it
cannot fall behind the declarations. Edit the declarations, not this table.

There is no category column: this family authors operation outcomes, and the eight categories
classify project content the SDK owns.

| Id | Costs | Kind | Status | Danish label |
| --- | --- | --- | --- | --- |
| `app.openvisual.block-export-failed` | Refusal | OperationOutcome | Active | Funktionsblokken '{name}' kunne ikke gemmes som '{path}'. |
| `app.openvisual.catalog-file-rejected` | Refusal | OperationOutcome | Active | Filen '{file}' er ikke en gyldig produkt- eller funktionsblok-definitionsfil. |
| `app.openvisual.catalog-folder-missing` | Refusal | OperationOutcome | Active | Mappen '{folder}' findes ikke. |
| `app.openvisual.catalog-import-stopped` | Refusal | OperationOutcome | Active | Filen '{file}' kunne ikke importeres. {count} fil(er) blev importeret før den. |
| `app.openvisual.controller-required-retrieve` | Refusal | OperationOutcome | Active | Hentning kræver en tilsluttet controller. Denne version kontakter ingen controller. |
| `app.openvisual.controller-required-send` | Refusal | OperationOutcome | Active | Afsendelse kræver en tilsluttet controller. Denne version kontakter ingen controller. |
| `app.openvisual.edit-failed` | Refusal | OperationOutcome | Active | Redigeringen kunne ikke gennemføres på grund af en intern fejl. Ændringen blev ikke gemt. |
| `app.openvisual.project-open-failed` | Refusal | OperationOutcome | Active | Projektet '{path}' kunne ikke åbnes. |
| `app.openvisual.project-save-failed` | Refusal | OperationOutcome | Active | Projektet kunne ikke gemmes som '{path}'. |
| `app.openvisual.report-not-openable` | Refusal | OperationOutcome | Active | Rapporten blev dannet, men kunne ikke åbnes i en fremviser.<br>Filen ligger her:<br>{path} |
| `app.openvisual.report-save-failed` | Refusal | OperationOutcome | Active | Rapporten kunne ikke gemmes. |
| `app.openvisual.report-view-failed` | Refusal | OperationOutcome | Active | Rapporten kunne ikke vises. |
| `app.openvisual.telemetry-host-missing` | Refusal | OperationOutcome | Active | Der er ikke konfigureret nogen telemetri-vært i ihcsettings.json. |
| `app.openvisual.telemetry-host-unreachable` | Refusal | OperationOutcome | Active | Telemetri-værten '{host}' kunne ikke åbnes. |
| `app.openvisual.unexpected` | Refusal | OperationOutcome | Active | Handlingen kunne ikke gennemføres på grund af en intern fejl. Detaljerne er skrevet til loggen. |
| `app.openvisual.validation-errors-block-send` | Refusal | OperationOutcome | Active | Projektet indeholder fejl. Ret dem i Problemer-panelet, før projektet sendes. |

**Total: 16 host codes.**
<!-- END GENERATED -->
