# Migrating to the 0.8 supported API

This note maps every capability withdrawn from `ihcclient`'s public surface in the 0.8 API-boundary pass to
the supported way of doing the same thing. It is derived from the two public-API baselines, not from memory:
every row that left `PublicAPI.Shipped.txt` or `PublicAPI.Unshipped.txt` is accounted for below.

Why this was allowed at all: 0.8.x is an unpublished beta, so surface may be narrowed without a staged
deprecation release. That makes the change *permitted*, not compatible — any source or binary consumer that
used a removed member still breaks, which is what this note exists to answer.

What the boundary now is, and why, is [the API contract](api-contract.md). This note is the delta.

## 1. The one rule behind every removal

The SDK supports **two service tiers** — the controller API services and the application services — plus the
inputs, results, domain values, commands, events, documented failures, approved helpers and provider
contracts they deliberately publish. Everything else beneath them is implementation, and appearing in a
public signature was never by itself an approval. What follows is that rule applied.

## 2. Catalog construction and the catalog provider seam

**Withdrawn.** `Ihc.Vis.Catalog.ICatalog`, `BuiltInCatalog`, `CompositeCatalog`, `MaterializedCatalog`,
`CatalogDiscovery`, `CatalogDocReader`, `CatalogReader`, `CatalogFileWriter`, `CatalogLayout`,
`CatalogFormatException`, `DefinitionBuilderBase<TSelf>`, `Ihc.Vis.Products.ProductDefinitionBuilder`,
`Ihc.Vis.FunctionBlocks.FunctionBlockDefinitionBuilder`, `Ihc.Vis.Model.CatalogGrammarBuilder`,
`Ihc.Vis.Editing.InputBuilder`, `OutputBuilder`, `SettingBuilder`, `PartyInfoBuilder`, `ProjectInfoBuilder`,
`Ihc.Vis.FunctionBlocks.FbResourceDefBuilder`, `Ihc.Vis.Products.ProductDialogPresets`, and the
`ProjectAppService(IhcSettings, ICatalog, TimeProvider, …)` constructor.

**Instead.** Extend a catalog by importing the vendor's own file formats:

```csharp
var projects = new ProjectAppService(settings);
projects.ImportCatalogFile(pathToDefOrIfb);          // a *.def product or *.ifb function block

IReadOnlyList<ProductDefinition> products = projects.GetAvailableProducts();
IReadOnlyList<CatalogItem> items = projects.GetProductCatalogItems();
```

`ImportCatalogFile`, `GetAvailableProducts`, `GetAvailableFunctionBlocks`, `GetProductCatalogItems`,
`GetFunctionBlockCatalogItems`, `GetProductCatalogItem`, `ResolveProduct` and `GetProductDialog` are all
supported and unchanged. `ProductDefinition` and `FunctionBlockDefinition` remain public as the results.

**Why.** A C# builder commits the SDK to one in-memory representation of a catalog definition; the file
formats commit it to the vendor's, which it already reads and writes byte-identically. With the builders
gone, an `ICatalog` implementer could only hand-populate records — the same commitment by another route —
so the seam went with them. Test doubles are unaffected in-repo: the interface is still there, just internal.

**Determinism.** A caller that supplied its own clock through the injecting constructor uses the new
overload, which exposes no catalog:

```csharp
var projects = new ProjectAppService(settings, timeProvider);   // reproducible id1/id2 stamps
```

## 3. Editing handles and the project editor

**Withdrawn.** `Ihc.Vis.Editing.ProjectEditor` and `ProjectEditingExtensions` (so `project.Edit()`), the
handle types `ElementRef`, `ResourceRef`, `FunctionBlockRef`, `GroupRef`, `ScenesRef`, `SubProgramRef`,
`BranchRef`, `CaseRef`, `ConditionRef`, `ConditionsGroupRef`, `EnumDefinitionRef`, `ProductRef`, `AttrInfo`,
`LinkInfo`, `ProgramBuilder`, and the `Ihc.Vis.FunctionBlocks` builder handles `FbBranchRef`, `FbCaseRef`,
`FbConditionRef`, `FbConditionsGroupRef`, `FbEnumDefRef`, `FbOperand`, `FbProgramBuilder`,
`FbResourceHandle`, `FbSubProgramRef`.

