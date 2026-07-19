namespace safe_project_tests;

/// <summary>
/// fablerefac W1-1: proves the C# 14 extension-member read surface (<see cref="Ihc.Vis.ProjectElementRead"/>)
/// resolves under this repo's SDK — an extension <em>property</em> accessed with no parentheses returns the same
/// value as the underlying member. If this class fails to compile, the parenthesis-free read surface is not
/// available and the fallback (classic static extension methods) must be signed off before Wave 1 proceeds.
/// </summary>
public class ExtensionMemberSpikeTests
{
    [Test]
    public void SpikeTag_ExtensionProperty_EqualsUnderlyingTag()
    {
        ProjectElement element = ProjectElement.Create("group", null, [], []);

        Assert.Multiple(() =>
        {
            Assert.That(element.SpikeTag, Is.EqualTo(element.Tag), "the extension property echoes the underlying Tag");
            Assert.That(element.SpikeTag, Is.EqualTo("group"), "and returns the actual tag value");
        });
    }
}
