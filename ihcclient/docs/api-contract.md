# The supported `ihcclient` API

This document is the specification of what `ihcclient` supports as API. It states which entry points a
client may call, which types it may construct, implement or subscribe to, and which types are
implementation that happens to be compiled into the same assembly. It is the reference an API change is
reviewed against, and the written specification the SDK boundary test in `safe_architecture_tests` is
built to enforce.

It is scoped to the SDK assembly. Application-level policy lives in
[`ARCHITECTURE.md`](../../ARCHITECTURE.md); the decision behind the two-tier shape is
[ADR-002](../../docs/adr/ADR-002-thick-sdk-services-and-thin-apps.md); the coded failures the SDK can
report are catalogued in [the problem catalogue](problem-catalogue.md).

Compatibility stance: `ihcclient` 0.8.x is an unpublished beta, so public surface may be narrowed
without a staged deprecation release. Narrowing still breaks any source or binary consumer that used
the removed member — the absence of a published package makes the change permitted, not compatible.
Every capability removed from the public baselines is mapped to its supported replacement in
[the 0.8 migration note](api-migration-0.8.md).

## 1. Two jobs, two supported service tiers

`ihcclient` contains a controller SDK and a controller-free `.vis` engine in one assembly. The
controller SDK's API services expose SDK models over a private SOAP adapter layer. Application services
provide use-case behaviour over those services, over the offline engine, or over both.

| Supported entry | Responsibility | Client choice |
| --- | --- | --- |
| API services in `ihcclient/src/api/services/`, marker contract `IIHCApiService` | Controller capabilities and SOAP-to-SDK mapping. No application workflow policy. | Call directly and compose a custom use case. |
| Application services in `ihcclient/src/app/`, marker contract `IIHCAppService` | Use-case workflow, policy, state and integration over the engine and the API services. | Use the supplied high-level behaviour. |
| Supporting and provider contracts | Inputs, results, domain values, commands, events, documented exceptions, configuration and approved helpers. | Use according to each member's documented contract. |

Both tiers are supported. A client may use application services for the high-level use cases, API
services to compose its own workflow, or both together. Choosing the API-service tier is a legitimate
composition, not a boundary violation, and no rule may remove that choice. What a consumer rule may
still constrain is lifecycle and presentation ownership inside a particular application — OpenVisual
keeps its edits on one open document because that application owns a document lifecycle, not because
the stateless operations are unsupported for other callers.

Application-service entry points today: `AdminAppService`, `InformationAppService`, `LabAppService` and
`ProjectAppService`. `ProjectAppService` belongs to the application tier even though it lives in the
`Ihc.Vis` namespace rather than `Ihc.App`: namespace placement does not determine contract ownership.
Its `Commands` gateway and the `IProjectDocument` it returns are part of the supported project API.

Implementation beneath either tier is private, except for explicitly documented technical exposure —
today the generated `Ihc.Soap` layer and the envelope types the XML serializer requires to be public.
Those exceptions are bounded by consumer bans rather than by accessibility, and are listed in section 5.

## 2. Contract roles, and the question each one answers

Every publicly visible type in the SDK holds exactly one of these roles. The review question is what an
API change has to answer before the type stays public.

| Contract role | Examples | Review question |
| --- | --- | --- |
| Service entry points | The API-service classes and interfaces; `ProjectAppService`, `AdminAppService`, `InformationAppService`, `LabAppService` | What capability or use case does the entry own? |
| Client-facing ports | `IProjectDocument`, `ProjectCommands` | What state, lifetime and operations can the client rely on? |
| Inputs and results | `ProjectSaveOptions`, the catalog item records, command outcomes, findings | Which data and members does a caller need in order to use the operation? |
| Domain values and helpers | `ElementId`, `DatalineAddress`, `ProjectElement`, `ReportMimeTypes` | Does each member express a supported domain operation or a caller convenience? |
| Failure contracts | `ProjectFormatException`, `RefusedOperationException`, `EditRefusals`, `IProblemCarrier` and the structured `Problem` values | Which failures escape an operation, and how does a client handle them? |
| Provider contracts (SPI) | `IReportIconProvider` | May an external client implement this, and what must an implementation provide? |
| Public implementation exceptions | The generated SOAP message types, the envelope roots, the cookie-handling interfaces | Why must this stay visible, and which consumers are prevented from using it? |