**Instead.** Mint a command from `ProjectCommands` and apply it through the document port:

```csharp
IProjectDocument document = projects.OpenDocument(project);
EditVerdict verdict = document.CanApply(command);
EditOutcome outcome = document.Apply(command);
```

Every authoring operation the handles offered has a command factory on `ProjectAppService.Commands`. The
stateless `Apply`/`CanApply`/`Preview` on the service remain for one-shot callers.

**Why.** The handles mutate below the command layer, so an edit made through them has no undo entry, no
change set and no legality gate — the three things the session exists to provide.

## 4. The validation engine

**Withdrawn.** Every rule class in `Ihc.Vis.Validation` — `CapacityRules`, `DatalineAddressRules`, `DeviceAddressRules`, `DeviceSettingRules`, `DeviceStateRules`, `DocumentationCompletenessRules`, `DocumentationRules`, `EnumDefinitionRules`, `FunctionBlockShapeRules`, `IdentityRules`, `ModuleAddressRules`, `NamingRules`, `ProductAdvisoryRules`, `ProgramDataflowRules`, `ProgramShapeRules`, `ProjectStructureRules`, `ReciprocityAndEnumRules`, `ScenarioRules`, `SchemaConformanceRules`, `StructureRules`, `VariableUsageRules`, `WiringRules` — the rule-registration types
`RuleDefinition`, `RuleBuilder`, `RuleSet`, `ProjectRules`, `IProjectInspection` and the `ProjectInspection`
delegate, the analyses `IProjectAnalyses`, `IIdAnalysis`, `ITopologyAnalysis`, `IProgramUsageAnalysis`,
`DuplicateIdGroup`, `LastUniqueIdFault`, `VariableUsage`, `CaseTest`, the executors `WholeProjectValidator`
and `IWholeProjectValidator`, the `FieldMetadataFace` extension block, and the plumbing `ValidationProfile`,
`RuleFailurePolicy`, `RuleRegistrationException`, `IValueConstraint`, `RequiredFieldConstraint`,
`ConstraintSequence`, `ValueConstraintVerdict`, `DeclaredThreshold`, `DeclaredFirmwareBound`,
`ThresholdConfidence`, `CatalogInvariants`, `ProjectVerification`, `ControllerFirmwareVersion`,
`ControllerCapabilityLimits`, `CategoryExtensions`.

**Instead.** Validation is asked of the service, in the shape the answer is wanted:

```csharp
ProjectValidationResult flat = projects.Validate(project);
ProjectValidationResult grouped = projects.ValidateCategorized(project);
FieldConstraintMetadata field = projects.DescribeField(target);   // what a dialog binds
```

`ProjectValidationResult`, `ProjectValidationFinding`, `ValidationFinding`, `ValidationSeverity`,
`ValidationCategory`, `FindingLocation`, `FixLocation`, `ValidationGate` and `ProblemCatalog` /
`ProblemCatalogEntry` all remain public — the findings, their vocabulary, the gate helpers, and the
catalogue a host declares its own `app.*` rows through.

**Why.** A rule set assembled or executed outside the engine is a second validation truth: the finding a user
sees and the finding that blocks a save stop being the same finding.

## 5. Projections the frontend used to compute

**Withdrawn.** `Ihc.Vis.Products.ProductClassifier` and `ProductFamily`, `Ihc.Vis.Schema.ProductRows`,
`VariableTypeRegistry` and `VariableTypeInfo`, `Ihc.Vis.Model.FunctionBlockSections`, and the program-method
tables `Ihc.Vis.Programs.ProgramMethodCatalog`, `ProgramMethod`, `ProgramPinType`, `ProgramMethodCategory`.

**Instead**, ask the read view or the service for the fact itself:

