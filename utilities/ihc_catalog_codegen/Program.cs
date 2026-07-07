#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ihc.Vis.CatalogCodegen
{
    /// <summary>
    /// Dev-time code generator that decompiles the vendor IHC Visual catalog (<c>Products\*.def</c>,
    /// <c>FunctionBlocks\*.ifb</c>) into committed builder-call source for <c>BuiltInCatalog</c>, so the SDK embeds the
    /// whole catalog and needs no install dir at runtime. It only ever READS the install dir — it never edits or
    /// deletes vendor files. Its fidelity gate is the SDK's own <c>DefinitionNormalizer</c> self-verify (the identical
    /// Normalize/compare the oracle tests use), so no component is emitted unless its <c>Build()</c> reproduces the
    /// source file canonically.
    /// </summary>
    /// <remarks>
    /// This phase (A4) is the harness scaffold: it enumerates the catalog and reports counts, writing nothing.
    /// Decompilation, self-verify, and deterministic emit land in Phase B.
    /// </remarks>
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
                return RunSelfTest(options.SelfTestDir, options.Preview);
            }

            if (!TryResolveInstallDirs(options.InstallDir, out CatalogPaths paths, out string? dirError))
            {
                Console.Error.WriteLine($"error: {dirError}");
                return 1;
            }

            if (options.OutDir is not null && !options.DryRun)
            {
                return RunEmit(paths.ProductsDir, options.OutDir);
            }
            return Enumerate(paths, options);
        }

        // Decompiles every product .def, self-verifies each, and (only if all pass) writes the committed
        // BuiltInCatalog.Products.g.cs. A single self-verify failure aborts the whole emit — a partial catalog is worse
        // than none. Reads only counts + failures into view; the generated body is never echoed.
        private static int RunEmit(string productsDir, string outDir)
        {
            EmitReport report = ProductCatalogEmitter.Emit(productsDir, outDir);
            Console.WriteLine($"emit: scanned {report.Scanned} product .def; {report.Emitted} recipes self-verified.");
            foreach (string failure in report.Failures)
            {
                Console.WriteLine($"  {failure}");
            }
            if (!report.Written)
            {
                Console.Error.WriteLine(
                    $"emit ABORTED — {report.Failures.Count} product(s) failed; nothing written.");
                return 1;
            }
            Console.WriteLine($"wrote {report.OutputPath}");
            return 0;
        }

        // Decompiles every product .def under a directory, replays each recipe against the real builder and verifies it
        // reproduces the source file (the same normalize/compare the oracle tests use). Constructs a later B1 sub-stage
        // owns are reported as UNSUPPORTED, not failures, so a flat-product bring-up cleanly distinguishes
        // "not implemented yet" from "wrong". Returns non-zero only when a supported recipe fails self-verify.
        private static int RunSelfTest(string dir, bool preview)
        {
            if (!Directory.Exists(dir))
            {
                Console.Error.WriteLine($"error: self-test dir '{dir}' does not exist.");
                return 1;
            }
            IReadOnlyList<string> files = FilesSorted(dir, "*.def");
            Console.WriteLine($"self-test: {files.Count} product .def under {dir}");
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

                VerifyResult result = SelfVerify.Verify(recipe, source);
                if (result.Ok)
                {
                    pass++;
                    Console.WriteLine($"  PASS         {name}  [{source.Definition.ProductIdentifier}]");
                    if (preview)
                    {
                        Console.WriteLine();
                        Console.WriteLine(recipe.RenderMethod("Product" + source.Definition.ProductIdentifier));
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
            Console.WriteLine();
            Console.WriteLine($"self-test summary: {pass} pass, {unsupported} unsupported, {fail} fail");
            return fail == 0 ? 0 : 1;
        }

        private static int Enumerate(CatalogPaths paths, Options options)
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

            // Phase A4 writes nothing. Emit + self-verify are Phase B; announce the seam rather than silently no-op.
            Console.WriteLine();
            Console.WriteLine(options.OutDir is null
                ? "dry-run: enumerated only (no --out given); code emit lands in Phase B."
                : $"--out '{options.OutDir}' recorded, but code emit is not implemented yet (Phase B). Nothing written.");
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
                "usage: ihc_catalog_codegen --install-dir <dir> [--out <dir>] [--dry-run] [--help]\n" +
                "       ihc_catalog_codegen --self-test <dir> [--preview]\n" +
                "\n" +
                "  --install-dir <dir>  Read-only IHC Visual install dir (contains Products\\, FunctionBlocks\\, Data\\).\n" +
                "  --out <dir>          Output dir for generated *.g.cs (Phase B; ignored in this scaffold).\n" +
                "  --dry-run            Enumerate and self-verify without writing (the only mode in this scaffold).\n" +
                "  --self-test <dir>    Decompile every product .def under <dir> and verify each recipe reproduces its\n" +
                "                       source file (points at a folder of .def files, e.g. the synthetic oracles).\n" +
                "  --preview            With --self-test, print the generated factory method for each passing product.\n" +
                "  --help               Show this help.\n" +
                "\n" +
                "Vendor install files are never modified or deleted.";

            public string? InstallDir { get; private init; }
            public string? OutDir { get; private init; }
            public string? SelfTestDir { get; private init; }
            public bool DryRun { get; private init; }
            public bool Preview { get; private init; }
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
                bool dryRun = false;
                bool preview = false;
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
                        default:
                            error = $"unknown argument '{args[i]}'.";
                            return null;
                    }
                }
                return new Options
                {
                    InstallDir = installDir,
                    OutDir = outDir,
                    SelfTestDir = selfTestDir,
                    DryRun = dryRun,
                    Preview = preview,
                    ShowHelp = help,
                };
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