A review covers parameters, returns, properties and fields, public and protected construction, extension
points, base types and implemented interfaces, generic constraints, delegate and event payloads, and the
metadata that a caller can observe. It also covers what a signature cannot express: which exceptions
escape, who owns a returned snapshot, on which thread an event is delivered, and when I/O has completed.
Those are contract properties as much as the type graph is, and a migration must preserve the ones that
already hold.

Two rules constrain the classification itself:

- **Appearing in a public signature does not make a type contract.** A public member that returns an
  implementation type is a finding about that member, not an approval of the type. This replaces the
  older "does any facade signature hand this type to the caller" closure rule, which approved too much
  (representation types dragged in by a returned definition) and too little (a parameter-only options
  type, an exception, an extension holder or a static constant is legitimate API even though nothing
  returns it).
- **A public interface does not license naming its implementation.** A client depends on the port; the
  class behind it stays hidden unless it holds a role of its own in the table above.

Narrowing is the smallest edit that removes the exposure: narrow a member or a constructor when that is
enough. Neither a namespace move nor a wrapper DTO layer is required just to move a count.

## 3. Rulings that this pass settles

### 3.1 Catalog extension is file-based

A caller extends a catalog by importing vendor-format files — function blocks (`*.ifb`) and products
(`*.def`) — not by constructing definitions in C#. The supported contract is:

- `Ihc.Vis.ProjectAppService.ImportCatalogFile(string path, Func<string, string?>? documentationProbe = null)`,
  which reads one `*.def` or `*.ifb` file into the service's catalog; and
- the read side that then sees it: `GetAvailableProducts`, `GetAvailableFunctionBlocks`,
  `GetProductCatalogItems`, `GetFunctionBlockCatalogItems`, `GetProductCatalogItem`, `ResolveProduct`
  and `GetProductDialog`.

Both already ship. `ProductDefinition` and `FunctionBlockDefinition` stay public as the results those
operations return.

The C# definition **builders are not API**: `ProductDefinitionBuilder`, `FunctionBlockDefinitionBuilder`,
`DefinitionBuilderBase<TSelf>`, `CatalogGrammarBuilder`, `InputBuilder`, `OutputBuilder`,
`SettingBuilder`, `PartyInfoBuilder`, `ProjectInfoBuilder`, `FbResourceDefBuilder`, the `Fb*Ref` handles
and `CatalogLayout` are implementation. They commit the SDK to a particular in-memory representation of
a catalog definition, which the file contract does not.

### 3.1a What a returned definition exposes, member by member

`ProductDefinition` and `FunctionBlockDefinition` are supported RESULTS — section 3.1's read side hands them
back — so what they expose is contract whether or not a caller could build one. Reviewed member by member now
that C# construction is gone:

