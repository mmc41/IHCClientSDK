namespace ihc_openvisual.ViewModels;

/// <summary>
/// fablerefac W3-7: the GUI-level classification of a tree row — the single source the projector sets once and from
/// which <see cref="TreeNodeViewModel"/>'s kind flags and its automation <c>NodeKind</c> string are computed. It is
/// FINER than the SDK <see cref="Ihc.Vis.ElementKind"/> (whose 17 tag-level members map every program-tree row to a
/// single <c>ProgramNode</c>, so an ElementKind switch cannot tell an <c>events</c> container from a <c>case</c>
/// switch): the labels in programming mode are user data, so the row's type has to be carried explicitly.
/// <para>A closed enum with a compile-checked <see cref="TreeNodeViewModel.NodeKind"/> mapping: adding a member is a
/// compile break at that switch until its automation string is defined (the task's "new kind ⇒ compile break").</para>
/// </summary>
public enum TreeNodeKind
{
    /// <summary>A row no construction site classified (maps to the <c>"unknown"</c> automation string).</summary>
    Unknown = 0,

    /// <summary>The synthetic <c>Localities</c> root — the <i>Insert locality</i> target (US-008).</summary>
    LocalitiesRoot,

    /// <summary>A locality (room) node.</summary>
    Locality,

    /// <summary>A product node.</summary>
    Product,

    /// <summary>A product's <c>scenes</c> container — a scenario-link target (US-024).</summary>
    Scenes,

    /// <summary>A scene member row under a <c>scenes</c> container.</summary>
    SceneMember,

    /// <summary>A function block node in configuration mode — the <i>Save block…</i> target (US-021).</summary>
    FunctionBlock,

    /// <summary>The block node at the head of a programming-mode <i>Programs</i> subtree. It renders like a function
    /// block but is NOT a <see cref="FunctionBlock"/> for command gating — it exists to root the program tree, so it
    /// carries none of the function-block flags (preserving the two sites' historical divergence).</summary>
    ProgramBlockRoot,

    /// <summary>A function-block variable section (<c>inputs</c>/<c>outputs</c>/<c>settings</c>/<c>internalsettings</c>) —
    /// the <i>Insert variable</i> target when it has a backing container (US-027).</summary>
    Section,

    /// <summary>A resource pin — a drag source/target for linking (US-022).</summary>
    Pin,

    /// <summary>A block's <c>Programs</c> container.</summary>
    Programs,

    /// <summary>A single program under <c>Programs</c>.</summary>
    Program,

    /// <summary>A program's <c>events</c> container — the <i>Add event</i> target (US-028).</summary>
    Events,

    /// <summary>A single program event.</summary>
    Event,

    /// <summary>A program's <c>actions</c> ("Commands") container — <i>Add command</i>/<i>Sub-program</i> (US-028/029).</summary>
    Commands,

    /// <summary>A single program command.</summary>
    Command,

    /// <summary>A conditional sub-program's true-branch command container.</summary>
    CommandsWhenTrue,

    /// <summary>A conditional sub-program's false-branch command container.</summary>
    CommandsWhenFalse,

    /// <summary>A conditional sub-program node.</summary>
    SubProgram,

    /// <summary>A <c>conditions</c> group — <i>Add condition</i>/<i>Logic group</i>/AND-OR toggle (US-029).</summary>
    Conditions,

    /// <summary>A nested logic group inside a conditions group.</summary>
    LogicGroup,

    /// <summary>A single condition row.</summary>
    Condition,

    /// <summary>A <c>program_case</c> switch — the <i>New case value…</i> target (US-031).</summary>
    Case,

    /// <summary>A case value branch (also a command container).</summary>
    CaseValue,

    /// <summary>A case default (Else) branch (also a command container).</summary>
    CaseElse,

    /// <summary>A "link from" row under a pin.</summary>
    LinkFrom,

    /// <summary>A "link to" row under a pin.</summary>
    LinkTo,

    /// <summary>A scene-link row under a pin.</summary>
    SceneLink,
}
