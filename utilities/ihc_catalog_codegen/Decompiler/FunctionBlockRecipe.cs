#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

using Ihc.Vis.FunctionBlocks;

namespace Ihc.Vis.CatalogCodegen
{
    /// <summary>
    /// The live environment a function-block recipe replays into: the one <see cref="FunctionBlockDefinitionBuilder"/>
    /// under construction plus a name→object table mirroring the <c>var</c> locals the rendered method declares
    /// (resource handles, enum handles, program/sub/case refs). Statement apply-closures read and write it by the same
    /// names the render side prints, so the executed plan and the emitted source stay in lock-step.
    /// </summary>
    internal sealed class FbBuildEnv
    {
        public FbBuildEnv(FunctionBlockDefinitionBuilder builder) => Builder = builder;

        public FunctionBlockDefinitionBuilder Builder { get; }

        private readonly Dictionary<string, object> vars = new(StringComparer.Ordinal);

        public void Set(string name, object value) => vars[name] = value;

        public T Get<T>(string name) => (T)vars[name];
    }

    /// <summary>One block-level fluent setter chained onto <c>FunctionBlockDefinitionBuilder.Create(..)</c> in the
    /// method head (e.g. <c>.VendorMaster()</c>, <c>.Note(..)</c>): the live <see cref="Apply"/> paired with the
    /// rendered C# call, authored together so they cannot disagree.</summary>
    internal sealed class FbHeadCall
    {
        public FbHeadCall(Action<FunctionBlockDefinitionBuilder> apply, string render)
        {
            Apply = apply;
            Render = render;
        }

        public Action<FunctionBlockDefinitionBuilder> Apply { get; }
        public string Render { get; }
    }

    /// <summary>One statement in the method body — a <c>var</c> declaration (add a resource/enum/program/sub/case) or a
    /// call on an earlier ref (wire an event/action/condition). Pairs the live <see cref="Apply"/> against the shared
    /// <see cref="FbBuildEnv"/> with the rendered C# statement text; both are authored from the same data.</summary>
    internal sealed class FbStatement
    {
        public FbStatement(Action<FbBuildEnv> apply, string render)
        {
            Apply = apply;
            Render = render;
        }

        public Action<FbBuildEnv> Apply { get; }

        /// <summary>The C# statement text without indentation or trailing newline (may contain internal newlines).</summary>
        public string Render { get; }
    }

    /// <summary>
    /// The decompiled plan for authoring one catalog function block from code: the master identity plus the chained
    /// block-level setters (the method head) and the ordered <see cref="FbStatement"/> body. Dual-nature like
    /// <see cref="ProductRecipe"/> — <see cref="Build"/> replays it against the real
    /// <see cref="FunctionBlockDefinitionBuilder"/> (so the generator self-verifies the plan reproduces the source
    /// <c>.ifb</c>), and <see cref="RenderMethod"/> emits the identical plan as a statement-bodied factory for
    /// <c>BuiltInCatalog</c>.
    /// </summary>
    internal sealed class FunctionBlockRecipe
    {
        public FunctionBlockRecipe(string masterType, string masterVersion, string masterName)
        {
            MasterType = masterType;
            MasterVersion = masterVersion;
            MasterName = masterName;
        }

        public string MasterType { get; }
        public string MasterVersion { get; }
        public string MasterName { get; }

        /// <summary>Block-level setters chained on <c>Create(..)</c> (VendorMaster/DisplayName/Note/container notes…).</summary>
        public List<FbHeadCall> Head { get; } = new();

        /// <summary>The body statements (resource/enum/program declarations and program-graph wiring).</summary>
        public List<FbStatement> Statements { get; } = new();

        /// <summary>Replays the plan against a fresh real builder and returns its <see cref="FunctionBlockDefinition"/> —
        /// the in-process block the generator normalizes against the source <c>.ifb</c> to gate emission.</summary>
        public FunctionBlockDefinition Build()
        {
            FunctionBlockDefinitionBuilder builder =
                FunctionBlockDefinitionBuilder.Create(MasterType, MasterVersion, MasterName);
            foreach (FbHeadCall call in Head)
            {
                call.Apply(builder);
            }
            var env = new FbBuildEnv(builder);
            foreach (FbStatement statement in Statements)
            {
                statement.Apply(env);
            }
            return builder.Build();
        }

        /// <summary>Emits the plan as a committed statement-bodied factory
        /// <c>private static FunctionBlockDefinition {methodName}() { var b = ...; ...; return b.Build(); }</c>.</summary>
        public string RenderMethod(string methodName)
        {
            var builder = new StringBuilder();
            builder.Append("        private static FunctionBlockDefinition ").Append(methodName).Append("()\n");
            builder.Append("        {\n");
            builder.Append("            var b = FunctionBlockDefinitionBuilder.Create(")
                .Append(CSharpLiteral.Quote(MasterType)).Append(", ")
                .Append(CSharpLiteral.Quote(MasterVersion)).Append(", ")
                .Append(CSharpLiteral.Quote(MasterName)).Append(')');
            if (Head.Count == 0)
            {
                builder.Append(";\n");
            }
            else
            {
                builder.Append('\n');
                for (int i = 0; i < Head.Count; i++)
                {
                    builder.Append("                ").Append(Head[i].Render);
                    builder.Append(i == Head.Count - 1 ? ";\n" : "\n");
                }
            }
            foreach (FbStatement statement in Statements)
            {
                builder.Append("            ").Append(statement.Render).Append('\n');
            }
            builder.Append("            return b.Build();\n");
            builder.Append("        }\n");
            return builder.ToString();
        }
    }
}
