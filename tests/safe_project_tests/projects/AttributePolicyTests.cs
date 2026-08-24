using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The three layers that see an out-of-grammar attribute must agree: the whole-project verification reports it,
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
            using var ms = new MemoryStream(TestData.ReadBytes("projects/" + name));
            return new ProjectAppService(Settings).Load(ms).GetAwaiter().GetResult();
        }

        [Test]
        public void Edit_UndeclaredLoadedAttribute_ThrowsAtSessionOpen()
        {
            // 'bogus' on group "Stue" is declared by neither the file's inline DTD nor the registry. Serialize
            // already throws for it; the edit session must fail the same way at open — not silently drop it at
            // ToProject() and save a file with the attribute gone.
            Project project = Load("Synthetic/OpenWorldUndeclaredAttr.vis");

            // InstanceOf, not Throws.InvalidOperationException: that form matches the EXACT type, and the refusal
            // is now a RefusedOperationException, which derives from it precisely so every existing caller keeps
            // catching it. What the base type buys is asserted here; the code it now carries is asserted beside it.
            Assert.That(() => project.Edit(),
                Throws.InstanceOf<InvalidOperationException>()
                    .With.Message.Contains("bogus").And.Message.Contains("group"));

            RefusedOperationException refusal =
                Assert.Throws<RefusedOperationException>(() => project.Edit())!;

            Assert.Multiple(() =>
            {
                Assert.That(refusal.Problems!.Operation.Code, Is.EqualTo(OperationCodes.EditOpen),
                    "the open is what was refused");
                Assert.That(refusal.Problems.Cause.Message, Does.Contain("bogus").And.Contain("group"),
                    "and the Danish sentence names the attribute and its element");
            });
        }

        [Test]
        public void Validate_UndeclaredAttribute_IsReported()
        {
            Project project = Load("Synthetic/OpenWorldUndeclaredAttr.vis");

            ProjectValidationResult result = ProjectVerification.Structural(project);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.False, "Validate must be a superset of what Serialize rejects");
                Assert.That(result.Findings.Any(f => f.RuleId == "attr-undeclared"), Is.True,
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
                Root = project.Root with { Children = project.Root.Children.AsImmutableArray().SetItem(index, stripped) },
            };

            // InstanceOf, not TypeOf: the refusal now carries a code (attr-required under io.save) and is the
            // derived RefusedOperationException. It is still an InvalidOperationException, which is the promise
            // this assertion is here to keep.
            Assert.That(() => ProjectSerializer.Serialize(broken),
                Throws.InstanceOf<InvalidOperationException>()
                    .With.Message.Contains("modified").And.Message.Contains("year"));
        }

        // Rebuilds the tree with the element of the given id replaced — a raw model edit that bypasses the editor's
        // canonicalization, so a default-equal / undeclared attribute actually reaches the serializer.
        private static ProjectElement Replace(ProjectElement node, ElementId id, ProjectElement replacement) =>
            node.Id == id
                ? replacement
                : node with { Children = node.Children.Select(c => Replace(c, id, replacement)).ToImmutableArray() };

        // T036 (D03): pin — the serializer OMITS a Defaulted attribute whose value equals its DTD default
        // (omit-if-default, AttrSchema.OmitsOnWrite); the reader never re-materializes it. No serializer change.
        [Test]
        public void Serialize_AttributeEqualToItsDtdDefault_IsOmitted()
        {
            Project project = Load("Project1-SimpelWired.vis");
            ProjectSchemaView view = project.SchemaView;
            // Find a real element whose schema declares a Defaulted attribute with a non-empty default, currently absent.
            (ProjectElement Element, AttrSchema Attr) target = project.Root.DescendantsAndSelf()
                .Where(e => e.Id is not null)
                .Select(e => (Element: e, Attr: view.TryGet(e.Tag)?.Attrs
                    .FirstOrDefault(a => a.Kind == AttrKind.Defaulted && a.Default.Length > 0 && e.GetAttribute(a.Name) is null)))
                .First(x => x.Attr is not null)!;
            ElementId id = target.Element.Id!.Value;

            // Set that attribute to EXACTLY its DTD default and re-serialize.
            Project modified = project with { Root = Replace(project.Root, id, target.Element.WithAttribute(target.Attr.Name, target.Attr.Default)) };
            Project reparsed = ProjectReader.Read(new MemoryStream(ProjectSerializer.Serialize(modified)));

            Assert.That(reparsed.FindById(id)!.GetAttribute(target.Attr.Name), Is.Null,
                $"attribute '{target.Attr.Name}' equal to its DTD default ('{target.Attr.Default}') is omitted on write (omit-if-default)");
        }

        // T036 (D03): pin — the serializer REFUSES (throws) an attribute the element's schema does not declare,
        // rather than silently dropping it or writing a DTD-invalid file. No serializer change.
        [Test]
        public void Serialize_UndeclaredAttribute_IsRefused()
        {
            Project project = Load("Project1-SimpelWired.vis");
            ProjectElement group = project.Groups.First();
            Project modified = project with { Root = Replace(project.Root, group.Id!.Value, group.WithAttribute("bogus_undeclared", "x")) };

            Assert.That(() => ProjectSerializer.Serialize(modified),
                Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("bogus_undeclared"));
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
                Root = project.Root with { Children = project.Root.Children.AsImmutableArray().SetItem(index, stripped) },
            };

            ProjectValidationResult result = ProjectVerification.Structural(broken);

            Assert.That(result.Findings.Any(f => f.RuleId == "attr-required"), Is.True,
                "findings: " + string.Join(" | ", result.Findings.Select(f => f.RuleId)));
        }
    }
}
