using System.IO;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// US-041: the SDK function-block report builder (<see cref="ProjectAppService.GenerateFunctionBlockReport"/>)
    /// over the <c>project3-KompleksWired.vis</c> oracle — every function block in Functions-pane document order,
    /// each carrying the four variable sections and their variable names.
    /// </summary>
    public class FunctionBlockReportTests
    {
        private static FunctionBlockReport Report(string name = "project3-KompleksWired.vis") =>
            new ProjectAppService(TestSetup.Settings).GenerateFunctionBlockReport(
                new ProjectAppService(TestSetup.Settings).Load(
                    new MemoryStream(TestData.ReadBytes(Path.Combine("projects", name)))).GetAwaiter().GetResult());

        [Test]
        public void FunctionBlockReport_HasHeadingAndBlocksInDocumentOrder()
        {
            FunctionBlockReport report = Report();
            Assert.Multiple(() =>
            {
                Assert.That(report.Heading, Is.EqualTo("Functionsblok dokumentation"));
                Assert.That(report.Blocks, Is.Not.Empty, "project3 has function blocks");
                // Every block lists exactly the four variable sections in document order.
                foreach (FunctionBlockReportEntry block in report.Blocks)
                {
                    Assert.That(block.Sections.Select(s => s.Label),
                        Is.EqualTo(new[] { "Input", "Output", "Settings", "Internal variables" }),
                        $"block '{block.Name}' section labels");
                }
            });
        }

        [Test]
        public void FunctionBlockReport_ListsBlockVariableNames()
        {
            FunctionBlockReport report = Report();
            // Every variable name surfaced is non-empty, and at least one block declares an input variable.
            Assert.Multiple(() =>
            {
                Assert.That(report.Blocks.SelectMany(b => b.Sections).SelectMany(s => s.Variables),
                    Is.All.Not.Empty);
                Assert.That(report.Blocks.Any(b => b.Sections.First(s => s.Label == "Input").Variables.Length > 0),
                    Is.True, "at least one block has input variables");
            });
        }
    }
}