| Member | Verdict | Why |
| --- | --- | --- |
| `ProductIdentifier`, `MasterType`, `MasterVersion`, `MasterName` | public | The identity a caller addresses the definition by, and what it passes back to an insert command. |
| `DisplayName`, `CategoryPath` | public | What a caller shows and groups by; the catalog-item projection carries the same two. |
| `Body` | public | The definition's element tree, which the placement commands read and a caller may inspect. |
| `Documentation` | public | The vendor documentation attached to the definition — a domain value, not representation. |
| `Resources`, `Inputs`, `Outputs`, `Settings`, `InternalVariables`, `IsEmptyTemplate` | public | Computed projections over `Body`, i.e. the same information already exposed, in the shape a caller wants. |
| `Grammar` | **internal** | The definition's catalog GRAMMAR — how its type is declared in a `.def`/`.ifb`. Pure serialization representation: it exists so a write re-emits the source file's declarations, and no supported operation asks a caller for it or acts on it. |
| `SourceEncoding` | **internal** | Which encoding the source file used. Provenance of the same kind, and the reason a re-write is byte-identical rather than something a caller chooses. |
| `ExplicitCloseIds` | **internal** | Which elements the source file closed explicitly rather than self-closing — byte-level fidelity detail, invisible in the domain. |
| The positional constructors | public, deliberately | Every parameter is a value the record already hands back through a public getter, so construction publishes nothing the returns do not. A positional record has no way to narrow its primary constructor without ceasing to be positional, which would change `Deconstruct` and the shape of a supported return for no contract gain. What D02 withdrew was the fluent BUILDER surface — a grammar-authoring API — not the four-value record. |

`Project` is reviewed on the same terms: its `Root` is public because the tree is what a caller reads, its
positional constructor stays for the reason above, and its `InlineDtdBlocks` is internal on both halves —
captured provenance about the source file, which the record's own equality already excludes.

### 3.2 The catalog provider seam is implementation

`ICatalog` is internal, together with `CompositeCatalog`, `MaterializedCatalog`, `CatalogDiscovery`,
`CatalogDocReader`, `CatalogReader`, `CatalogFileWriter`, `BuiltInCatalog`, and the
`ProjectAppService(IhcSettings, ICatalog, TimeProvider, ...)` constructor that injects one.

The reasoning is the previous ruling's consequence. With the builders hidden, an external implementer of
`ICatalog` could only produce definitions by hand-populating records, which is precisely the
representation commitment the builders were withdrawn to avoid; and `ICatalog`'s own purpose, as its
documentation states, is to let the service be driven by a fake catalog in tests. It is in-repo
composition plumbing, not a customization capability. The customization capability is section 3.1.

### 3.3 `ProjectAppService(IhcSettings, TimeProvider)` is a supported determinism seam

One public constructor overload takes the settings and a `TimeProvider`:

```csharp
public ProjectAppService(IhcSettings settings, TimeProvider timeProvider)
```

It is supported. A caller that supplies a `TimeProvider` controls the clock the engine stamps a saved
project with — the `.vis` `id1`/`id2` stamps in particular — which is what makes a save reproducible in
a test or a batch tool. It exposes no catalog and no catalog representation, so it does not reopen
section 3.2.

### 3.4 Diagnostic capabilities are not intended for real use

**Do not invoke the dangerous manufacturing/internal-test operations against a live installation.** These
operations are retained primarily for API completeness and to document the controller's service surface.
They are not intended for real use at this stage. Keep `IhcSettings.AllowDangerousInternTestCalls` set to
`false`; selected dangerous operations refuse to run while it is disabled. This flag does not govern every
controller operation that changes state.

**Keep `IhcSettings.LogSensitiveData` set to `false`.** Sensitive-data logging is also discouraged for real
use: enabling it can expose passwords and other private data in telemetry.

Both flags default to `false`. Explicit opt-in by the calling application, together with these usage
warnings, is the accepted protection. No additional runtime authorization gate is required. The presence
of an operation in the API, or the ability to enable it, is not a recommendation to use it or an assurance
that doing so is safe.

## 4. Failure contracts

Failures are values, not strings: an SDK refusal carries a `ProblemCode`, the Danish sentence already
bound for an end user, the declared arguments, and an English diagnostic. A client handles a failure by
its code, and renders the carried message whole rather than re-deriving text from it. The codes
themselves, with their categories and severities, are the
[problem catalogue](problem-catalogue.md).

The exceptions that are part of the supported surface, each because a documented operation throws it:

