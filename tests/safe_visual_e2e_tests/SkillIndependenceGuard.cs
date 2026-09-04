using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
// GetNextHandle is an EXTENSION method on MetadataReader, declared here rather than on the reader itself.
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// THE INDEPENDENCE RULE, as a gate. This suite is pure .NET and reaches nothing under <c>.claude</c>: the
/// <c>aui-openvisual</c> skill is a development tool a person drives by hand, and the suite drives the
/// application itself. Neither may become the other's dependency.
///
/// <para>That was a convention, and a convention is what the last edge was added under. This fixture reads the
/// compiled assembly's USER-STRING HEAP — every string literal the compiler emitted — and reports which of them
/// name a path under the skill directory. A new edge is a new literal, so it shows up here by name rather than
/// by review.</para>
///
/// <para><b>Why it lives in this suite and not in <c>safe_architecture_tests</c>.</b> That suite references the
/// PRODUCT, not its sibling test assemblies, so it cannot see this one at all — the same reason
/// <c>ControllerReachScan</c> is compiled into each suite it polices rather than run once from outside. A rule
/// about an assembly has to ship inside it.</para>
///
/// <para><b>What the scan does not see.</b> Only the user-string heap: literals reachable by <c>ldstr</c>.
/// Attribute arguments live in the BLOB heap, XML doc comments are not in the assembly at all, and a path
/// assembled from fragments at run time is invisible by construction. The scan is therefore a floor, not a
/// proof — it catches the shape an edge actually takes (a literal path segment), and the reverse direction
/// (the skill reaching into <c>tests/</c>) is not checkable from here and stays a reviewed convention.</para>
///
/// <para>Neither test is <c>DesktopOnly</c>: both read a file and run on every push, in CI's headless leg.</para>
/// </summary>
[TestFixture]
public sealed class SkillIndependenceGuard
{
    /// <summary>
    /// The control for the scan. A literal declared here and nowhere else, so finding it proves the heap was
    /// really read — without it, a scan that silently returned nothing would satisfy the ban test forever.
    /// Held as <c>static readonly</c> rather than <c>const</c> so exactly one <c>ldstr</c> emits it.
    /// </summary>
    internal static readonly string ArmedMarker = "SkillIndependenceGuard.ArmedMarker";

    /// <summary>
    /// The skill directory's name, assembled at run time. Written as one literal it would be in the very heap
    /// this fixture scans, and the guard would report itself. Roslyn folds constant expressions of literals, so
    /// this has to be a method call and not a <c>+</c> chain.
    /// </summary>
    private static string SkillDirectoryName() => string.Concat(".", "cla", "ude");

    /// <summary>Every string literal the compiler emitted into this assembly.</summary>
    private static List<string> UserStringsOfThisSuite()
    {
        string location = typeof(SkillIndependenceGuard).Assembly.Location;
        using FileStream stream = File.OpenRead(location);
        using PEReader pe = new(stream);
        MetadataReader reader = pe.GetMetadataReader();

        // The walk starts from the NIL handle, which is what yields the first entry. The handle has to be a
        // typed local: GetNextHandle is overloaded for three heaps, and a bare `default` argument is ambiguous
        // between them.
        List<string> literals = [];
        UserStringHandle handle = default;
        while (!(handle = reader.GetNextHandle(handle)).IsNil)
        {
            literals.Add(reader.GetUserString(handle));
        }

        return literals;
    }

    private static List<string> LiteralsNamingTheSkillDirectory()
    {
        string needle = SkillDirectoryName();
        return UserStringsOfThisSuite()
            .Where(literal => literal.Contains(needle, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// The rule: this suite names NO path under the skill directory.
    /// </summary>
    /// <remarks>
    /// A ban, as of the deletion of the process driver that used to build the skill's script path. It was an
    /// inventory of that one edge while the edge existed, so that a SECOND one would fail immediately rather
    /// than wait for the deletion; with the edge gone, the expected set is empty and the rule is absolute.
    /// </remarks>
    [Test]
    public void ThisSuiteNamesNoPathUnderTheSkillDirectory()
    {
        List<string> matches = LiteralsNamingTheSkillDirectory();

        Assert.That(matches, Is.Empty,
            "this suite must not depend on the aui-openvisual skill: it drives the application through its own "
            + "real-UIA driver, and the skill is a hand-driven development tool that must stay free to change. "
            + "Each literal listed above is an edge into it: ["
            + string.Join(", ", matches)
            + "]");
    }

    /// <summary>
    /// The scan is armed. Proves the enumeration actually reaches this assembly's literals, so an empty result
    /// above means "no edge" rather than "nothing was read".
    /// </summary>
    [Test]
    public void TheUserStringScanIsArmed()
    {
        Assert.That(UserStringsOfThisSuite(), Does.Contain(ArmedMarker),
            "the user-string scan found none of this fixture's own literals, so it is reading nothing and the "
            + "independence rule above is vacuous");
    }
}
