#nullable enable
using Ihc.Vis.Problems;

namespace Ihc.App
{
    /// <summary>
    /// The identity of every condition that stops a project crossing the controller bridge, ready to raise.
    /// <para>
    /// The two directions are two OPERATIONS, not one — <c>bridge.download</c> and <c>bridge.upload</c> — because
    /// what a user does next differs completely: an empty controller means there is nothing to fetch, while a
    /// declined store means the controller's own project state is now uncertain. Folding both under one head
    /// would put those two behind the same filter.
    /// </para>
    /// <para>
    /// They live beside the bridge rather than in the <c>.vis</c> engine because that is where they are raised:
    /// the engine reads and writes files and knows nothing about a controller.
    /// </para>
    /// </summary>
    public static class BridgeRefusalCodes
    {
        /// <summary>The controller holds no stored project to download.</summary>
        public static RefusalIdentity ControllerNoProject { get; } = new(
            OperationCodes.BridgeDownload, OperationCodes.BridgeDownloadLabel,
            new ProblemCode("import-controller-no-project"), "Intet projekt på controlleren");

        /// <summary>The controller declined to store the uploaded project.</summary>
        public static RefusalIdentity ControllerDeclined { get; } = new(
            OperationCodes.BridgeUpload, OperationCodes.BridgeUploadLabel,
            new ProblemCode("export-controller-declined"), "Controlleren afviste projektet");
    }
}