| Exception | Thrown by | Client handling |
| --- | --- | --- |
| `ProjectFormatException` | `ProjectAppService.Load` when the stream is not a readable `.vis` project | Catch to distinguish an unreadable file from an I/O failure; carries the structured problem. |
| `RefusedOperationException` | An operation whose precondition the current document does not meet | Catch to report the refusal; the carried problem states which precondition failed. |
| `EditRefusedException` | A guard deep inside an edit body, after the command gate already allowed the edit | An edit applied through `IProjectDocument` does NOT see it escape: the session catches it and returns a refused outcome carrying the guard's own code. It is public because the same guards run under a direct editor call, and because its code is the identity a caller compares against `EditRefusalCodes`. |
| `ProjectUploadException` | The controller-backed project upload path | Catch to distinguish a rejected project from a transport failure. |

A refusal an edit command can foresee is reported, not thrown: `IProjectDocument.CanApply` answers the
precondition before the fact, and applying a refused command returns an outcome whose status is
`Refused` and whose code is the gate's own. The exception rows above are the failures that escape
instead.

An operation whose failure contract is not listed here does not have one: it either succeeds or reports
through a returned result. A type in the refusal or exception area that no supported operation can hand
to a caller is implementation, and is narrowed rather than kept for symmetry. Two types were narrowed on
exactly that test: `ProjectSerializer`, the `.vis` writer behind `Save`, and `ProjectDocumentSession`, the
concrete runner behind `IProjectDocument` — no supported signature returns or accepts either, and the port is
what a caller holds. `CatalogFormatException` went with the catalog implementation for the same reason: the
failure a caller sees from a bad catalog file is `RefusedImportException`, which is coded.

## 5. The bounded technical exceptions

Two groups stay public for reasons that are not about contract, and each is bounded by enforcement
rather than by accessibility:

- **The generated SOAP layer** (`Ihc.Soap.*`) is regenerated from the controller's WSDL, so a rule
  written inside it would be lost on the next generation. It stays public and is bounded by a consumer
  ban, with a declared opt-out for the SDK's own transport code.
- **The envelope roots** must stay public because `XmlSerializer` binds public types and fails at
  runtime otherwise — neither `XmlAttributeOverrides` nor a friend grant lifts that. They are bounded
  the same way, by a consumer ban rather than by hiding them.

Neither exception licenses a client to depend on those types. A ban target is the enforcement; this
section is the reason it exists.

## 6. Caller examples

These are API-design samples showing the shape of supported usage. They are illustrative code, not
instructions to run against a controller, and the controller-backed ones need real credentials and a
reachable controller. Each names the failure it handles and the state it owns.

### 6.1 A direct API-service operation

The first tier, used directly. The client owns the authentication lifetime: `AuthenticationService` is
constructed from `IhcSettings`, every authenticated service is constructed from it so they share its
settings and cookie session, and the client decides when the session ends.

```csharp
IhcSettings settings = IhcSettings.GetFromConfiguration(IhcConfiguration.FromAppDirectory());

using var auth = new AuthenticationService(settings);
var resources = new ResourceInteractionService(auth);
try
{
    await auth.Authenticate();

    ResourceValue value = await resources.GetRuntimeValue(resourceId);
    await resources.SetResourceValue(ResourceValue.ToogleBool(value));
}
finally
{
    await auth.Disconnect();   // the client owns the session, so the client closes it
}
```

### 6.2 A custom composition of API services

Composing several API services into a use case the SDK does not ship is a supported client choice, not a
boundary violation. The services share one authenticated session, and the workflow policy — what to read,
in which order, what to do with the result — is the client's.

```csharp
using var auth = new AuthenticationService(settings);
var controller = new ControllerService(auth);
var modules = new ModuleService(auth);
var log = new MessageControlLogService(auth);

await auth.Authenticate();

// The client's own policy: only collect the log when the controller reports a project at all.
bool hasProject = await controller.IsIHCProjectAvailable();
var events = hasProject ? await log.GetEvents() : [];
var sceneProject = hasProject ? await modules.GetSceneProject() : null;
```

