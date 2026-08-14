#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Schema;

namespace Ihc.Vis.CatalogCodegen
{
    /// <summary>
    /// Reverses a parsed function-block body (a <c>FunctionBlocks\*.ifb</c> tree as <c>CatalogReader</c> yields it) into
    /// a <see cref="FunctionBlockRecipe"/> of <see cref="FunctionBlockDefinitionBuilder"/> / <see cref="FbProgramBuilder"/>
    /// calls — the inverse of "a builder is a code-authored CatalogReader". It lowers the master identity, the four
    /// resource containers, embedded enum definitions, and the whole program graph (events, root actions,
    /// <c>program_sub</c> conditions/branches, and <c>program_case</c> switches) into a statement-bodied factory. IDREF
    /// tokens (<c>link1</c>/<c>link2</c>/<c>variable</c>/<c>value</c>/<c>typedef</c>/<c>inivalue</c>) are resolved to the
    /// <c>var</c> that declares the referenced resource/enum, so the builder re-mints ids and re-wires the references on
    /// its own.
    /// </summary>
    /// <remarks>
    /// Resource attributes are emitted verbatim in file order — the reader yields the raw body, so the recipe replays
    /// every attribute the file wrote (the same raw-body model as <see cref="ProductDecompiler"/>). Structural
    /// decorations (container notes, sub-program/branch names) are emitted only when they differ from the builder's
    /// fixed vendor grammar (<see cref="FbGrammar"/>), so a block that already matches the synthetic defaults renders
    /// leanly.
    /// </remarks>
    internal sealed class FunctionBlockDecompiler
    {
        private readonly ProjectSchemaView grammar;
        private readonly FunctionBlockRecipe recipe;

        // Source id token -> the C# var that declares the referenced element, for IDREF resolution.
        private readonly Dictionary<string, string> resourceVarById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> enumVarByDefId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> valueNameByValueId = new(StringComparer.Ordinal);

        private int resourceCount;
        private int enumCount;
        private int programCount;
        private int subCount;
        private int caseCount;
        private int caseBranchCount;
        private int defaultBranchCount;
        private int condGroupCount;

        private FunctionBlockDecompiler(FunctionBlockDefinition def, ImmutableDictionary<string, string> blocks)
        {
            grammar = ProjectSchemaView.For(blocks);
            recipe = new FunctionBlockRecipe(def.MasterType, def.MasterVersion, def.MasterName);
        }

        /// <summary>Decompiles <paramref name="def"/> (parsed against <paramref name="blocks"/>, the file's inline DTD)
        /// into a builder recipe, optionally baking <paramref name="documentation"/> (parsed from the sibling
        /// <c>syn_en*.md</c>) as programmatic-lookup-only <c>.Documentation(..)</c> calls. Throws
        /// <see cref="DecompileNotSupportedException"/> for a construct this stage cannot reverse.</summary>
        public static FunctionBlockRecipe Decompile(FunctionBlockDefinition def, ImmutableDictionary<string, string> blocks,
            DefinitionDocumentation? documentation = null)
        {
            ArgumentNullException.ThrowIfNull(def);
            if (def.MasterName.Length == 0)
            {
                // A block with no master identity at all is the empty "Tom blok" template (the code peer of
                // Data\fb.def, authored via AsEmptyTemplate) — a template, not a catalog component, so this stage
                // does not reverse it. Every corpus .ifb carries at least a master_name (AutoProof is keyless but
                // named); only template-shaped files (e.g. the synthetic empty oracle) reach here.
                throw new DecompileNotSupportedException(
                    "block has no master_name — an empty 'Tom blok' template, not a catalog component.");
            }
            return new FunctionBlockDecompiler(def, blocks).Run(def, documentation);
        }

        private FunctionBlockRecipe Run(FunctionBlockDefinition def, DefinitionDocumentation? documentation)
        {
            ProjectElement body = def.Body;
            EmitHead(body, def);
            EmitDocumentation(documentation);
            foreach (ProjectElement child in body.Children)
            {
                if (child.Tag == "enum_definition")
                {
                    EmitEnumDefinition(child);
                }
            }
            EmitContainer(body, "inputs", "AddInput");
            EmitContainer(body, "outputs", "AddOutput");
            EmitContainer(body, "settings", "AddSetting");
            EmitContainer(body, "internalsettings", "AddInternalVariable");
            ProjectElement? programs = body.FindChild("programs");
            if (programs is not null)
            {
                foreach (ProjectElement program in programs.Children)
                {
                    RequireTag(program, "program_simple");
                    EmitProgram(program);
                }
            }
            return recipe;
        }

        // ---- block-level head (identity + container notes) ----

