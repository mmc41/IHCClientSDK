#nullable enable
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
    /// (3) the six grammar↔body <c>Validate()</c> advisory categories are non-blocking warnings.
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
            // ---- ProductDefinitionBuilder ----
            ["ProductDefinitionBuilder.Dataline(String,String)"] = Verb.Closed(new[] { "product_dataline" }, "Dataline"),
            ["ProductDefinitionBuilder.Airlink(String,String)"] = Verb.Closed(new[] { "product_airlink" }, "Airlink"),
            ["ProductDefinitionBuilder.Rs485LedDimmer(String,String)"] = Verb.Closed(new[] { "product_rs485_led_dimmer" }, "Rs485LedDimmer"),
            ["ProductDefinitionBuilder.Rs485SmsModem(String,String)"] = Verb.Closed(new[] { "product_rs485_sms_modem" }, "Rs485SmsModem"),
            ["ProductDefinitionBuilder.S0Device(String,String)"] = Verb.Closed(new[] { "s0_device" }, "S0Device"),
            ["ProductDefinitionBuilder.Create(String,String,String)"] = Verb.Dynamic,
            ["ProductDefinitionBuilder.From(ProductDefinition)"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.CategoryPath(String)"] = Verb.NonEmitting,
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
            ["ProductDefinitionBuilder.Documentation(String)"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.Documentation(String,String)"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.Attribute(String,String)"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.AddResource(String,String,Action`1)"] = Verb.Dynamic,
            ["ProductDefinitionBuilder.RawChild(ProjectElement)"] = Verb.Dynamic,
            ["ProductDefinitionBuilder.Grammar(CatalogGrammar)"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.ExtendGrammar(Action`1)"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.Validate()"] = Verb.NonEmitting,
            ["ProductDefinitionBuilder.Build()"] = Verb.NonEmitting,

            // ---- ProductResourceDefBuilder (attribute setters only) ----
            ["ProductResourceDefBuilder.Address(String)"] = Verb.NonEmitting,
            ["ProductResourceDefBuilder.CableColour(String)"] = Verb.NonEmitting,
            ["ProductResourceDefBuilder.Note(String)"] = Verb.NonEmitting,
            ["ProductResourceDefBuilder.Backup(Boolean)"] = Verb.NonEmitting,
            ["ProductResourceDefBuilder.Icon(String)"] = Verb.NonEmitting,
            ["ProductResourceDefBuilder.Attribute(String,String)"] = Verb.NonEmitting,

            // ---- FunctionBlockDefinitionBuilder ----
            // Create/Build always emit the block root and its five fixed containers.
            ["FunctionBlockDefinitionBuilder.Create(String,String,String)"] = Verb.Closed(new[]
                { "functionblock", "inputs", "outputs", "settings", "internalsettings", "programs" }),
            ["FunctionBlockDefinitionBuilder.From(FunctionBlockDefinition)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.DisplayName(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.CategoryPath(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.MasterProgrammer(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.MasterDate(DateOnly)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.VendorMaster(Boolean)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.Locked(Boolean)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.Note(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.Attribute(String,String)"] = Verb.NonEmitting,
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
            ["FunctionBlockDefinitionBuilder.AddInput(String,String,Action`1)"] = Verb.Dynamic,
            ["FunctionBlockDefinitionBuilder.AddOutput(String)"] = Verb.Closed(new[] { "resource_output" }),
            ["FunctionBlockDefinitionBuilder.AddOutput(String,String,Action`1)"] = Verb.Dynamic,
            ["FunctionBlockDefinitionBuilder.AddSetting(String,String,Action`1)"] = Verb.Dynamic,
            ["FunctionBlockDefinitionBuilder.AddInternalVariable(String,String,Action`1)"] = Verb.Dynamic,
            ["FunctionBlockDefinitionBuilder.AddEnumDefinition(String)"] = Verb.Closed(new[] { "enum_definition" }),
            ["FunctionBlockDefinitionBuilder.AddEnumDefinition(String,String)"] = Verb.Closed(new[] { "enum_definition" }),
            ["FunctionBlockDefinitionBuilder.Program(String)"] = Verb.Closed(new[] { "program_simple", "events", "actions" }),
            ["FunctionBlockDefinitionBuilder.Documentation(String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.Documentation(FbResourceHandle,String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.Documentation(String,String)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.RawResource(String,ProjectElement)"] = Verb.Dynamic,
            ["FunctionBlockDefinitionBuilder.RawChild(ProjectElement)"] = Verb.Dynamic,
            ["FunctionBlockDefinitionBuilder.Grammar(CatalogGrammar)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.ExtendGrammar(Action`1)"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.Validate()"] = Verb.NonEmitting,
            ["FunctionBlockDefinitionBuilder.Build()"] = Verb.NonEmitting,

            // ---- FbResourceDefBuilder (attribute setters only) ----
            ["FbResourceDefBuilder.Note(String)"] = Verb.NonEmitting,
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

        // ---- (3) the six advisory categories: non-blocking warnings, Build() and Write still succeed ----

        private static (ProjectValidationResult Validation, ProductDefinition Definition) BuildAndValidate(
            Func<ProductDefinitionBuilder, ProductDefinitionBuilder> configure)
        {
            ProductDefinitionBuilder builder = configure(ProductDefinitionBuilder.Dataline("_0x9fe2", "Advisory probe"));
            ProjectValidationResult validation = builder.Validate();
            ProductDefinition definition = builder.Build();
            using var ms = new MemoryStream();
            CatalogFileWriter.Write(definition, ms);
            Assert.That(ms.Length, Is.GreaterThan(0), "advisories never block Build or Write");
            return (validation, definition);
        }

        private static void AssertWarns(ProjectValidationResult validation, string category)
        {
            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True, "advisories are warnings, not errors");
                Assert.That(validation.Findings.Where(f => f.RuleId == category), Is.Not.Empty,
                    $"expected a '{category}' warning; got: " +
                    string.Join("; ", validation.Findings.Select(f => f.RuleId)));
            });
        }

        [Test]
        public void Advisory_GrammarUndeclaredType_Warns()
        {
            (ProjectValidationResult validation, _) = BuildAndValidate(b => b.RawChild(
                new ProjectElement("resource_mystery", new ElementId(0x90, 0x06),
                    ImmutableArray.Create(("id", "_0x9006"), ("name", "?")), ImmutableArray<ProjectElement>.Empty)));

            AssertWarns(validation, "grammar-undeclared-type");
        }

        [Test]
        public void Advisory_GrammarUndeclaredAttribute_Warns()
        {
            (ProjectValidationResult validation, _) = BuildAndValidate(b =>
                b.AddInput("Tryk", i => i.Attribute("mystery_attr", "x")));

            AssertWarns(validation, "grammar-undeclared-attribute");
        }

        [Test]
        public void Advisory_GrammarMissingRequired_Warns()
        {
            // The airlink relay declares address_channel #REQUIRED; splice one without it.
            ProductDefinitionBuilder builder = ProductDefinitionBuilder
                .Airlink("_0x9fe3", "Advisory probe")
                .Attribute("device_type", "_0x0804")
                .RawChild(new ProjectElement("airlink_relay", new ElementId(0x90, 0x07),
                    ImmutableArray.Create(("id", "_0x9007"), ("name", "Relay")), ImmutableArray<ProjectElement>.Empty));

            AssertWarns(builder.Validate(), "grammar-missing-required");
        }

        [Test]
        public void Advisory_GrammarEnumValue_Warns()
        {
            // The authentic S0 kWh vendor bug: accessibility="readwrite" is outside (read | write | read-write).
            ProductDefinitionBuilder builder = ProductDefinitionBuilder
                .S0Device("_0x9fe4", "Advisory probe")
                .AddResource("kWh", "Energi", r => r.Attribute("accessibility", "readwrite"));

            AssertWarns(builder.Validate(), "grammar-enum-value");
        }

        [Test]
        public void Advisory_GrammarDuplicateId_Warns()
        {
            (ProjectValidationResult validation, _) = BuildAndValidate(b => b
                .RawChild(new ProjectElement("dataline_input", new ElementId(0x9, 0x11),
                    ImmutableArray.Create(("id", "_0x911"), ("name", "A")), ImmutableArray<ProjectElement>.Empty))
                .RawChild(new ProjectElement("dataline_input", new ElementId(0x9, 0x11),
                    ImmutableArray.Create(("id", "_0x911"), ("name", "B")), ImmutableArray<ProjectElement>.Empty)));

            AssertWarns(validation, "grammar-duplicate-id");
        }

        [Test]
        public void Advisory_GrammarDanglingIdRef_Warns()
        {
            (ProjectValidationResult validation, _) = BuildAndValidate(b => b
                .AddOutput("Udgang")
                .RawChild(new ProjectElement("scenes", new ElementId(0x9, 0x27),
                    ImmutableArray.Create(("id", "_0x927"), ("name", "S"), ("scene_resource", "_0xdead")),
                    ImmutableArray<ProjectElement>.Empty)));

            AssertWarns(validation, "grammar-dangling-idref");
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