A client that instead wants the shipped workflow calls an application service — `AdminAppService`,
`InformationAppService` or `ProjectAppService` — and gets the same capability with the policy already
decided. Both are supported; neither is a fallback for the other.

### 6.3 Load, read and save a project

The offline engine needs no controller. `ProjectAppService` owns no document state in this flow: `Load`
returns an immutable `Project` the caller holds, and `Save` writes the snapshot it is given.

```csharp
var projects = new ProjectAppService(settings);

Project project = await projects.Load(path);

// Read: the project is an immutable snapshot, so a read needs no lock and no copy. A raw
// ProjectElement carries the .vis tag and its attributes; the named, typed reading of one is the
// ElementView the project projects it into.
foreach (ProjectElement locality in project.Groups)
{
    ElementView view = project.View(locality);
    Console.WriteLine(view.Name);
}

await projects.Save(project, outputPath);
```

Save options are the caller's: `ProjectSaveOptions` decides what the write is allowed to do. Because
edits replace object instances rather than mutating them, a caller retains an element by its `ElementId`
and re-reads it from the current project rather than holding the element itself.

### 6.4 An interactive edit through one document

An application with a document lifecycle opens ONE document and applies commands to it. The document owns
the undo/redo history, the dirty flag and the version; the caller owns nothing but the reference.

```csharp
IProjectDocument document = projects.OpenDocument(project);
try
{
    ProjectCommand command =
        ProjectCommands.RenameLocality(document.Current!, localityId, "Stue", note: "");

    // Ask before acting: the verdict carries the refusal reason a menu gate would show.
    EditVerdict verdict = document.CanApply(command);
    if (verdict.Ok)
    {
        EditOutcome outcome = document.Apply(command);
        switch (outcome.Status)
        {
            case EditStatus.Committed: break;                     // document.Version advanced
            case EditStatus.NoChange:  break;                     // legal, but nothing to do
            case EditStatus.Refused:   Report(outcome.Code, outcome.Reason); break;
            case EditStatus.Failed:    Report(outcome.Code, outcome.Reason); break;
        }
    }

    if (document.CanUndo) document.Undo();

    await projects.SaveDocument(document.Current!, path);
    document.MarkSaved(document.Current!);   // the document's dirty flag is the document's to clear
}
finally
{
    document.Close();
}
```

Two guarantees this flow relies on. `document.Current` is a snapshot: the instance a caller reads stays
unchanged when the next command commits, and the caller re-reads `Current` to see the new state. The
`Changed` and `StateChanged` events are raised on the thread that applied the command — the SDK posts
nothing to a UI thread of its own accord, so an application with an affinity requirement marshals them
itself.

### 6.5 Program-authoring choices

Authoring a program is the same command flow, with every choice coming from the service rather than from
the caller's own tables. `GetProgramOperations` answers what the armed variable may be given in this
container — the pin's operator family, the commands that need a bool output, whether a case may switch on
it, and the arithmetic whose opcode depends on the operand — each as a descriptor carrying the vendor
method token and the persisted name and note the matching command takes.

```csharp
foreach (ProgramOperationOffer offer in projects.GetProgramOperations(project, containerId, variableId))
{
    if (!offer.NeedsOperand)
    {
        document.Apply(app.Commands.AddProgramCommand(
            document.Current!, containerId, variableId, offer.Method, offer.NameTemplate, offer.Note));
        continue;
    }

    // A two-operand row is completed from the operands the offer carries; each names the method for ITS
    // pairing, which is what makes an arithmetic opcode right for the target and operand types together.
    foreach (ProgramOperandOffer operand in offer.Operands)
    {
        document.Apply(app.Commands.AddArithmeticCommand(
            document.Current!, containerId, variableId, operand.Method, operand.VariableId,
            offer.NameTemplate, operand.Note));
    }
}
```