        private void EmitHead(ProjectElement body, FunctionBlockDefinition def)
        {
            // The body is the RAW file root. Emit EVERY attribute the file wrote, in file order (the block runs
            // .SuppressResourceDefaults(), so the builder keeps the emitted order verbatim rather than canonicalizing).
            // master_type/version/name come from Create as the definition's identity fields, but are ALSO written as
            // body attributes here (their file position is not constant across the corpus), as is name — the builder's
            // composed default is only used for the DisplayName record field, set separately below.
            foreach ((string name, string value) in body.Attrs)
            {
                switch (name)
                {
                    case "id":
                        break;
                    case "master_schneider_electric" when value is "yes" or "no":
                        Head(b => b.VendorMaster(value == "yes"), value == "yes" ? ".VendorMaster()" : ".VendorMaster(false)");
                        break;
                    case "master_programmer":
                        Head(b => b.MasterProgrammer(value), $".MasterProgrammer({CSharpLiteral.Quote(value)})");
                        break;
                    case "locked" when value is "yes" or "no":
                        Head(b => b.Locked(value == "yes"), value == "yes" ? ".Locked()" : ".Locked(false)");
                        break;
                    case "note":
                        Head(b => b.Note(value), $".Note({CSharpLiteral.Quote(value)})");
                        break;
                    default:
                        Head(b => b.Attribute(name, value),
                            $".Attribute({CSharpLiteral.Quote(name)}, {CSharpLiteral.Quote(value)})");
                        break;
                }
            }

            // DisplayName override only when the vendor name attribute differs from the builder's composed default.
            if (def.DisplayName != FbGrammar.ComposeDisplayName(def.MasterType, def.MasterVersion, def.MasterName))
            {
                string displayName = def.DisplayName;
                Head(b => b.DisplayName(displayName), $".DisplayName({CSharpLiteral.Quote(displayName)})");
            }

            if (def.CategoryPath.Length > 0)
            {
                string categoryPath = def.CategoryPath;
                Head(b => b.CategoryPath(categoryPath), $".CategoryPath({CSharpLiteral.Quote(categoryPath)})");
            }

            EmitContainerDecoration(body, "inputs",
                FbGrammar.InputsName, "InputsName", (b, v) => b.InputsName(v),
                FbGrammar.InputsNoteDefault, "InputsNote", (b, v) => b.InputsNote(v));
            EmitContainerDecoration(body, "outputs",
                FbGrammar.OutputsName, "OutputsName", (b, v) => b.OutputsName(v),
                FbGrammar.OutputsNoteDefault, "OutputsNote", (b, v) => b.OutputsNote(v));
            EmitContainerDecoration(body, "settings",
                FbGrammar.SettingsName, "SettingsName", (b, v) => b.SettingsName(v),
                FbGrammar.SettingsNote, "SettingsNote", (b, v) => b.SettingsNote(v));
            EmitContainerDecoration(body, "internalsettings",
                FbGrammar.InternalName, "InternalVariablesName", (b, v) => b.InternalVariablesName(v),
                FbGrammar.InternalNote, "InternalVariablesNote", (b, v) => b.InternalVariablesNote(v));
            EmitContainerDecoration(body, "programs",
                FbGrammar.ProgramsName, "ProgramsName", (b, v) => b.ProgramsName(v),
                FbGrammar.ProgramsNote, "ProgramsNote", (b, v) => b.ProgramsNote(v));
        }

        // Bakes the block's syn_en documentation as programmatic-lookup-only head calls (out-of-Body, so it does not
        // affect self-verify), through the shared lowering rule the product decompiler uses too.
        private void EmitDocumentation(DefinitionDocumentation? documentation)
        {
            foreach ((Action<FunctionBlockDefinitionBuilder> apply, string render) in
                     DefinitionDocumentationCalls.For<FunctionBlockDefinitionBuilder>(documentation))
            {
                Head(apply, render);
            }
        }

        // Emits the .{Name}/{Note}(..) override for a body container only when its vendor name/note differs from the
        // builder default. The apply delegate is passed directly (as AddProgramOverride does), so the method string is
        // used only to render the call — no second name→delegate mapping to keep in sync.
        private void EmitContainerDecoration(ProjectElement body, string container,
            string nameDefault, string nameMethod, Action<FunctionBlockDefinitionBuilder, string> applyName,
            string noteDefault, string noteMethod, Action<FunctionBlockDefinitionBuilder, string> applyNote)
        {
            ProjectElement? element = body.FindChild(container);
            if (element is null)
            {
                return;
            }
            string name = element.GetAttribute("name") ?? string.Empty;
            if (name != nameDefault)
            {
                Head(b => applyName(b, name), $".{nameMethod}({CSharpLiteral.Quote(name)})");
            }
            string note = element.GetAttribute("note") ?? string.Empty;
            if (note != noteDefault)
            {
                Head(b => applyNote(b, note), $".{noteMethod}({CSharpLiteral.Quote(note)})");
            }
        }

        // ---- enum definitions ----

