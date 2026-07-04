using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The three layers that see an out-of-grammar attribute must agree: <see cref="ProjectValidator"/> reports it,
    /// <see cref="ProjectSerializer"/> refuses it, and an edit session refuses it up front — never the previous
    /// split where Validate said clean, direct Save threw, and Edit()+commit silently deleted the attribute.
    /// Also pins the serializer's #REQUIRED guard: a missing required attribute must fail the save loudly instead
    /// of writing a DTD-invalid file IHC Visual rejects after the original was already replaced.
    /// </summary>
    public class AttributePolicyTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static Project Load(string name)
        {
            using var ms = new MemoryStream(TestData.ReadBytes(name));
            return new ProjectAppService(Settings).Load(ms).GetAwaiter().GetResult();
        }

        [Test]
        public void Edit_UndeclaredLoadedAttribute_ThrowsAtSessionOpen()
        {
            // 'bogus' on group "Stue" is declared by neither the file's inline DTD nor the registry. Serialize
            // already throws for it; the edit session must fail the same way at open — not silently drop it at
            // ToProject() and save a file with the attribute gone.
            Project project = Load("Synthetic/OpenWorldUndeclaredAttr.vis");

            Assert.That(() => project.Edit(),
                Throws.InvalidOperationException.With.Message.Contains("bogus").And.Message.Contains("group"));
        }

        [Test]
        public void Validate_UndeclaredAttribute_IsReported()
        {
            Project project = Load("Synthetic/OpenWorldUndeclaredAttr.vis");

            ProjectValidationResult result = ProjectValidator.Validate(project);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.False, "Validate must be a superset of what Serialize rejects");
                Assert.That(result.Errors.Any(e => e.Contains("bogus") && e.Contains("group")), Is.True,
                    "errors: " + string.Join(" | ", result.Errors));
            });
        }

        [Test]
        public void Serialize_MissingRequiredAttribute_Throws()
        {
            // modified@year is #REQUIRED; stripping it must fail the serialize, not emit a DTD-invalid file.
            Project project = Load("Project1-SimpelWired.vis");
            ProjectElement modified = project.Root.FindChild("modified")!;
            ProjectElement stripped = modified with
            {
                Attrs = modified.Attrs.Where(a => a.Item1 != "year").ToImmutableArray(),
            };
            int index = project.Root.Children.IndexOf(modified);
            Project broken = project with
            {
                Root = project.Root with { Children = project.Root.Children.SetItem(index, stripped) },
            };

            Assert.That(() => ProjectSerializer.Serialize(broken),
                Throws.InvalidOperationException.With.Message.Contains("modified").And.Message.Contains("year"));
        }

        [Test]
        public void Validate_MissingRequiredAttribute_MatchesTheSerializerVerdict()
        {
            // The validator's pre-flight and the serializer's guard must agree on the same broken project.
            Project project = Load("Project1-SimpelWired.vis");
            ProjectElement modified = project.Root.FindChild("modified")!;
            ProjectElement stripped = modified with
            {
                Attrs = modified.Attrs.Where(a => a.Item1 != "year").ToImmutableArray(),
            };
            int index = project.Root.Children.IndexOf(modified);
            Project broken = project with
            {
                Root = project.Root with { Children = project.Root.Children.SetItem(index, stripped) },
            };

            ProjectValidationResult result = ProjectValidator.Validate(broken);

            Assert.That(result.Errors.Any(e => e.Contains("year") && e.Contains("modified")), Is.True,
                "errors: " + string.Join(" | ", result.Errors));
        }
    }
}