Which variable types a container accepts is the same shape of question:

```csharp
foreach (InsertableVariableType type in projects.GetInsertableVariableTypes(project, containerId))
{
    // type.IsSignal separates the block's wiring from the values it holds.
}
```

Legality is rechecked when the command executes, against the document as it is then — an offer read
earlier is a menu, not a promise, and a caller does not have to re-ask before applying. The offers decide
nothing about presentation: labels, menu organisation and ordering stay the caller's, which is why an
offer carries the persisted templates rather than a rendered string.

### 6.6 Handling load and validation failures

The two failure shapes are different on purpose. A load either produces a project or throws; a validation
always produces a result and never throws.

```csharp
Project project;
try
{
    project = await projects.Load(path);
}
catch (ProjectFormatException ex)          // the file is not a readable .vis project
{
    // The exception message is the ENGLISH diagnostic. The Danish sentence for an end user is the
    // carried problem's, rendered whole rather than re-worded.
    ProblemChain? chain = ((IProblemCarrier)ex).Problems;
    string code = chain?.Cause.Code.Value ?? "io.load";
    ShowUser(chain?.Cause.Message ?? ex.Message, code);
    return;
}
catch (IOException ex)                     // the file could not be read at all
{
    ShowUser(ex.Message, "io");
    return;
}

ProjectValidationResult result = projects.ValidateCategorized(project);
foreach (ProjectValidationFinding finding in result.Findings)
{
    // RuleId is the stable filtering key; Severity and Category decide presentation; Locator points
    // at the element. Message is already the user-facing sentence.
    Present(finding.RuleId, finding.Severity, finding.Category, finding.Locator, finding.Message);
}
```

A refusal is handled by its code, never by matching its sentence: the code is stable, the sentence is
user-facing text that may be re-worded. `IProblemCarrier` is the one type test that spans the refusal
exceptions, whose base types differ because their callers' do — a failed write stays an `IOException`
and a schema guard stays an `InvalidOperationException`.

## 7. The boundary fitness test — specification

This section specifies the architecture test that enforces the preceding sections. It is written before
the test exists so the test is built against a stated rule rather than against whatever the tree happens
to contain, and so a reviewer can tell a deliberate approval from an accident. The test lives in
`safe_architecture_tests`, and it is activated last, after every visibility change this pass makes.

### 7.1 What it checks

One question: **does any type the supported API hands a caller, or asks a caller for, fall outside the
approved contract set?** A type reached from a seed through a public signature is either approved — it
holds one of the roles in section 2 — or it is implementation, and reaching it is a finding.

The test does not measure exposure by counting, and it does not treat reachability as approval. The
older calculation this replaces started from every type in three namespaces and followed public
signatures, which is reachability from a namespace, not a definition of the supported API: a root DTO
became a seed automatically however much its surface needed narrowing, a returned definition dragged
grammar and representation types in behind it, and static companions, sub-namespace extension holders,
inheritance and exceptions were handled inconsistently. Its out-of-closure figures were therefore
neither a lower nor an upper bound on unintended exposure — candidates for investigation, not a verdict.

### 7.2 The seed set

The seeds are enumerated explicitly, never derived from a namespace:

- every API-service entry point of the first tier, and its `IIHCApiService` contract;
- every application service of the second tier, and its `IIHCAppService` contract;
- the client-facing ports the services return — `IProjectDocument`, `ProjectCommands`;
- the companion and provider contracts section 2 approves, each listed by name, including the
  documented failure contracts of section 4 and the provider interfaces a client may implement;
- the extension holders a caller reaches WITHOUT holding a service — the read views over `Project` and
  `ProjectElement`, and the finding helpers. An extension member is reached through the type it extends,
  so a seed set of entry points alone would leave that whole surface unmeasured; and a documented failure
  contract is thrown rather than returned, so no traversal reaches one either. Both are seeded by name.