        private void EmitEnumDefinition(ProjectElement enumDef)
        {
            string varName = $"g{enumCount++}";
            string name = enumDef.GetAttribute("name") ?? string.Empty;
            string? typeid = NonDefault(enumDef, "typeid");
            if (enumDef.GetAttribute("id") is { } defId)
            {
                enumVarByDefId[defId] = varName;
            }

            var values = new List<(string Name, int Index, string? Typeid)>();
            foreach (ProjectElement value in enumDef.Children)
            {
                RequireTag(value, "enum_value");
                string valueName = value.GetAttribute("name") ?? string.Empty;
                int index = int.TryParse(value.GetAttribute("index"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
                string? valueTypeid = NonDefault(value, "typeid");
                values.Add((valueName, index, valueTypeid));
                if (value.GetAttribute("id") is { } valueId)
                {
                    valueNameByValueId[valueId] = valueName;
                }
            }

            var render = new StringBuilder($"var {varName} = b.AddEnumDefinition({CSharpLiteral.Quote(name)}");
            render.Append(typeid is null ? ")" : $", {CSharpLiteral.Quote(typeid)})");
            foreach ((string Name, int Index, string? Typeid) value in values)
            {
                render.Append($".AddValue({CSharpLiteral.Quote(value.Name)}, {value.Index}");
                render.Append(value.Typeid is null ? ")" : $", {CSharpLiteral.Quote(value.Typeid)})");
            }
            render.Append(';');

            recipe.Statements.Add(new FbStatement(env =>
            {
                FbEnumDefRef reference = env.Builder.AddEnumDefinition(name, typeid);
                foreach ((string Name, int Index, string? Typeid) value in values)
                {
                    reference.AddValue(value.Name, value.Index, value.Typeid);
                }
                env.Set(varName, reference);
            }, render.ToString()));
        }

        // ---- resource containers ----

        private void EmitContainer(ProjectElement body, string container, string addMethod)
        {
            ProjectElement? element = body.FindChild(container);
            if (element is null)
            {
                return;
            }
            foreach (ProjectElement resource in element.Children)
            {
                EmitResource(container, addMethod, resource);
            }
        }

        private void EmitResource(string container, string addMethod, ProjectElement resource)
        {
            string varName = $"r{resourceCount++}";
            string tag = resource.Tag;
            string name = resource.GetAttribute("name") ?? string.Empty;
            if (resource.GetAttribute("id") is { } id)
            {
                resourceVarById[id] = varName;
            }

            List<ResourceConfigItem> configs = BuildResourceConfig(resource);
            string configRender = string.Concat(configs.Select(c => c.Render));
            string head = $"var {varName} = b.{addMethod}({CSharpLiteral.Quote(tag)}, {CSharpLiteral.Quote(name)}";
            string render = configs.Count == 0
                ? $"{head});"
                : $"{head}, r => r{configRender});";

            recipe.Statements.Add(new FbStatement(env =>
            {
                Action<FbResourceDefBuilder>? configure = configs.Count == 0
                    ? null
                    : r =>
                    {
                        foreach (ResourceConfigItem config in configs)
                        {
                            config.Apply(r, env);
                        }
                    };
                FbResourceHandle handle = addMethod switch
                {
                    "AddInput" => env.Builder.AddInput(tag, name, configure),
                    "AddOutput" => env.Builder.AddOutput(tag, name, configure),
                    "AddSetting" => env.Builder.AddSetting(tag, name, configure),
                    "AddInternalVariable" => env.Builder.AddInternalVariable(tag, name, configure),
                    _ => throw new InvalidOperationException($"Unknown resource add method '{addMethod}'."),
                };
                env.Set(varName, handle);
            }, render));
        }

        private List<ResourceConfigItem> BuildResourceConfig(ProjectElement resource)
        {
            var configs = new List<ResourceConfigItem>();
            bool isEnum = resource.Tag == "resource_enum";
            if (isEnum)
            {
                _ = resource.GetAttribute("typedef")
                    ?? throw new DecompileNotSupportedException("resource_enum without typedef.");
                _ = resource.GetAttribute("inivalue")
                    ?? throw new DecompileNotSupportedException("resource_enum without inivalue.");
            }

            foreach ((string name, string value) in resource.Attrs)
            {
                if (name is "id" or "name")
                {
                    continue;
                }
                if (isEnum && name == "typedef")
                {
                    // A resource_enum's typedef+inivalue are wired together through the enum handle (by value name),
                    // not emitted as raw tokens (the builder re-mints the enum's ids, so raw source tokens would not
                    // resolve). The Enum() call sits HERE, at the source typedef position, because the builder appends
                    // typedef then inivalue at the call site — replayed attribute order must match the file (an icon
                    // may legitimately precede the pair).
                    (string enumVar, string valueName) = ResolveEnumReference(value, resource.GetAttribute("inivalue")!);
                    configs.Add(new ResourceConfigItem(
                        (r, env) => r.Enum(env.Get<FbEnumDefRef>(enumVar), valueName),
                        $".Enum({enumVar}, {CSharpLiteral.Quote(valueName)})"));
                    continue;
                }
                if (isEnum && name == "inivalue")
                {
                    continue;   // emitted together with typedef by the Enum() call above
                }
                configs.Add(ResourceConfigCall(name, value));
            }
            return configs;
        }

        private static ResourceConfigItem ResourceConfigCall(string name, string value)
        {
            switch (name)
            {
                case "note":
                    return new ResourceConfigItem((r, _) => r.Note(value), $".Note({CSharpLiteral.Quote(value)})");
                case "icon":
                    return new ResourceConfigItem((r, _) => r.Icon(value), $".Icon({CSharpLiteral.Quote(value)})");
                case "inivalue":
                    return new ResourceConfigItem((r, _) => r.Inivalue(value), $".Inivalue({CSharpLiteral.Quote(value)})");
                case "backup" when value is "yes" or "no":
                    return value == "yes"
                        ? new ResourceConfigItem((r, _) => r.Backup(), ".Backup()")
                        : new ResourceConfigItem((r, _) => r.Backup(false), ".Backup(false)");
                default:
                    return new ResourceConfigItem((r, _) => r.Attribute(name, value),
                        $".Attribute({CSharpLiteral.Quote(name)}, {CSharpLiteral.Quote(value)})");
            }
        }

        // ---- programs ----

        private void EmitProgram(ProjectElement program)
        {
            string varName = $"p{programCount++}";
            string name = program.GetAttribute("name") ?? string.Empty;
            ProjectElement events = program.FindChild("events")
                ?? throw new DecompileNotSupportedException("program_simple without events.");
            ProjectElement actions = program.FindChild("actions")
                ?? throw new DecompileNotSupportedException("program_simple without actions.");

            var overrides = new List<(string Render, Action<FbProgramBuilder> Apply)>();
            AddProgramOverride(overrides, program.GetAttribute("note"), string.Empty,
                "Note", (pb, v) => pb.Note(v));
            AddProgramOverride(overrides, events.GetAttribute("name"), FbGrammar.EventsName,
                "EventsName", (pb, v) => pb.EventsName(v));
            AddProgramOverride(overrides, events.GetAttribute("note"), FbGrammar.EventsNote,
                "EventsNote", (pb, v) => pb.EventsNote(v));
            AddProgramOverride(overrides, actions.GetAttribute("name"), FbGrammar.RootActionsName,
                "ActionsName", (pb, v) => pb.ActionsName(v));
            AddProgramOverride(overrides, actions.GetAttribute("note"), FbGrammar.RootActionsNote,
                "ActionsNote", (pb, v) => pb.ActionsNote(v));

            string render = $"var {varName} = b.Program({CSharpLiteral.Quote(name)})"
                + string.Concat(overrides.Select(o => o.Render)) + ";";
            recipe.Statements.Add(new FbStatement(env =>
            {
                FbProgramBuilder builder = env.Builder.Program(name);
                foreach ((string _, Action<FbProgramBuilder> apply) in overrides)
                {
                    apply(builder);
                }
                env.Set(varName, builder);
            }, render));

            foreach (ProjectElement leaf in events.Children)
            {
                EmitEvent(varName, leaf);
            }
            var target = new Target(varName, env => env.Get<FbProgramBuilder>(varName).RootBranch);
            EmitActionNodes(target, actions);
        }

        private static void AddProgramOverride(List<(string Render, Action<FbProgramBuilder> Apply)> overrides,
            string? sourceValue, string builderDefault, string method, Action<FbProgramBuilder, string> apply)
        {
            string value = sourceValue ?? string.Empty;
            if (value != builderDefault)
            {
                overrides.Add(($".{method}({CSharpLiteral.Quote(value)})", pb => apply(pb, value)));
            }
        }

        private void EmitEvent(string programVar, ProjectElement leaf)
        {
            string name = leaf.GetAttribute("name") ?? string.Empty;
            string? note = NoteOf(leaf);
            if (leaf.Tag == "event_power")
            {
                string render = note is null
                    ? $"{programVar}.AddPowerEvent({CSharpLiteral.Quote(name)});"
                    : $"{programVar}.AddPowerEvent({CSharpLiteral.Quote(name)}, note: {CSharpLiteral.Quote(note)});";
                recipe.Statements.Add(new FbStatement(
                    env => env.Get<FbProgramBuilder>(programVar).AddPowerEvent(name, note), render));
                return;
            }
            RequireTag(leaf, "event");
            string method = leaf.GetAttribute("method") ?? "_0x0";
            string link1Var = ResolveResource(leaf.GetAttribute("link1"));
            if (OperandChild(leaf) is { } operandEl)
            {
                (string operandRender, Func<FbBuildEnv, FbOperand> operandFactory) = BuildOperand(operandEl);
                string args = RenderOperandLeafArgs(name, link1Var, method, operandRender, note);
                recipe.Statements.Add(new FbStatement(
                    env => env.Get<FbProgramBuilder>(programVar).AddEvent(
                        name, env.Get<FbResourceHandle>(link1Var), method, operandFactory(env), note),
                    $"{programVar}.AddEvent({args});"));
                return;
            }
            string? link2Var = ResolveOptionalResource(leaf.GetAttribute("link2"));
            string body = RenderLeafArgs(name, link1Var, method, link2Var, note);
            recipe.Statements.Add(new FbStatement(env =>
            {
                FbResourceHandle l1 = env.Get<FbResourceHandle>(link1Var);
                FbResourceHandle? l2 = link2Var is null ? null : env.Get<FbResourceHandle>(link2Var);
                env.Get<FbProgramBuilder>(programVar).AddEvent(name, l1, method, l2, note);
            }, $"{programVar}.AddEvent({body});"));
        }

        private void EmitActionNodes(Target target, ProjectElement actionsContainer)
        {
            foreach (ProjectElement node in actionsContainer.Children)
            {
                DispatchActionNode(target, node);
            }
        }

        // Emits one child of an actions container (or case_action body): a leaf action, a nested program_sub, or a
        // nested program_case switch.
        private void DispatchActionNode(Target target, ProjectElement node)
        {
            switch (node.Tag)
            {
                case "action":
                    EmitAction(target, node);
                    break;
                case "program_sub":
                    EmitSub(target, node);
                    break;
                case "program_case":
                    EmitCase(target, node);
                    break;
                default:
                    throw new DecompileNotSupportedException(
                        $"unexpected node '{node.Tag}' in an actions container.");
            }
        }

        private void EmitAction(Target target, ProjectElement leaf)
        {
            string name = leaf.GetAttribute("name") ?? string.Empty;
            string? note = NoteOf(leaf);
            string method = leaf.GetAttribute("method") ?? "_0x0";
            string link1Var = ResolveResource(leaf.GetAttribute("link1"));
            if (OperandChild(leaf) is { } operandEl)
            {
                (string operandRender, Func<FbBuildEnv, FbOperand> operandFactory) = BuildOperand(operandEl);
                string args = RenderOperandLeafArgs(name, link1Var, method, operandRender, note);
                recipe.Statements.Add(new FbStatement(
                    env => target.Live(env).AddAction(name, env.Get<FbResourceHandle>(link1Var),
                        method, operandFactory(env), note),
                    $"{target.Expr}.AddAction({args});"));
                return;
            }
            string? link2Var = ResolveOptionalResource(leaf.GetAttribute("link2"));
            string body = RenderLeafArgs(name, link1Var, method, link2Var, note);
            recipe.Statements.Add(new FbStatement(env =>
            {
                FbResourceHandle l1 = env.Get<FbResourceHandle>(link1Var);
                FbResourceHandle? l2 = link2Var is null ? null : env.Get<FbResourceHandle>(link2Var);
                target.Live(env).AddAction(name, l1, method, l2, note);
            }, $"{target.Expr}.AddAction({body});"));
        }

        private void EmitSub(Target target, ProjectElement sub)
        {
            string subVar = $"sub{subCount++}";
            string name = sub.GetAttribute("name") ?? string.Empty;
            string? subNote = NoteOf(sub);
            string subRender = name == FbGrammar.SubProgramName
                ? $"var {subVar} = {target.Expr}.AddSubProgram();"
                : $"var {subVar} = {target.Expr}.AddSubProgram({CSharpLiteral.Quote(name)});";
            recipe.Statements.Add(new FbStatement(
                env => env.Set(subVar, target.Live(env).AddSubProgram(name)), subRender));

            if (subNote is not null)
            {
                recipe.Statements.Add(new FbStatement(
                    env => env.Get<FbSubProgramRef>(subVar).Note(subNote),
                    $"{subVar}.Note({CSharpLiteral.Quote(subNote)});"));
            }

            ProjectElement conditions = sub.FindChild("conditions")
                ?? throw new DecompileNotSupportedException("program_sub without conditions.");
            EmitConditionsGroup($"{subVar}.Conditions",
                env => env.Get<FbSubProgramRef>(subVar).Conditions, conditions);

            (ProjectElement trueActions, ProjectElement falseActions) = TrueFalseBranches(sub);
            EmitBranch(subVar, "WhenTrue", trueActions, FbGrammar.TrueActionsName, FbGrammar.TrueActionsNote);
            EmitBranch(subVar, "WhenFalse", falseActions, FbGrammar.FalseActionsName, FbGrammar.FalseActionsNote);
        }

        private void EmitBranch(string subVar, string branch, ProjectElement actions, string defaultName, string defaultNote)
        {
            string branchExpr = $"{subVar}.{branch}";
            string name = actions.GetAttribute("name") ?? string.Empty;
            string note = actions.GetAttribute("note") ?? string.Empty;
            if (name != defaultName)
            {
                recipe.Statements.Add(new FbStatement(
                    env => BranchOf(env, subVar, branch).Name(name), $"{branchExpr}.Name({CSharpLiteral.Quote(name)});"));
            }
            if (note != defaultNote)
            {
                recipe.Statements.Add(new FbStatement(
                    env => BranchOf(env, subVar, branch).Note(note), $"{branchExpr}.Note({CSharpLiteral.Quote(note)});"));
            }
            var target = new Target(branchExpr, env => BranchOf(env, subVar, branch));
            EmitActionNodes(target, actions);
        }

        // Reverses a conditions container (its per-group note/type, its condition leaves and its nested conditions
        // sub-groups) onto the group referenced by groupExpr/groupLive, recursing into sub-groups.
        private void EmitConditionsGroup(string groupExpr, Func<FbBuildEnv, FbConditionsGroupRef> groupLive,
            ProjectElement conditions)
        {
            string groupName = conditions.GetAttribute("name") ?? string.Empty;
            if (groupName != FbGrammar.ConditionsName)
            {
                recipe.Statements.Add(new FbStatement(
                    env => groupLive(env).Name(groupName), $"{groupExpr}.Name({CSharpLiteral.Quote(groupName)});"));
            }
            string note = conditions.GetAttribute("note") ?? string.Empty;
            if (note != FbGrammar.ConditionsNote)
            {
                recipe.Statements.Add(new FbStatement(
                    env => groupLive(env).Note(note), $"{groupExpr}.Note({CSharpLiteral.Quote(note)});"));
            }
            if (conditions.GetAttribute("type") == "or")
            {
                recipe.Statements.Add(new FbStatement(
                    env => groupLive(env).OrConditions(), $"{groupExpr}.OrConditions();"));
            }
            foreach (ProjectElement child in conditions.Children)
            {
                if (child.Tag == "condition")
                {
                    EmitCondition(groupExpr, groupLive, child);
                }
                else if (child.Tag == "conditions")
                {
                    string nestedVar = $"cg{condGroupCount++}";
                    recipe.Statements.Add(new FbStatement(
                        env => env.Set(nestedVar, groupLive(env).AddConditionGroup()),
                        $"var {nestedVar} = {groupExpr}.AddConditionGroup();"));
                    EmitConditionsGroup(nestedVar, env => env.Get<FbConditionsGroupRef>(nestedVar), child);
                }
                else
                {
                    throw new DecompileNotSupportedException($"unexpected node '{child.Tag}' in a conditions group.");
                }
            }
        }

        private void EmitCondition(string groupExpr, Func<FbBuildEnv, FbConditionsGroupRef> groupLive,
            ProjectElement condition)
        {
            RequireTag(condition, "condition");
            string name = condition.GetAttribute("name") ?? string.Empty;
            string method = condition.GetAttribute("method") ?? "_0x0";
            string? note = NoteOf(condition);
            string link1Var = ResolveResource(condition.GetAttribute("link1"));

            if (OperandChild(condition) is { } operandEl)
            {
                (string operandRender, Func<FbBuildEnv, FbOperand> operandFactory) = BuildOperand(operandEl);
                string args = RenderOperandLeafArgs(name, link1Var, method, operandRender, note);
                recipe.Statements.Add(new FbStatement(
                    env => groupLive(env).AddCondition(
                        name, env.Get<FbResourceHandle>(link1Var), method, operandFactory(env), note),
                    $"{groupExpr}.AddCondition({args});"));
                return;
            }

            string? link2Var = ResolveOptionalResource(condition.GetAttribute("link2"));
            string body = RenderLeafArgs(name, link1Var, method, link2Var, note);
            recipe.Statements.Add(new FbStatement(env =>
            {
                FbResourceHandle l1 = env.Get<FbResourceHandle>(link1Var);
                FbResourceHandle? l2 = link2Var is null ? null : env.Get<FbResourceHandle>(link2Var);
                groupLive(env).AddCondition(name, l1, method, l2, note);
            }, $"{groupExpr}.AddCondition({body});"));
        }

        private void EmitCase(Target target, ProjectElement caseElement)
        {
            string caseVar = $"case{caseCount++}";
            string name = caseElement.GetAttribute("name") ?? string.Empty;
            string? note = NoteOf(caseElement);
            string switchVar = ResolveResource(caseElement.GetAttribute("link"));
            string switchArg = note is null
                ? $"{target.Expr}.AddCase({CSharpLiteral.Quote(name)}, {switchVar})"
                : $"{target.Expr}.AddCase({CSharpLiteral.Quote(name)}, {switchVar}, note: {CSharpLiteral.Quote(note)})";
            recipe.Statements.Add(new FbStatement(
                env => env.Set(caseVar, target.Live(env).AddCase(name, env.Get<FbResourceHandle>(switchVar), note)),
                $"var {caseVar} = {switchArg};"));

            ProjectElement? defaultActions = null;
            foreach (ProjectElement child in caseElement.Children)
            {
                if (child.Tag == "case_action")
                {
                    EmitCaseAction(caseVar, child);
                }
                else if (child.Tag == "actions")
                {
                    defaultActions = child;   // the trailing default branch
                }
                else
                {
                    throw new DecompileNotSupportedException($"unexpected node '{child.Tag}' in a program_case.");
                }
            }
            if (defaultActions is not null)
            {
                EmitCaseDefault(caseVar, defaultActions);
            }
        }

        private void EmitCaseAction(string caseVar, ProjectElement caseAction)
        {
            string caseBranchVar = $"cb{caseBranchCount++}";
            string name = caseAction.GetAttribute("name") ?? string.Empty;
            string? note = NoteOf(caseAction);
            ProjectElement operandEl = OperandChild(caseAction)
                ?? throw new DecompileNotSupportedException("case_action without an embedded resource operand.");
            (string operandRender, Func<FbBuildEnv, FbOperand> operandFactory) = BuildOperand(operandEl);
            string caseArgs = note is null
                ? $"{caseVar}.Case({CSharpLiteral.Quote(name)}, {operandRender})"
                : $"{caseVar}.Case({CSharpLiteral.Quote(name)}, {operandRender}, note: {CSharpLiteral.Quote(note)})";
            recipe.Statements.Add(new FbStatement(
                env => env.Set(caseBranchVar, env.Get<FbCaseRef>(caseVar).Case(name, operandFactory(env), note)),
                $"var {caseBranchVar} = {caseArgs};"));

            var target = new Target(caseBranchVar, env => env.Get<FbBranchRef>(caseBranchVar));
            foreach (ProjectElement node in caseAction.Children)
            {
                if (ReferenceEquals(node, operandEl))
                {
                    continue;   // the match operand, handled above
                }
                DispatchActionNode(target, node);
            }
        }

        private void EmitCaseDefault(string caseVar, ProjectElement defaultActions)
        {
            string name = defaultActions.GetAttribute("name") ?? string.Empty;
            string note = defaultActions.GetAttribute("note") ?? string.Empty;
            bool overrideName = name != FbGrammar.DefaultCaseName;
            bool overrideNote = note != FbGrammar.DefaultCaseNote;
            bool hasChildren = !defaultActions.Children.IsEmpty;
            if (!overrideName && !overrideNote && !hasChildren)
            {
                return;   // the builder already emits the standard empty default branch
            }
            string defaultVar = $"def{defaultBranchCount++}";
            recipe.Statements.Add(new FbStatement(
                env => env.Set(defaultVar, env.Get<FbCaseRef>(caseVar).Default()),
                $"var {defaultVar} = {caseVar}.Default();"));
            if (overrideName)
            {
                recipe.Statements.Add(new FbStatement(
                    env => env.Get<FbBranchRef>(defaultVar).Name(name), $"{defaultVar}.Name({CSharpLiteral.Quote(name)});"));
            }
            if (overrideNote)
            {
                recipe.Statements.Add(new FbStatement(
                    env => env.Get<FbBranchRef>(defaultVar).Note(note), $"{defaultVar}.Note({CSharpLiteral.Quote(note)});"));
            }
            var target = new Target(defaultVar, env => env.Get<FbBranchRef>(defaultVar));
            EmitActionNodes(target, defaultActions);
        }

        // ---- shared helpers ----

        // The embedded literal operand of a leaf — the %S constant of a "%P <op> %S" comparison, materialized as a
        // child resource whose id the leaf's link2 (or a case_action's value) targets — or null when the operand is a
        // reference to another resource instead.
        private static ProjectElement? OperandChild(ProjectElement leaf) =>
            leaf.Children.FirstOrDefault(c => c.Tag.StartsWith("resource_", StringComparison.Ordinal));

        // Builds the (rendered FbOperand expression, live factory) for an embedded operand: an enum value wired by the
        // enum handle, or a value-type constant carrying its verbatim attributes.
        private (string Render, Func<FbBuildEnv, FbOperand> Factory) BuildOperand(ProjectElement operand)
        {
            string? operandName = operand.GetAttribute("name");
            if (operand.Tag == "resource_enum")
            {
                (string enumVar, string valueName) = ResolveEnumReference(
                    operand.GetAttribute("typedef") ?? throw new DecompileNotSupportedException("enum operand without typedef."),
                    operand.GetAttribute("inivalue") ?? throw new DecompileNotSupportedException("enum operand without inivalue."));
                string? operandIcon = NonDefault(operand, "icon");
                string render = operandName == "Enumerator" && operandIcon == "_0x22"
                    ? $"FbOperand.Enum({enumVar}, {CSharpLiteral.Quote(valueName)})"
                    : $"FbOperand.Enum({enumVar}, {CSharpLiteral.Quote(valueName)}, name: {NullableQuote(operandName)}, icon: {NullableQuote(operandIcon)})";
                return (render, env => FbOperand.Enum(env.Get<FbEnumDefRef>(enumVar), valueName, operandName, operandIcon));
            }

            List<ResourceConfigItem> configs = BuildResourceConfig(operand);
            string configRender = string.Concat(configs.Select(c => c.Render));
            string tag = operand.Tag;
            string literalRender = configs.Count == 0
                ? $"FbOperand.Literal({CSharpLiteral.Quote(tag)}, {NullableQuote(operandName)})"
                : $"FbOperand.Literal({CSharpLiteral.Quote(tag)}, {NullableQuote(operandName)}, o => o{configRender})";
            return (literalRender, env =>
            {
                Action<FbResourceDefBuilder>? configure = configs.Count == 0
                    ? null
                    : r =>
                    {
                        foreach (ResourceConfigItem config in configs)
                        {
                            config.Apply(r, env);
                        }
                    };
                return FbOperand.Literal(tag, operandName, configure);
            });
        }

        private static string NullableQuote(string? value) => value is null ? "null" : CSharpLiteral.Quote(value);

        private static string RenderOperandLeafArgs(string name, string link1Var, string method, string operandRender, string? note)
        {
            var builder = new StringBuilder();
            builder.Append(CSharpLiteral.Quote(name)).Append(", ").Append(link1Var).Append(", ")
                .Append(CSharpLiteral.Quote(method)).Append(", ").Append(operandRender);
            if (note is not null)
            {
                builder.Append(", note: ").Append(CSharpLiteral.Quote(note));
            }
            return builder.ToString();
        }

        private string ResolveResource(string? idToken) =>
            idToken is not null && resourceVarById.TryGetValue(idToken, out string? var)
                ? var
                : throw new DecompileNotSupportedException($"link '{idToken}' does not resolve to a declared resource.");

        private string? ResolveOptionalResource(string? idToken) =>
            idToken is null ? null : ResolveResource(idToken);

        private (string EnumVar, string ValueName) ResolveEnumReference(string typedef, string inivalue)
        {
            if (!enumVarByDefId.TryGetValue(typedef, out string? enumVar))
            {
                throw new DecompileNotSupportedException($"enum typedef '{typedef}' is not a body-local enum_definition.");
            }
            if (!valueNameByValueId.TryGetValue(inivalue, out string? valueName))
            {
                throw new DecompileNotSupportedException($"enum inivalue '{inivalue}' is not a body-local enum_value.");
            }
            return (enumVar, valueName);
        }

        private static string RenderLeafArgs(string name, string link1Var, string method, string? link2Var, string? note)
        {
            var builder = new StringBuilder();
            builder.Append(CSharpLiteral.Quote(name)).Append(", ").Append(link1Var).Append(", ")
                .Append(CSharpLiteral.Quote(method));
            if (link2Var is not null)
            {
                builder.Append(", link2: ").Append(link2Var);
            }
            if (note is not null)
            {
                builder.Append(", note: ").Append(CSharpLiteral.Quote(note));
            }
            return builder.ToString();
        }

        private void Head(Action<FunctionBlockDefinitionBuilder> apply, string render) =>
            recipe.Head.Add(new FbHeadCall(apply, render));

        private static FbBranchRef BranchOf(FbBuildEnv env, string subVar, string branch) =>
            branch == "WhenTrue" ? env.Get<FbSubProgramRef>(subVar).WhenTrue : env.Get<FbSubProgramRef>(subVar).WhenFalse;

        private static (ProjectElement True, ProjectElement False) TrueFalseBranches(ProjectElement sub)
        {
            ImmutableArray<ProjectElement> actions = sub.Children.Where(c => c.Tag == "actions").ToImmutableArray();
            if (actions.Length != 2)
            {
                throw new DecompileNotSupportedException(
                    $"program_sub has {actions.Length} action branches (expected true + false).");
            }
            return (actions[0], actions[1]);
        }

        private static string? NoteOf(ProjectElement element)
        {
            string? note = element.GetAttribute("note");
            return string.IsNullOrEmpty(note) ? null : note;
        }

        private string? NonDefault(ProjectElement element, string attr)
        {
            string? value = element.GetAttribute(attr);
            if (value is null)
            {
                return null;
            }
            AttrSchema? schema = grammar.TryGet(element.Tag)?.FindAttr(attr);
            return schema is { Kind: AttrKind.Defaulted } && value == schema.Default ? null : value;
        }

        private static void RequireTag(ProjectElement element, string tag)
        {
            if (element.Tag != tag)
            {
                throw new DecompileNotSupportedException($"expected <{tag}> but found <{element.Tag}>.");
            }
        }

        // Live returns the appendable branch view uniformly: for the program root that is FbProgramBuilder.RootBranch,
        // for program_sub/case bodies the branch handle itself — so adds need no runtime type dispatch. Expr renders
        // the ORIGINAL variable (a generated factory still calls the builder's own AddAction/AddSubProgram/AddCase).
        private sealed record Target(string Expr, Func<FbBuildEnv, FbBranchRef> Live);

        private sealed class ResourceConfigItem
        {
            public ResourceConfigItem(Action<FbResourceDefBuilder, FbBuildEnv> apply, string render)
            {
                Apply = apply;
                Render = render;
            }

            public Action<FbResourceDefBuilder, FbBuildEnv> Apply { get; }
            public string Render { get; }
        }
    }
}