| Was | Now |
| --- | --- |
| `ProductClassifier.IsProduct(tag)` | `element.Kind is ElementKind.Product` |
| `ProductRows.IsSetting(element.GetAttribute("setting"))` | `project.View(element).IsSetting` |
| `ProductRows.IsStructuralChild(tag)` | `project.View(element).IsStructural` |
| `ProductRows.IsHiddenFromTree(tag, setting)` | `project.View(element).IsHiddenFromTree` |
| `FunctionBlockSections.All` | `project.View(block).BlockSections` — `FunctionBlockSectionInfo(Container, Caption, IsInternal)` |
| `VariableTypeRegistry.ValueTypeTags` | `projects.GetInsertableVariableTypes(project, containerId)` — each `InsertableVariableType(Tag, IsSignal)` |
| `ProgramMethodCatalog.EventsFor` / `CommandsFor` / `ConditionsFor` / `ClassifyPin` / `BoolOutputOnlyCommandTokens` / `EligibleCaseVariableTags` / `NumericVariableTags` / `ArithmeticToken` | `projects.GetProgramOperations(project, containerId, variableId)` |

`GetProgramOperations` is new in 0.8 and answers the whole question a frontend used to assemble from those
tables: which operations the armed variable may be given in that container, and which operands each may be
completed with — as `ProgramOperationOffer` / `ProgramOperandOffer` descriptors with stable identifiers.
`GetInsertableVariableTypes` changed shape at the same time, from `IReadOnlyList<string>` to
`IReadOnlyList<InsertableVariableType>`; a caller that only wants the tags reads `.Select(t => t.Tag)`.

**Why.** Each of these was a frontend re-deriving a domain fact from tag spellings. Which family a tag
belongs to, which commands need a bool output, and which arithmetic cells are authorable are the engine's
answers, and a copy in a frontend is how the offered set and the accepted set drift apart.

## 6. IO and session implementation

**Withdrawn.** `Ihc.Vis.Io.ProjectSerializer` and `Ihc.Vis.Session.ProjectDocumentSession`.

**Instead.** `ProjectAppService.Load`/`Save`/`SaveDocument` for bytes, and `OpenDocument`, which returns the
`IProjectDocument` port the concrete session implements. Every member a caller used on the session is on the
port.

## 7. Narrowed members on types that remain public

| Member | Now | Instead |
| --- | --- | --- |
| `ProductDefinition.Grammar`, `.SourceEncoding` | internal | Nothing: these describe how the definition is re-emitted to a file, which the SDK owns. |
| `FunctionBlockDefinition.Grammar`, `.SourceEncoding`, `.ExplicitCloseIds` | internal | As above. |
| `Project.InlineDtdBlocks` | internal | Nothing: captured provenance about the source file. The open-world round trip it enables is unchanged and is the engine's promise. |
| `ProblemCatalogEntry.Thresholds`, `.FirmwareBound` | internal | Nothing: declared evidence behind an SDK rule's predicate. A host declaring its own `app.*` row sets neither. |
| `ProjectVerification.Run` / `.RunStructured` taking an `IWholeProjectValidator` | internal | `ProjectAppService.Validate` / `ValidateCategorized`. |
| `Ihc.CopyUtil.DeepCopyAndApply` | internal | Nothing: a reflective deep-copy mechanism the SDK uses internally. |

## 8. Additions

Nothing in this note is a pure loss of capability. New in the same pass:
`ProjectAppService(IhcSettings, TimeProvider)`, `ProjectAppService.GetProgramOperations`,
`ProgramOperationOffer` / `ProgramOperandOffer` / `ProgramOperationKind`, `InsertableVariableType`,
`FunctionBlockSectionInfo`, and the read-view facts `ElementView.IsSetting`, `.IsStructural`,
`.IsHiddenFromTree` and `.BlockSections`.

## 9. Two consumer prohibitions

Unchanged in accessibility, newly bounded in use: the generated `Ihc.Soap` layer and the `Ihc.Envelope`
roots stay public because `XmlSerializer` binds public types, and both now carry a compile-time consumer ban
with the same documented opt-out. Bind neither; call the `Ihc` service interfaces and use the SDK models.
