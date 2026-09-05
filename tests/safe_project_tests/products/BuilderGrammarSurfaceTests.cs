using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The grammar↔builder invariants that must not rot silently:
    /// (1) the reflection classification — every public verb of the builder surface is classified in the reviewed
    /// map below as a closed emitter (with its emitted tags, family-scoped where applicable), dynamic-raw (the
    /// caller supplies the tag) or non-emitting, so an UNCLASSIFIED new verb fails this test at the PR that adds
    /// it; and every closed-emitter tag must be declared by its family preset (tag presence — preset CONTENT is
    /// byte-pinned by the designated oracle tests);
    /// (2) <c>.ExtendGrammar</c> extends the preset without disturbing it;
    /// (3) the grammar↔body <c>Validate()</c> advisory categories are non-blocking warnings.
    /// </summary>
    public class BuilderGrammarSurfaceTests
    {
        // ---- (1) reflection classification ----

        private sealed record Verb(string Kind, string[] Tags, string[] Families)
        {
            public static Verb NonEmitting { get; } = new("non-emitting", Array.Empty<string>(), Array.Empty<string>());
            public static Verb Dynamic { get; } = new("dynamic-raw", Array.Empty<string>(), Array.Empty<string>());
            public static Verb Closed(string[] tags, params string[] families) => new("closed", tags,
                families.Length > 0 ? families : new[] { "FunctionBlock" });
        }

        private static readonly string[] AllProductFamilies =
            { "Dataline", "Airlink", "Rs485LedDimmer", "Rs485SmsModem", "S0Device" };

        private static readonly Type[] SurfaceTypes =
        {
            typeof(DefinitionBuilderBase<>),
            typeof(ProductDefinitionBuilder), typeof(ProductResourceDefBuilder),
            typeof(FunctionBlockDefinitionBuilder), typeof(FbResourceDefBuilder),
            typeof(FbProgramBuilder), typeof(FbSubProgramRef), typeof(FbConditionsGroupRef),
            typeof(FbBranchRef), typeof(FbCaseRef), typeof(FbConditionRef), typeof(FbEnumDefRef),
            typeof(FbOperand), typeof(FbResourceHandle),
        };

        // The REVIEWED map. Adding a public verb without classifying it here fails the classification test —
        // deliberately: the reviewer must decide whether the new verb emits a fixed element type (and then extend
        // the family preset) or is dynamic/non-emitting.
        private static readonly Dictionary<string, Verb> Classification = new(StringComparer.Ordinal)
        {
            // ---- DefinitionBuilderBase<TSelf> (shared authoring core of both definition builders) ----
            ["DefinitionBuilderBase`1.CategoryPath(String)"] = Verb.NonEmitting,
            ["DefinitionBuilderBase`1.Grammar(CatalogGrammar)"] = Verb.NonEmitting,
            ["DefinitionBuilderBase`1.ExtendGrammar(Action`1)"] = Verb.NonEmitting,
            ["DefinitionBuilderBase`1.Documentation(String)"] = Verb.NonEmitting,
            ["DefinitionBuilderBase`1.Attribute(String,String)"] = Verb.NonEmitting,   // M7: the raw root-attribute escape hatch moved to the shared base

            // ---- ProductDefinitionBuilder ----
            ["ProductDefinitionBuilder.Dataline(String,String)"] = Verb.Closed(new[] { "product_dataline" }, "Dataline"),
            ["ProductDefinitionBuilder.Airlink(String,String)"] = Verb.Closed(new[] { "product_airlink" }, "Airlink"),
            ["ProductDefinitionBuilder.Rs485LedDimmer(String,String)"] = Verb.Closed(new[] { "product_rs485_led_dimmer" }, "Rs485LedDimmer"),
            ["ProductDefinitionBuilder.Rs485SmsModem(String,String)"] = Verb.Closed(new[] { "product_rs485_sms_modem" }, "Rs485SmsModem"),
            ["ProductDefinitionBuilder.S0Device(String,String)"] = Verb.Closed(new[] { "s0_device" }, "S0Device"),
            ["ProductDefinitionBuilder.Create(String,String,String)"] = Verb.Dynamic,
            ["ProductDefinitionBuilder.From(ProductDefinition)"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.DisplayName(String)"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.Name(String)"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.Locked(Boolean)"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.EnduserReport(Boolean)"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.Note(String)"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.Position(String)"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.CableType(String)"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.CableNumber(String)"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.DocumentationTag(String)"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.PowerGroup(String)"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.AddInput(String,Action`1)"] = Verb.Closed(new[] { "dataline_input" }, "Dataline"),
            ["ProductDefinitionBuilder.AddOutput(String,Action`1)"] = Verb.Closed(new[] { "dataline_output" }, "Dataline"),
            ["ProductDefinitionBuilder.AddScenes(String)"] = Verb.Closed(new[] { "scenes" }, AllProductFamilies),
            ["ProductDefinitionBuilder.AddResource(String,String,Action`1)"] = Verb.Dynamic,
            ["ProductDefinitionBuilder.RawChild(ProjectElement)"] = Verb.Dynamic,
            ["ProductDefinitionBuilder.RawChild(ProjectElement,String)"] = Verb.Dynamic,   // same splice, plus the spliced element's help text
            ["ProductDefinitionBuilder.Validate()"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.Build()"] = Verb.NonEmitting,

            // ---- ProductResourceDefBuilder (attribute setters only) ----
            ["ProductResourceDefBuilder.Address(String)"] = Verb.NonEmitting,
            ["ProductResourceDefBuilder.CableColour(String)"] = Verb.NonEmitting,
            ["ProductResourceDefBuilder.Note(String)"] = Verb.NonEmitting,
            ["ProductResourceDefBuilder.Documentation(String)"] = Verb.NonEmitting,   // help metadata on the resource — never serialized, emits nothing
            ["ProductResourceDefBuilder.Backup(Boolean)"] = Verb.NonEmitting,
            ["ProductResourceDefBuilder.Icon(String)"] = Verb.NonEmitting,
            ["ProductResourceDefBuilder.Attribute(String,String)"] = Verb.NonEmitting,

            // ---- FunctionBlockDefinitionBuilder ----
            // Create/Build always emit the block root and its five fixed containers.
            ["FunctionBlockDefinitionBuilder.Create(String,String,String)"] = Verb.Closed(new[]
                { "functionblock", "inputs", "outputs", "settings", "internalsettings", "programs" }),
            ["FunctionBlockDefinitionBuilder.From(FunctionBlockDefinition)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.DisplayName(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.MasterProgrammer(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.MasterDate(DateOnly)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.VendorMaster(Boolean)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.Locked(Boolean)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.Note(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.AsEmptyTemplate(String)"] = Verb.Closed(new[]
                { "program_simple", "events", "actions" }),
            ["FunctionBlockDefinitionBuilder.InputsNote(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.OutputsNote(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.SettingsNote(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.InternalVariablesNote(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.ProgramsNote(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.InputsName(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.OutputsName(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.SettingsName(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.InternalVariablesName(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.ProgramsName(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.AddInput(String)"] = Verb.Closed(new[] { "resource_input" }),
            ["FunctionBlockDefinitionBuilder.AddInput(String,Action`1)"] = Verb.Closed(new[] { "resource_input" }),    // tag-free short form + configurator
            ["FunctionBlockDefinitionBuilder.AddInput(String,String,Action`1)"] = Verb.Dynamic,
            ["FunctionBlockDefinitionBuilder.AddOutput(String)"] = Verb.Closed(new[] { "resource_output" }),
            ["FunctionBlockDefinitionBuilder.AddOutput(String,Action`1)"] = Verb.Closed(new[] { "resource_output" }),  // tag-free short form + configurator
            ["FunctionBlockDefinitionBuilder.AddOutput(String,String,Action`1)"] = Verb.Dynamic,
            ["FunctionBlockDefinitionBuilder.AddSetting(String,String,Action`1)"] = Verb.Dynamic,
            ["FunctionBlockDefinitionBuilder.AddInternalVariable(String,String,Action`1)"] = Verb.Dynamic,
            ["FunctionBlockDefinitionBuilder.AddEnumDefinition(String)"] = Verb.Closed(new[] { "enum_definition" }),
            ["FunctionBlockDefinitionBuilder.AddEnumDefinition(String,String)"] = Verb.Closed(new[] { "enum_definition" }),
            ["FunctionBlockDefinitionBuilder.Program(String)"] = Verb.Closed(new[] { "program_simple", "events", "actions" }),
            ["FunctionBlockDefinitionBuilder.RawChild(ProjectElement)"] = Verb.Dynamic,
            ["FunctionBlockDefinitionBuilder.Validate()"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.Build()"] = Verb.NonEmitting,

            // ---- FbResourceDefBuilder (attribute setters only) ----
            ["FbResourceDefBuilder.Note(String)"] = Verb.NonEmitting,
            ["FbResourceDefBuilder.Documentation(String)"] = Verb.NonEmitting,   // help metadata on the resource — never serialized, emits nothing
            ["FbResourceDefBuilder.Backup(Boolean)"] = Verb.NonEmitting,
            ["FbResourceDefBuilder.Icon(String)"] = Verb.NonEmitting,
            ["FbResourceDefBuilder.Inivalue(String)"] = Verb.NonEmitting,
            ["FbResourceDefBuilder.Enum(FbEnumDefRef,String)"] = Verb.NonEmitting,
            ["FbResourceDefBuilder.Enum(String,String)"] = Verb.NonEmitting,
            ["FbResourceDefBuilder.TimerHms(Int32,Int32,Int32,Int32)"] = Verb.NonEmitting,
            ["FbResourceDefBuilder.DateYmd(Int32,Int32,Int32)"] = Verb.NonEmitting,
            ["FbResourceDefBuilder.Attribute(String,String)"] = Verb.NonEmitting,

            // ---- FbProgramBuilder ----
            ["FbProgramBuilder.EventsNote(String)"] = Verb.NonEmitting,
            ["FbProgramBuilder.ActionsNote(String)"] = Verb.NonEmitting,
            ["FbProgramBuilder.Note(String)"] = Verb.NonEmitting,
            ["FbProgramBuilder.EventsName(String)"] = Verb.NonEmitting,
            ["FbProgramBuilder.ActionsName(String)"] = Verb.NonEmitting,
            ["FbProgramBuilder.AddPowerEvent(String,String)"] = Verb.Closed(new[] { "event_power" }),
            ["FbProgramBuilder.AddEvent(String,FbResourceHandle,String,FbResourceHandle,String)"] = Verb.Closed(new[] { "event" }),
            ["FbProgramBuilder.AddEvent(String,FbResourceHandle,String,FbOperand,String)"] = Verb.Closed(new[] { "event" }),
            ["FbProgramBuilder.AddAction(String,FbResourceHandle,String,FbResourceHandle,String)"] = Verb.Closed(new[] { "action" }),
            ["FbProgramBuilder.AddAction(String,FbResourceHandle,String,FbOperand,String)"] = Verb.Closed(new[] { "action" }),
            ["FbProgramBuilder.AddSubProgram(String)"] = Verb.Closed(new[] { "program_sub", "conditions", "actions" }),
            ["FbProgramBuilder.AddCase(String,FbResourceHandle,String)"] = Verb.Closed(new[] { "program_case", "actions" }),

            // ---- FbSubProgramRef ----
            ["FbSubProgramRef.Note(String)"] = Verb.NonEmitting,
            ["FbSubProgramRef.ConditionsNote(String)"] = Verb.NonEmitting,
            ["FbSubProgramRef.OrConditions()"] = Verb.NonEmitting,
            ["FbSubProgramRef.AddCondition(String,FbResourceHandle,String,FbResourceHandle,String)"] = Verb.Closed(new[] { "condition" }),
            ["FbSubProgramRef.AddCondition(String,FbResourceHandle,String,FbOperand,String)"] = Verb.Closed(new[] { "condition" }),

            // ---- FbConditionsGroupRef ----
            ["FbConditionsGroupRef.Name(String)"] = Verb.NonEmitting,
            ["FbConditionsGroupRef.Note(String)"] = Verb.NonEmitting,
            ["FbConditionsGroupRef.OrConditions()"] = Verb.NonEmitting,
            ["FbConditionsGroupRef.AddCondition(String,FbResourceHandle,String,FbResourceHandle,String)"] = Verb.Closed(new[] { "condition" }),
            ["FbConditionsGroupRef.AddCondition(String,FbResourceHandle,String,FbOperand,String)"] = Verb.Closed(new[] { "condition" }),
            ["FbConditionsGroupRef.AddConditionGroup()"] = Verb.Closed(new[] { "conditions" }),

            // ---- FbBranchRef ----
            ["FbBranchRef.Name(String)"] = Verb.NonEmitting,
            ["FbBranchRef.Note(String)"] = Verb.NonEmitting,
            ["FbBranchRef.AddAction(String,FbResourceHandle,String,FbResourceHandle,String)"] = Verb.Closed(new[] { "action" }),
            ["FbBranchRef.AddAction(String,FbResourceHandle,String,FbOperand,String)"] = Verb.Closed(new[] { "action" }),
            ["FbBranchRef.AddSubProgram(String)"] = Verb.Closed(new[] { "program_sub", "conditions", "actions" }),
            ["FbBranchRef.AddCase(String,FbResourceHandle,String)"] = Verb.Closed(new[] { "program_case", "actions" }),

            // ---- FbCaseRef ----
            ["FbCaseRef.Case(String,FbEnumDefRef,String,String)"] = Verb.Closed(new[] { "case_action", "resource_enum" }),
            ["FbCaseRef.Case(String,FbOperand,String)"] = Verb.Closed(new[] { "case_action" }),
            ["FbCaseRef.Default()"] = Verb.NonEmitting,

            // ---- FbConditionRef ----
            ["FbConditionRef.AddEnumOperand(String,FbEnumDefRef,String)"] = Verb.Closed(new[] { "resource_enum" }),
            ["FbConditionRef.AddEnumOperand(String,String,String)"] = Verb.Closed(new[] { "resource_enum" }),

            // ---- FbEnumDefRef ----
            ["FbEnumDefRef.AddValue(String)"] = Verb.Closed(new[] { "enum_value" }),
            ["FbEnumDefRef.AddValue(String,Int32)"] = Verb.Closed(new[] { "enum_value" }),
            ["FbEnumDefRef.AddValue(String,Int32,String)"] = Verb.Closed(new[] { "enum_value" }),
            ["FbEnumDefRef.InitialValue(String)"] = Verb.NonEmitting,

            // ---- FbOperand ----
            ["FbOperand.Literal(String,String,Action`1)"] = Verb.Dynamic,
            ["FbOperand.Enum(FbEnumDefRef,String,String,String)"] = Verb.Closed(new[] { "resource_enum" }),
            ["FbOperand.EnumRaw(String,String,String,String)"] = Verb.Closed(new[] { "resource_enum" }),
        };

        private static readonly Dictionary<string, CatalogGrammar> Presets = new(StringComparer.Ordinal)
        {
            ["Dataline"] = CatalogGrammarPresets.Dataline,
            ["Airlink"] = CatalogGrammarPresets.Airlink,
            ["Rs485LedDimmer"] = CatalogGrammarPresets.Rs485LedDimmer,
            ["Rs485SmsModem"] = CatalogGrammarPresets.Rs485SmsModem,
            ["S0Device"] = CatalogGrammarPresets.S0Device,
            ["FunctionBlock"] = CatalogGrammarPresets.FunctionBlock,
        };

        private static readonly string[] NonVerbNames =
            { "ToString", "Equals", "GetHashCode", "GetType", "Deconstruct", "PrintMembers", "<Clone>$" };

        private static IEnumerable<string> DiscoverVerbs() =>
            SurfaceTypes.SelectMany(type => type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && !NonVerbNames.Contains(m.Name) && !m.Name.StartsWith("op_", StringComparison.Ordinal))
                .Select(m => $"{type.Name}.{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})"));

        [Test]
        public void EveryPublicBuilderVerb_IsClassified()
        {
            string[] unclassified = DiscoverVerbs().Where(key => !Classification.ContainsKey(key))
                .OrderBy(k => k, StringComparer.Ordinal).ToArray();

            Assert.That(unclassified, Is.Empty,
                "New public builder verb(s) without a grammar classification. Decide for each: does it emit a " +
                "fixed element type (closed — add its tags AND extend the family preset), a caller-supplied type " +
                "(dynamic-raw), or nothing? Then classify it in this test's reviewed map:\n  " +
                string.Join("\n  ", unclassified));
        }

        [Test]
        public void ClassifiedVerbs_AllExist_SoTheMapCannotGoStale()
        {
            var discovered = new HashSet<string>(DiscoverVerbs(), StringComparer.Ordinal);
            string[] stale = Classification.Keys.Where(key => !discovered.Contains(key))
                .OrderBy(k => k, StringComparer.Ordinal).ToArray();

            Assert.That(stale, Is.Empty, "Classified verb(s) no longer exist — prune the map:\n  " +
                string.Join("\n  ", stale));
        }

        [Test]
        public void EveryClosedEmitterTag_IsDeclaredByItsFamilyPresets()
        {
            var violations = new List<string>();
            foreach ((string verb, Verb entry) in Classification.Where(e => e.Value.Kind == "closed"))
            {
                foreach (string family in entry.Families)
                {
                    CatalogGrammar preset = Presets[family];
                    foreach (string tag in entry.Tags.Where(tag => preset.TryGetDeclaration(tag) is null))
                    {
                        violations.Add($"{verb} emits <{tag}> but the {family} preset does not declare it");
                    }
                }
            }
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        // ---- (2) .ExtendGrammar keeps the preset intact ----

        [Test]
        public void ExtendGrammar_AddsDeclaration_WithoutDisturbingThePreset()
        {
            ProductDefinition product = ProductDefinitionBuilder
                .Dataline("_0x9fe0", "Extension probe")
                .ExtendGrammar(g => g.Element("resource_probe",
                    GrammarAttr.Id("id"), GrammarAttr.Cdata("name", ""), GrammarAttr.IdRef("wired"),
                    GrammarAttr.Cdata("inivalue", "7.5")))
                .AddInput("Tryk")
                .RawChild(new ProjectElement("resource_probe", new ElementId(0x90, 0x05),
                    ImmutableArray.Create(("id", "_0x9005"), ("name", "Probe")), ImmutableArray<ProjectElement>.Empty))
                .Build();

            using var ms = new MemoryStream();
            CatalogFileWriter.Write(product, ms);
            CatalogGrammar written = CatalogDtdParser.ParseStrict(
                CatalogDtdParser.CaptureHeadText(ms.ToArray()));

            Assert.Multiple(() =>
            {
                Assert.That(written.Declarations.Length,
                    Is.EqualTo(CatalogGrammarPresets.Dataline.Declarations.Length + 1),
                    "the preset plus exactly the one added declaration");
                foreach (GrammarDeclaration expected in CatalogGrammarPresets.Dataline.Declarations)
                {
                    Assert.That(written.TryGetDeclaration(expected.Tag), Is.EqualTo(expected),
                        $"the preset's '{expected.Tag}' declaration (types, defaults, IDREF classification) is untouched");
                }
                GrammarDeclaration? added = written.TryGetDeclaration("resource_probe");
                Assert.That(added, Is.Not.Null);
                Assert.That(added!.FindAttr("wired")!.Type, Is.EqualTo(GrammarAttrType.IdRef));
            });
        }

        [Test]
        public void ExtendGrammar_ReplacesOneDeclaration_InPlace()
        {
            ProductDefinition product = ProductDefinitionBuilder
                .Dataline("_0x9fe1", "Replace probe")
                .ExtendGrammar(g => g.Element("dataline_input",
                    GrammarAttr.Id("id"), GrammarAttr.Cdata("name", ""),
                    GrammarAttr.Enumerated("inivalue", new[] { "on", "off" }, "on")))
                .Build();

            Assert.Multiple(() =>
            {
                Assert.That(product.Grammar.Declarations.Length,
                    Is.EqualTo(CatalogGrammarPresets.Dataline.Declarations.Length), "replace, not append");
                Assert.That(product.Grammar.Declarations[1].Tag, Is.EqualTo("dataline_input"),
                    "the replaced declaration keeps its position");
                Assert.That(product.Grammar.TryGetDeclaration("dataline_input")!.FindAttr("inivalue")!.RawLiteral,
                    Is.EqualTo("on"));
            });
        }

        // ---- (3) the advisory categories: non-blocking warnings, Build() and Write still succeed ----

        /// <summary>
        /// One advisory: its body raises that code as a WARNING, and Build and Write still succeed.
        /// <para>
        /// The bodies live on <see cref="DefinitionFindingProbe.GrammarAdvisories"/> rather than here,
        /// because the drift and severity gates provoke exactly the same ones. They are sensitive to the
        /// grammar presets to the byte, so a second copy could stop objecting on one side — passing while
        /// testing nothing — while the other side stayed honest.
        /// </para>
        /// </summary>
        [TestCaseSource(nameof(GrammarAdvisoryCodes))]
        public void Advisory_Warns(string code) => AssertAdvisoryWarns(code);

        /// <summary>
        /// The advisory codes, READ from the probe rather than re-listed. A further advisory added there is
        /// covered here the moment it exists; a hand-kept list of stubs would have left it silently untested
        /// until someone remembered to write another method.
        /// </summary>
        private static IEnumerable<string> GrammarAdvisoryCodes =>
            DefinitionFindingProbe.GrammarAdvisories.Select(a => a.Code);

        private static void AssertAdvisoryWarns(string code)
        {
            ProductDefinitionBuilder builder =
                DefinitionFindingProbe.GrammarAdvisories.Single(a => a.Code == code).Body();
            ProjectValidationResult validation = builder.Validate();

            using var ms = new MemoryStream();
            CatalogFileWriter.Write(builder.Build(), ms);

            Assert.Multiple(() =>
            {
                Assert.That(ms.Length, Is.GreaterThan(0), "advisories never block Build or Write");
                Assert.That(validation.IsValid, Is.True, "advisories are warnings, not errors");
                Assert.That(validation.Findings.Where(f => f.RuleId == code), Is.Not.Empty,
                    $"expected a '{code}' warning; got: " +
                    string.Join("; ", validation.Findings.Select(f => f.RuleId)));
            });
        }

        /// <summary>
        /// A bound the GRAMMAR defaults, which no element carries and every element of the tag inherits.
        ///
        /// <para>The advisory read only the attributes an element physically carries, while
        /// <c>ElementView.DeclaredBounds</c> — the reader whose answer decides whether the composed dialog
        /// offers the field — reads the EFFECTIVE value and so falls back to the declared default. A definition
        /// defaulting <c>minimum</c> to something unreadable therefore made every placed instance's field
        /// read-only with no finding to say why, which is the same silence this row was minted to end, one
        /// level up.</para>
        /// </summary>
        [Test]
        public void Advisory_UnreadableBoundDeclaredOnlyAsAGrammarDefault_IsStillWarned()
        {
            ProductDefinitionBuilder builder = ProductDefinitionBuilder
                .Dataline("_0x9fe6", "Bound default probe")
                .ExtendGrammar(g => g.Element("resource_probe",
                    GrammarAttr.Id("id"), GrammarAttr.Cdata("name", ""), GrammarAttr.Cdata("minimum", "x")))
                .RawChild(new ProjectElement("resource_probe", new ElementId(0x90, 0x06),
                    ImmutableArray.Create(("id", "_0x9006"), ("name", "Probe")),
                    ImmutableArray<ProjectElement>.Empty));

            ProjectValidationResult validation = builder.Validate();

            Assert.That(validation.Findings.Where(f => f.RuleId == "catalog-bound-unreadable")
                    .Select(f => f.Locator),
                Does.Contain("resource_probe@minimum"),
                "the element carries no minimum of its own, so only the declared default states the bound — and "
                + "it is the one every instance of the tag reads");
        }

        /// <summary>A grammar default that DOES parse states a bound the engine can honour, and is not this
        /// row — the control without which the case above would pass on any default at all.</summary>
        [Test]
        public void Advisory_ReadableBoundDeclaredAsAGrammarDefault_IsNotWarned()
        {
            ProductDefinitionBuilder builder = ProductDefinitionBuilder
                .Dataline("_0x9fe7", "Bound default control")
                .ExtendGrammar(g => g.Element("resource_probe",
                    GrammarAttr.Id("id"), GrammarAttr.Cdata("name", ""), GrammarAttr.Cdata("minimum", "0")))
                .RawChild(new ProjectElement("resource_probe", new ElementId(0x90, 0x07),
                    ImmutableArray.Create(("id", "_0x9007"), ("name", "Probe")),
                    ImmutableArray<ProjectElement>.Empty));

            Assert.That(builder.Validate().Findings.Where(f => f.RuleId == "catalog-bound-unreadable"), Is.Empty);
        }

        [Test]
        public void Advisory_OrphanDeclaredTag_YieldsNoUndeclaredTypeWarning()
        {
            // The case-skew shape: the body's tag is declared ONLY by an orphan ATTLIST — that counts as declared.
            ProductDefinitionBuilder builder = ProductDefinitionBuilder
                .Dataline("_0x9fe5", "Advisory probe")
                .Grammar(CatalogGrammar.Create(new[]
                {
                    OracleGrammars.DatalineRootLean7, OracleGrammars.SkewElementOnly, OracleGrammars.SkewOrphan,
                }))
                .RawChild(new ProjectElement("resource_skew", new ElementId(0x9, 0x02),
                    ImmutableArray.Create(("id", "_0x902"), ("name", "Skew")), ImmutableArray<ProjectElement>.Empty));

            Assert.That(builder.Validate().Findings.Where(f => f.RuleId == "grammar-undeclared-type"), Is.Empty,
                "an orphan-ATTLIST declaration IS a declaration (ordinal tag match)");
        }

        [Test]
        public void Advisories_AreSkipped_ForAnEmptyGrammar()
        {
            ProductDefinitionBuilder builder = ProductDefinitionBuilder
                .Create("product_dataline", "_0x9fe6", "Open world")
                .AddInput("Tryk");

            Assert.That(builder.Validate().Findings.Where(f => f.RuleId.StartsWith("grammar-", StringComparison.Ordinal)),
                Is.Empty, "an Empty grammar would make every type 'undeclared' — noise, not advice");
        }
    }
}
