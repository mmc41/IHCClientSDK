using System;
using System.Linq;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// fablerefac W3-7: the row's structural type is a single <see cref="TreeNodeKind"/>; the kind flags and the
/// automation <see cref="TreeNodeViewModel.NodeKind"/> string are COMPUTED from it. These pin the mapping
/// (equivalence-partition over one node per kind) and guard exhaustiveness — a new enum member with no
/// <c>NodeKind</c> arm fails the exhaustiveness test (the practical stand-in for a compile break, which C#'s
/// CS8524-under-warnings-as-errors forces a throwing default arm to suppress).
/// </summary>
public class TreeNodeKindTests
{
    private static TreeNodeViewModel Node(TreeNodeKind kind, string? detail = null, ElementId? id = null) =>
        new("row", "/Assets/x.svg", elementId: id) { Kind = kind, KindDetail = detail };

    // Kind -> the exact automation string it must reproduce (the vendor-parameterised pin/section forms included).
    private static readonly (TreeNodeKind Kind, string? Detail, string NodeKind)[] Expected =
    {
        (TreeNodeKind.LocalitiesRoot, null, "localitiesRoot"),
        (TreeNodeKind.Locality, null, "locality"),
        (TreeNodeKind.Product, null, "product"),
        (TreeNodeKind.Scenes, null, "scenes"),
        (TreeNodeKind.SceneMember, null, "sceneMember"),
        (TreeNodeKind.FunctionBlock, null, "functionBlock"),
        (TreeNodeKind.ProgramBlockRoot, null, "functionBlock"),
        (TreeNodeKind.Section, "inputs", "section:inputs"),
        (TreeNodeKind.Pin, "dataline_input", "pin:dataline_input"),
        (TreeNodeKind.Programs, null, "programs"),
        (TreeNodeKind.Program, null, "program"),
        (TreeNodeKind.Events, null, "events"),
        (TreeNodeKind.Event, null, "event"),
        (TreeNodeKind.Commands, null, "commands"),
        (TreeNodeKind.Command, null, "command"),
        (TreeNodeKind.CommandsWhenTrue, null, "commandsWhenTrue"),
        (TreeNodeKind.CommandsWhenFalse, null, "commandsWhenFalse"),
        (TreeNodeKind.SubProgram, null, "subProgram"),
        (TreeNodeKind.Conditions, null, "conditions"),
        (TreeNodeKind.LogicGroup, null, "logicGroup"),
        (TreeNodeKind.Condition, null, "condition"),
        (TreeNodeKind.Case, null, "case"),
        (TreeNodeKind.CaseValue, null, "caseValue"),
        (TreeNodeKind.CaseElse, null, "caseElse"),
        (TreeNodeKind.LinkFrom, null, "linkFrom"),
        (TreeNodeKind.LinkTo, null, "linkTo"),
        (TreeNodeKind.SceneLink, null, "sceneLink"),
        (TreeNodeKind.Unknown, null, "unknown"),
    };

    [Test]
    public void EveryKind_ReproducesItsAutomationString()
    {
        Assert.Multiple(() =>
        {
            foreach ((TreeNodeKind kind, string? detail, string nodeKind) in Expected)
            {
                Assert.That(Node(kind, detail).NodeKind, Is.EqualTo(nodeKind), $"NodeKind for {kind}");
            }
        });
    }

    // Exhaustiveness guard (the compile-break stand-in): every declared TreeNodeKind must have a NodeKind arm, so a
    // new member added without one fails here instead of silently hitting the throwing default at runtime.
    [Test]
    public void EveryDeclaredKind_HasANodeKindMapping()
    {
        Assert.Multiple(() =>
        {
            foreach (TreeNodeKind kind in Enum.GetValues<TreeNodeKind>())
            {
                Assert.That(() => Node(kind, "x").NodeKind, Throws.Nothing, $"{kind} has no NodeKind mapping");
            }
        });
    }

    // The kind flags derive from Kind — one representative true case each, plus a partition that must read false.
    [Test]
    public void KindFlags_ClassifyByKind()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Node(TreeNodeKind.FunctionBlock).IsFunctionBlock, Is.True);
            Assert.That(Node(TreeNodeKind.ProgramBlockRoot).IsFunctionBlock, Is.False, "the program block root is not a Save-block target (historical divergence)");
            Assert.That(Node(TreeNodeKind.Pin, "resource_output").IsPin, Is.True);
            Assert.That(Node(TreeNodeKind.Scenes).IsSceneTarget, Is.True);
            Assert.That(Node(TreeNodeKind.Events).IsEventsContainer, Is.True);
            Assert.That(Node(TreeNodeKind.CaseValue).IsCommandsContainer, Is.True, "a case value branch is a command container");
            Assert.That(Node(TreeNodeKind.LogicGroup).IsConditionsContainer, Is.True);
            Assert.That(Node(TreeNodeKind.Case).IsCaseNode, Is.True);
            Assert.That(Node(TreeNodeKind.SceneMember).IsLinkRow, Is.True);
            Assert.That(Node(TreeNodeKind.LocalitiesRoot).IsLocalitiesRoot, Is.True);
            Assert.That(Node(TreeNodeKind.Locality).CanCut, Is.True);
            Assert.That(Node(TreeNodeKind.Pin, "x").CanCut, Is.True, "an editable function-block variable is cut/copyable");
            Assert.That(new TreeNodeViewModel("catalog", "/Assets/x.svg")
            {
                Kind = TreeNodeKind.Pin,
                IsCatalogPin = true,
            }.CanCut, Is.False, "a catalog pin remains protected");
        });
    }

    // IsBlockSection needs a backing container: a section WITH an element id is insertable, one WITHOUT is not.
    [Test]
    public void BlockSection_RequiresABackingContainer()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Node(TreeNodeKind.Section, "inputs", new ElementId(0x10, 1)).IsBlockSection, Is.True);
            Assert.That(Node(TreeNodeKind.Section, "inputs").IsBlockSection, Is.False, "a section with no backing container element can't accept a variable");
            Assert.That(Node(TreeNodeKind.Section, "inputs", new ElementId(0x10, 1)).SectionTag, Is.EqualTo("inputs"));
        });
    }
}
