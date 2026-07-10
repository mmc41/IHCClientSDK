#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Ihc.Vis.CatalogCodegen
{
    /// <summary>
    /// Dev-time code generator that decompiles the vendor IHC Visual catalog (<c>Products\*.def</c>,
    /// <c>FunctionBlocks\*.ifb</c>) into committed builder-call source for <c>BuiltInCatalog</c>, so the SDK embeds the
    /// whole catalog and needs no install dir at runtime. It only ever READS the install dir — it never edits or
    /// deletes vendor files. Its fidelity gate is two-tier: the SDK's own <c>DefinitionNormalizer</c> self-verify (the
    /// identical Normalize/compare the oracle tests use) plus a byte-level re-emission check
    /// (<c>CatalogFileWriter</c>-serializing the built definition must reproduce the source file, whitespace-normalized
    /// — the final-acceptance relation). No component is emitted unless both pass.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Options? options = Options.Parse(args, out string? parseError);
            if (parseError is not null)
            {
                Console.Error.WriteLine($"error: {parseError}");
                Console.Error.WriteLine();
                Console.Error.WriteLine(Options.Usage);
                return 2;
            }
            if (options is null || options.ShowHelp)
            {
                Console.WriteLine(Options.Usage);
                return 0;
            }

            if (options.SelfTestDir is not null)
            {
                return RunSelfTest(options.SelfTestDir, options.Preview, options.Expect);
            }

            if (options.SelfTestFbDir is not null)
            {
                return RunFbSelfTest(options.SelfTestFbDir, options.Preview, options.Expect);
            }

            if (!TryResolveInstallDirs(options.InstallDir, out CatalogPaths paths, out string? dirError))
            {
                Console.Error.WriteLine($"error: {dirError}");
                return 1;
            }

            if (options.OutDir is not null || options.DryRun)
            {
                return RunEmit(paths, options.OutDir, options.AcceptBaseline, options.DryRun);
            }
            return Enumerate(paths);
        }

        // ---- the versioned corpus baseline (this IHC Visual install copy) ----
        // Interned-table and file counts a regeneration must reproduce; a mismatch is reported as a diff and fails
        // the emit unless --accept-baseline explicitly acknowledges the change (then update these constants).
        // Per-file gates (byte fidelity + reparse + grammar equality) carry correctness; the counts only guard
        // against silently reading a different corpus.
        private const int BaselineProductFiles = 100;
        private const int BaselineFunctionBlockFiles = 73;
        private const int BaselineDeclarationRecords = 100;
        private const int BaselineGrammars = 99;

        // The coordinated all-or-nothing emit (collect-all → verify-all → render-in-memory → syntax-parse →
        // fingerprint → publish-all): decompiles and self-verifies EVERY product and function block first, interns
        // the grammar tables globally, renders the three generated files in memory, Roslyn-parses each (renderer
        // garbage must not reach the working tree), stamps one shared content-derived generation fingerprint, and
        // only then publishes each file via temp + File.Replace. A single failure anywhere aborts with nothing
        // written — a partial catalog is worse than none. A dry run executes the identical pipeline and stops after
        // the fingerprint, so its exit status carries the full-corpus verification verdict without touching the tree.
        private static int RunEmit(CatalogPaths paths, string? outDir, bool acceptBaseline, bool dryRun)
        {
            DecompileReport<ProductRecipe> products = ProductCatalogEmitter.Decompile(paths.ProductsDir);
            Console.WriteLine($"emit: scanned {products.Scanned} product .def; {products.Factories.Count} recipes self-verified.");
            foreach (string failure in products.Failures)
            {
                Console.WriteLine($"  {failure}");
            }

            DecompileReport<FunctionBlockRecipe> functionBlocks = FunctionBlockCatalogEmitter.Decompile(paths.FunctionBlocksDir);
            Console.WriteLine(
                $"emit: scanned {functionBlocks.Scanned} function-block .ifb; {functionBlocks.Factories.Count} recipes self-verified.");
            foreach (string failure in functionBlocks.Failures)
            {
                Console.WriteLine($"  {failure}");
            }

            if (products.Failures.Count > 0 || functionBlocks.Failures.Count > 0)
            {
                Console.Error.WriteLine(
                    $"emit ABORTED — {products.Failures.Count} product(s) + {functionBlocks.Failures.Count} block(s) failed; "
                    + "nothing written.");
                return 1;
            }

            GrammarTable grammars = GrammarTable.Build(
                products.Factories.Select(f => (f.File, f.Recipe.SourceGrammar))
                    .Concat(functionBlocks.Factories.Select(f => (f.File, f.Recipe.SourceGrammar))));
            Console.WriteLine(
                $"emit: interned {grammars.DeclarationCount} declaration records / {grammars.GrammarCount} grammars.");

            var baselineDiffs = new List<string>();
            void CheckBaseline(string what, int actual, int expected)
            {
                if (actual != expected)
                {
                    baselineDiffs.Add($"{what}: expected {expected} (baseline), found {actual}");
                }
            }
            CheckBaseline("product files", products.Scanned, BaselineProductFiles);
            CheckBaseline("function-block files", functionBlocks.Scanned, BaselineFunctionBlockFiles);
            CheckBaseline("interned declaration records", grammars.DeclarationCount, BaselineDeclarationRecords);
            CheckBaseline("interned grammars", grammars.GrammarCount, BaselineGrammars);
            if (baselineDiffs.Count > 0)
            {
                foreach (string diff in baselineDiffs)
                {
                    Console.Error.WriteLine($"baseline: {diff}");
                }
                Console.Error.WriteLine("baseline: declaration records now present:");
                foreach (string summary in grammars.DeclarationSummaries)
                {
                    Console.Error.WriteLine($"  {summary}");
                }
                if (!acceptBaseline)
                {
                    Console.Error.WriteLine(
                        "emit ABORTED — corpus differs from the versioned baseline. If the catalog legitimately " +
                        "changed, re-run with --accept-baseline and update the baseline constants in Program.cs.");
                    return 1;
                }
                Console.WriteLine("baseline: mismatch acknowledged via --accept-baseline; continuing.");
            }

            var rendered = new (string FileName, string Source)[]
            {
                ("BuiltInCatalog.Grammar.g.cs", grammars.RenderFile()),
                (ProductCatalogEmitter.FileName, ProductCatalogEmitter.RenderFile(products.Factories, grammars)),
                (FunctionBlockCatalogEmitter.FileName, FunctionBlockCatalogEmitter.RenderFile(functionBlocks.Factories, grammars)),
            };

            // Syntax gate: each rendered file must parse as C# with zero error diagnostics.
            foreach ((string fileName, string source) in rendered)
            {
                var diagnostics = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source)
                    .GetDiagnostics()
                    .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .ToArray();
                if (diagnostics.Length > 0)
                {
                    Console.Error.WriteLine($"emit ABORTED — rendered {fileName} does not parse as C#:");
                    foreach (var diagnostic in diagnostics.Take(10))
                    {
                        Console.Error.WriteLine($"  {diagnostic}");
                    }
                    return 1;
                }
            }

            // One shared content-derived fingerprint (not a timestamp — regeneration stays deterministic): a
            // mixed generated tree, e.g. after a crash between the three replaces, is detectable by comparing it.
            string fingerprint = Fingerprint(rendered.Select(r => r.Source));
            if (dryRun)
            {
                Console.WriteLine($"dry-run: full corpus decompiled, self-verified and rendered; nothing written. " +
                    $"Generation fingerprint would be: {fingerprint}");
                return 0;
            }
            if (outDir is null)
            {
                Console.Error.WriteLine("error: --out <dir> is required to write (or pass --dry-run).");
                return 2;
            }
            string stampLine = $"//     Generation fingerprint: {fingerprint} (identical across the three generated files of one run).\n";
            const string stampAnchor = "// </auto-generated>";

            Directory.CreateDirectory(outDir);
            foreach ((string fileName, string source) in rendered)
            {
                string stamped = source.Replace(stampAnchor, stampLine + stampAnchor);
                string target = Path.Combine(outDir, fileName);
                string temp = target + ".tmp";
                // UTF-8 without BOM, matching the repo's hand-authored .cs.
                File.WriteAllText(temp, stamped, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                if (File.Exists(target))
                {
                    File.Replace(temp, target, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(temp, target);
                }
                Console.WriteLine($"wrote {target}");
            }
            Console.WriteLine($"generation fingerprint: {fingerprint}");
            return 0;
        }

        private static string Fingerprint(IEnumerable<string> renderedBodies)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var content = new System.Text.StringBuilder();
            foreach (string body in renderedBodies)
            {
                content.Append(body).Append('\0');
            }
            byte[] digest = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content.ToString()));
            return Convert.ToHexString(digest, 0, 6).ToLowerInvariant();
        }

        // Decompiles every product .def under a directory, replays each recipe against the real builder and verifies it
        // reproduces the source file (the same normalize/compare the oracle tests use, plus the byte-level re-emission
        // gate). Unreversible constructs are reported as UNSUPPORTED, not failures, so a bring-up cleanly distinguishes
        // "not implemented yet" from "wrong". The exit status is gate-safe: non-zero on any failure, on an empty
        // directory (an empty run verifies nothing), and — with --expect — on any count drift (a file silently
        // becoming UNSUPPORTED must not read as success).
        private static int RunSelfTest(string dir, bool preview, (int Pass, int Unsupported)? expect)
        {
            if (!Directory.Exists(dir))
            {
                Console.Error.WriteLine($"error: self-test dir '{dir}' does not exist.");
                return 1;
            }
            IReadOnlyList<string> files = FilesSorted(dir, "*.def");
            Console.WriteLine($"self-test: {files.Count} product .def under {dir}");
            if (files.Count == 0)
            {
                Console.Error.WriteLine($"error: no .def files under '{dir}' — an empty run verifies nothing.");
                return 1;
            }
            int pass = 0, unsupported = 0, fail = 0;
            foreach (string path in files)
            {
                string name = Path.GetFileName(path);
                string categoryPath = Path.GetDirectoryName(Path.GetRelativePath(dir, path)) ?? string.Empty;
                ProductSource source = CatalogSourceFile.ReadProduct(path, categoryPath);
                ProductRecipe recipe;
                try
                {
                    recipe = ProductDecompiler.Decompile(
                        source.Definition.Body, source.Blocks, source.Definition.DisplayName, categoryPath);
                }
                catch (DecompileNotSupportedException ex)
                {
                    unsupported++;
                    Console.WriteLine($"  UNSUPPORTED  {name}  ({ex.Message})");
                    continue;
                }

                recipe.BakeSourceFidelity(source);
                VerifyResult result = SelfVerify.Verify(recipe, source);
                if (result.Ok)
                {
                    pass++;
                    Console.WriteLine($"  PASS         {name}  [{source.Definition.ProductIdentifier}]");
                    if (preview)
                    {
                        Console.WriteLine();
                        Console.WriteLine(recipe.RenderMethod("Product" + source.Definition.ProductIdentifier, "BuiltInCatalogGrammar.G_preview"));
                    }
                }
                else
                {
                    fail++;
                    Console.WriteLine($"  FAIL         {name}  ({result.Reason})");
                    if (result.Expected is not null)
                    {
                        Console.WriteLine("   --- expected (oracle) ---");
                        Console.WriteLine(result.Expected);
                        Console.WriteLine("   --- actual (builder) ---");
                        Console.WriteLine(result.Actual);
                    }
                }
            }
            return SelfTestVerdict("self-test", pass, unsupported, fail, expect);
        }

        // Decompiles every function-block .ifb under a directory, replays each recipe against the real builder and
        // verifies it reproduces the source file (the same normalize/compare the oracle tests use, plus the byte-level
        // re-emission gate). Constructs not yet reversed are reported UNSUPPORTED, not failures. Exit status is
        // gate-safe under the same rules as --self-test (see RunSelfTest).
        private static int RunFbSelfTest(string dir, bool preview, (int Pass, int Unsupported)? expect)
        {
            if (!Directory.Exists(dir))
            {
                Console.Error.WriteLine($"error: self-test-fb dir '{dir}' does not exist.");
                return 1;
            }
            IReadOnlyList<string> files = FilesSorted(dir, "*.ifb");
            Console.WriteLine($"self-test-fb: {files.Count} function-block .ifb under {dir}");
            if (files.Count == 0)
            {
                Console.Error.WriteLine($"error: no .ifb files under '{dir}' — an empty run verifies nothing.");
                return 1;
            }
            int pass = 0, unsupported = 0, fail = 0;
            foreach (string path in files)
            {
                string name = Path.GetFileName(path);
                string categoryPath = Path.GetDirectoryName(Path.GetRelativePath(dir, path)) ?? string.Empty;
                FunctionBlockSource source = CatalogSourceFile.ReadFunctionBlock(path, categoryPath);
                FunctionBlockRecipe recipe;
                try
                {
                    recipe = FunctionBlockDecompiler.Decompile(source.Definition, source.Blocks);
                }
                catch (DecompileNotSupportedException ex)
                {
                    unsupported++;
                    Console.WriteLine($"  UNSUPPORTED  {name}  ({ex.Message})");
                    continue;
                }

                recipe.BakeSourceFidelity(source);
                VerifyResult result = FbSelfVerify.Verify(recipe, source);
                if (result.Ok)
                {
                    pass++;
                    Console.WriteLine($"  PASS         {name}  [{source.Definition.MasterType}]");
                    if (preview)
                    {
                        Console.WriteLine();
                        Console.WriteLine(recipe.RenderMethod("FunctionBlock_" + name.Replace('.', '_'), "BuiltInCatalogGrammar.G_preview"));
                    }
                }
                else
                {
                    fail++;
                    Console.WriteLine($"  FAIL         {name}  ({result.Reason})");
                    if (result.Expected is not null)
                    {
                        Console.WriteLine("   --- expected (oracle) ---");
                        Console.WriteLine(result.Expected);
                        Console.WriteLine("   --- actual (builder) ---");
                        Console.WriteLine(result.Actual);
                    }
                }
            }
            return SelfTestVerdict("self-test-fb", pass, unsupported, fail, expect);
        }

        // The shared exit-status verdict of the two self-test modes: any failure is fatal; with --expect the pass and
        // unsupported counts must ALSO match exactly, so a regression that flips a passing file to UNSUPPORTED (which
        // is not a failure by itself — bring-up semantics) still fails the gate.
        private static int SelfTestVerdict(string label, int pass, int unsupported, int fail,
            (int Pass, int Unsupported)? expect)
        {
            Console.WriteLine();
            Console.WriteLine($"{label} summary: {pass} pass, {unsupported} unsupported, {fail} fail");
            if (fail > 0)
            {
                return 1;
            }
            if (expect is { } e && (pass != e.Pass || unsupported != e.Unsupported))
            {
                Console.Error.WriteLine(
                    $"{label} FAILED — counts differ from --expect {e.Pass}/{e.Unsupported}: found {pass}/{unsupported}.");
                return 1;
            }
            return 0;
        }

        private static int Enumerate(CatalogPaths paths)
        {
            IReadOnlyList<string> products = FilesSorted(paths.ProductsDir, "*.def");
            IReadOnlyList<string> functionBlocks = FilesSorted(paths.FunctionBlocksDir, "*.ifb");
            IReadOnlyList<string> templates = paths.TemplateFiles.Where(File.Exists).ToArray();

            Console.WriteLine($"IHC Visual catalog at: {paths.InstallDir}");
            Console.WriteLine($"  products        (Products\\**\\*.def)      : {products.Count}");
            Console.WriteLine($"  function blocks (FunctionBlocks\\**\\*.ifb): {functionBlocks.Count}");
            Console.WriteLine($"  File->New templates (Data\\)             : {templates.Count}/{paths.TemplateFiles.Count} present");
            foreach (string template in paths.TemplateFiles)
            {
                Console.WriteLine($"      {(File.Exists(template) ? "OK " : "MISSING")} {Path.GetFileName(template)}");
            }

            Console.WriteLine();
            Console.WriteLine("enumerated only — pass --out <dir> to regenerate the catalog source, or --dry-run to "
                + "decompile + self-verify the corpus without writing.");
            return 0;
        }

        private static bool TryResolveInstallDirs(string? installDir, out CatalogPaths paths, out string? error)
        {
            paths = default!;
            if (string.IsNullOrWhiteSpace(installDir))
            {
                error = "--install-dir <dir> is required (the read-only IHC Visual install directory).";
                return false;
            }
            if (!Directory.Exists(installDir))
            {
                error = $"install dir '{installDir}' does not exist.";
                return false;
            }
            var candidate = new CatalogPaths(installDir);
            foreach ((string dir, string name) in new[]
                     {
                         (candidate.ProductsDir, "Products"),
                         (candidate.FunctionBlocksDir, "FunctionBlocks"),
                         (candidate.DataDir, "Data"),
                     })
            {
                if (!Directory.Exists(dir))
                {
                    error = $"install dir '{installDir}' has no '{name}' subdirectory — not a complete IHC Visual install.";
                    return false;
                }
            }
            paths = candidate;
            error = null;
            return true;
        }

        private static IReadOnlyList<string> FilesSorted(string root, string pattern) =>
            Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();

        private sealed class CatalogPaths
        {
            public CatalogPaths(string installDir)
            {
                InstallDir = installDir;
                ProductsDir = Path.Combine(installDir, "Products");
                FunctionBlocksDir = Path.Combine(installDir, "FunctionBlocks");
                DataDir = Path.Combine(installDir, "Data");
                TemplateFiles = new[]
                {
                    Path.Combine(DataDir, "NewDoc.idf"),
                    Path.Combine(DataDir, "EnumeratorDefinitions.def"),
                    Path.Combine(DataDir, "fb.def"),
                };
            }

            public string InstallDir { get; }
            public string ProductsDir { get; }
            public string FunctionBlocksDir { get; }
            public string DataDir { get; }
            public IReadOnlyList<string> TemplateFiles { get; }
        }

        private sealed class Options
        {
            public const string Usage =
                "ihc_catalog_codegen — decompile the IHC Visual catalog into BuiltInCatalog builder source.\n" +
                "\n" +
                "usage: ihc_catalog_codegen --install-dir <dir> (--out <dir> | --dry-run) [--accept-baseline] [--help]\n" +
                "       ihc_catalog_codegen --self-test <dir> [--expect <pass>/<unsupported>] [--preview]\n" +
                "       ihc_catalog_codegen --self-test-fb <dir> [--expect <pass>/<unsupported>] [--preview]\n" +
                "\n" +
                "  --install-dir <dir>  Read-only IHC Visual install dir (contains Products\\, FunctionBlocks\\, Data\\).\n" +
                "  --out <dir>          Decompile + byte-self-verify the whole catalog, then regenerate the *.g.cs\n" +
                "                       files there (all-or-nothing: any failure aborts with nothing written).\n" +
                "  --dry-run            The identical decompile + self-verify pipeline without writing anything;\n" +
                "                       exit status carries the full-corpus verification verdict.\n" +
                "  --accept-baseline    Continue an emit whose corpus counts differ from the versioned baseline\n" +
                "                       (then update the baseline constants in Program.cs).\n" +
                "  --self-test <dir>    Decompile every product .def under <dir> and verify each recipe reproduces its\n" +
                "                       source file (points at a folder of .def files, e.g. the synthetic oracles).\n" +
                "  --self-test-fb <dir> Decompile every function-block .ifb under <dir> and verify each recipe.\n" +
                "  --expect <p>/<u>     With --self-test/--self-test-fb: fail unless exactly <p> files pass and <u>\n" +
                "                       are UNSUPPORTED — makes the exit status safe to gate on.\n" +
                "  --preview            With --self-test/--self-test-fb, print the generated factory method for each\n" +
                "                       passing file.\n" +
                "  --help               Show this help.\n" +
                "\n" +
                "Vendor install files are never modified or deleted.";

            public string? InstallDir { get; private init; }
            public string? OutDir { get; private init; }
            public string? SelfTestDir { get; private init; }
            public string? SelfTestFbDir { get; private init; }
            public bool DryRun { get; private init; }
            public bool Preview { get; private init; }
            public bool AcceptBaseline { get; private init; }
            public (int Pass, int Unsupported)? Expect { get; private init; }
            public bool ShowHelp { get; private init; }

            public static Options? Parse(string[] args, out string? error)
            {
                error = null;
                if (args.Length == 0)
                {
                    return new Options { ShowHelp = true };
                }
                string? installDir = null;
                string? outDir = null;
                string? selfTestDir = null;
                string? selfTestFbDir = null;
                bool dryRun = false;
                bool preview = false;
                bool acceptBaseline = false;
                (int Pass, int Unsupported)? expect = null;
                bool help = false;
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--help" or "-h":
                            help = true;
                            break;
                        case "--dry-run":
                            dryRun = true;
                            break;
                        case "--preview":
                            preview = true;
                            break;
                        case "--accept-baseline":
                            acceptBaseline = true;
                            break;
                        case "--install-dir":
                            if (!TryTakeValue(args, ref i, out installDir))
                            {
                                error = "--install-dir requires a value.";
                                return null;
                            }
                            break;
                        case "--out":
                            if (!TryTakeValue(args, ref i, out outDir))
                            {
                                error = "--out requires a value.";
                                return null;
                            }
                            break;
                        case "--self-test":
                            if (!TryTakeValue(args, ref i, out selfTestDir))
                            {
                                error = "--self-test requires a value.";
                                return null;
                            }
                            break;
                        case "--self-test-fb":
                            if (!TryTakeValue(args, ref i, out selfTestFbDir))
                            {
                                error = "--self-test-fb requires a value.";
                                return null;
                            }
                            break;
                        case "--expect":
                            if (!TryTakeValue(args, ref i, out string? expectRaw) || !TryParseExpect(expectRaw!, out expect))
                            {
                                error = $"--expect requires a value of the form <pass>/<unsupported>, e.g. --expect 7/1"
                                    + (expectRaw is null ? "." : $" (got '{expectRaw}').");
                                return null;
                            }
                            break;
                        default:
                            error = $"unknown argument '{args[i]}'.";
                            return null;
                    }
                }
                return new Options
                {
                    InstallDir = installDir,
                    AcceptBaseline = acceptBaseline,
                    OutDir = outDir,
                    SelfTestDir = selfTestDir,
                    SelfTestFbDir = selfTestFbDir,
                    DryRun = dryRun,
                    Preview = preview,
                    Expect = expect,
                    ShowHelp = help,
                };
            }

            // "7/1" → (7, 1). NumberStyles.None: plain non-negative digits only — no signs, whitespace or separators.
            private static bool TryParseExpect(string raw, out (int Pass, int Unsupported)? expect)
            {
                expect = null;
                string[] parts = raw.Split('/');
                if (parts.Length != 2
                    || !int.TryParse(parts[0], System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out int pass)
                    || !int.TryParse(parts[1], System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out int unsupported))
                {
                    return false;
                }
                expect = (pass, unsupported);
                return true;
            }

            private static bool TryTakeValue(string[] args, ref int i, out string? value)
            {
                if (i + 1 >= args.Length)
                {
                    value = null;
                    return false;
                }
                value = args[++i];
                return true;
            }
        }
    }
}
