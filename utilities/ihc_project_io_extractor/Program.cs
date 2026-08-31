using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Ihc.IOExtractor {
    /**
    * Parses an IHC project filename and generates source files with resource definitions.
    * Usage from commandline: dotnet run project.vis <destination dir>
    */
    public class Program
    {
        static void Main(string[] args)
        {
            if (args.Length!=2) {
                Console.WriteLine("Expected arguments: <projectsource> <destdir>");
                return;
            }

            string ihcProjectName = args[0];
            string destDir = args[1];
            if (!File.Exists(ihcProjectName)) {
                 Console.WriteLine("Could not find file " + ihcProjectName);
                 return;
            }

            if (!Directory.Exists(destDir)) {
                 Console.WriteLine("Could not find directory " + destDir);
                 return;
            }

            // Ihc.IhcConfiguration.FromAppDirectory() is the shared form of this, but reaching it would mean
            // referencing the whole SDK from a utility that deliberately parses .vis with its own loader. The
            // guard is what matters: GetEntryAssembly() is null outside a managed Main, and Location is empty
            // for a single-file publish.
            string basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? AppContext.BaseDirectory;
            IConfigurationRoot config = new ConfigurationBuilder()
                                .SetBasePath(basePath)
                                .AddJsonFile("ihcsettings.json")
                                .Build();

            IhcProjectLoader project = new IhcProjectLoader(ihcProjectName);

            IConfiguration appConfig = config.GetSection("projectExtrator");
          
            var generators = new GeneratorBase[] {
                new JsonGenerator(appConfig),
                new ScriptConstantGenerator(appConfig, "js", " export const {0} = {{ ", "    {0} : 0x{1}", " };"),
                new ScriptConstantGenerator(appConfig, "ts", " export const enum {0} {{ ", "    {0} = 0x{1}", " };"),
                new CSharpGenerator(appConfig)
            };

            var projectFileName = Path.GetFileName(ihcProjectName);
            foreach (var generator in generators) {
                var output = generator.Generate(project);
                var fileName = Path.ChangeExtension(projectFileName, generator.FileExtension());

                File.WriteAllText(Path.Combine(destDir, fileName), output);
            }
        }
    }
}
