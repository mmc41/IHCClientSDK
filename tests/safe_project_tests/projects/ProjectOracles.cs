namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The authentic-and-derived <c>.vis</c> corpus the whole-file gates run over: the vendor originals
    /// (<c>Project0-Tomt</c> … <c>Project6-Errors</c>) together with the post-edit oracles derived from them
    /// (<c>-copied</c>, <c>-mutated</c>, <c>-enumvalues</c>, <c>-projektinfo</c>, <c>-scenelinks</c>,
    /// <c>-enumappend</c>, <c>control-save</c>, <c>-refdelete</c>, <c>-logicgroups</c>, <c>-case</c>,
    /// <c>-gemsideeffect</c>). Every entry round-trips byte-identically, which is what lets a gate serialize
    /// one and still be measuring the vendor's own bytes.
    /// </summary>
    /// <remarks>
    /// One list behind <c>[TestCaseSource]</c> rather than a <c>[TestCase]</c> block per fixture, so adding an
    /// oracle extends every gate that claims to cover the corpus instead of only the fixture it was added to.
    /// A gate that deliberately covers more than the corpus keeps its extra cases beside its own test.
    /// </remarks>
    internal static class ProjectOracles
    {
        internal static readonly string[] All =
        [
            "Project0-Tomt.vis",
            "Project1-SimpelWired.vis",
            "project2-CustomBlock.vis",
            "project2-control-save.vis",
            "project2-CustomBlock-refdelete.vis",
            "project2-CustomBlock-logicgroups.vis",
            "project2-CustomBlock-case.vis",
            "project3-KompleksWired.vis",
            "project3-KompleksWired-copied.vis",
            "project3-KompleksWired-mutated.vis",
            "project3-KompleksWired-enumvalues.vis",
            "project3-KompleksWired-projektinfo.vis",
            "project3-KompleksWired-scenelinks.vis",
            "project3-KompleksWired-enumappend.vis",
            "project3-KompleksWired-gemsideeffect.vis",
            "project4-PrgTokens.vis",
            "project4-PrgTokens-round2.vis",
            "project5-Dokumentation.vis",
            "Project6-Errors.vis",
        ];
    }
}