An explicit list is the point. A namespace-derived seed set silently re-approves whatever is added to
that namespace next, which is the failure mode of the calculation it replaces. Adding a seed is a
deliberate edit that a reviewer sees.

### 7.3 What the traversal follows

From each seed, and transitively through each approved type it reaches:

- method return types and parameter types, including `ref`, `out` and `in` parameters;
- property types and indexer parameter types; field types;
- event payloads, followed into the delegate's own `Invoke` signature;
- public and protected constructors — construction is contract;
- base types and implemented interfaces;
- generic arguments and generic parameter constraints, recursively, and array element types;
A public NESTED type is reached the same way any other type is — when a signature names it. It is
deliberately not enumerated for its own sake: a nested type no member hands the caller is unreachable, and
listing it would report exposure that does not exist.

The existing `IProjectDocument` port scan already implements this traversal shape — `ContractHierarchy`
and `DeclaredContractTypes` in `IhcClientArchitectureTests`, over the recursive `TypeAndArguments`
expansion in `ArchRuleHelpers` — and the new test reuses that logic. Reusing it does not make the narrower port rule evidence for this one: the
port scan asks whether ONE port names an engine type, over two named namespaces. This test asks whether
the WHOLE supported surface stays inside the approved set.

### 7.4 How the awkward shapes are decided

| Shape | Ruling |
| --- | --- |
| Inheritance | A public base type or implemented interface of an approved type is itself reached, and must be approved on its own. Being the base of a contract does not confer approval. |
| Extension holders | A static extension class is attributed to the type it extends: if the extended type is approved and the extension is listed as an approved helper, its signature is traversed like any other member. An extension over an implementation type is a finding regardless of where it lives. |
| Static companions | A static class holding constants or factories is approved by name, like any other companion. Its members are traversed; being static exempts nothing. |
| Documented exceptions | An exception type is approved only when section 4 lists it against the operation that throws it. C# signatures do not enumerate thrown exceptions, so this is the one part of the contract the traversal cannot discover — the list is maintained by hand and the test asserts each listed type exists and is public, so a rename cannot silently empty it. |
| Generated SOAP layer and envelope roots | Excluded from the traversal, because they are the bounded technical exceptions of section 5. The exclusion is by the same opt-out shape the consumer bans use, so it names itself rather than hiding in a filter. |
| Obsolete members | Not exempt. A member marked obsolete is still public surface until it is removed. |

### 7.5 The rule that must not be softened

**A newly added public member returning an implementation type does not approve that type.** The test
reports the member; the resolution is to narrow the member, to return an approved type instead, or —
when the type genuinely holds a contract role — to add it to the seed or approval list as a reviewed
decision. Auto-approving whatever a public signature happens to name would reproduce the closure rule
this document replaced, and would make the test agree with any change made to the code it checks.

### 7.6 Proving the test works

Three pieces of evidence are required before the test counts as active, because a structural test that
passes over an empty set looks exactly like a clean surface:

1. **Seeded violators.** A synthetic contract that leaks an implementation type through a return,
   through a parameter, and through a generic argument, and the test must report all three. The seeds
   are then withdrawn and the test must go green.
2. **A vacuity guard.** The traversal must be asserted to have observed a known-real contract type from
   the live surface, so a reflection slip that returns nothing cannot read as purity.
3. **A name-resolution guard** for every hand-maintained list entry — approved companions and documented
   exceptions — asserting the name still resolves, so a rename empties nothing quietly.

### 7.7 What it deliberately does not cover

- **Member-level narrowing on an approved type.** The test asks which types are reachable; whether an
  approved type exposes one member too many is a review question against section 2, supported by the
  public-API baseline files rather than by this test.
- **Behavioural contract.** Snapshot ownership, event delivery thread, cancellation and I/O completion
  are contract properties no signature traversal can see.
- **Consumer rules.** Which application may call which tier is a separate question with its own rules;
  this test judges the SDK's surface, not its callers.
