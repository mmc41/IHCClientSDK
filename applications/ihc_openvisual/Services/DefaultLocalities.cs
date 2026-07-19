using System.Collections.Generic;
using System.Diagnostics;
using ihc_openvisual.Configuration;
using Ihc.Vis;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ihc_openvisual.Services;

/// <summary>
/// OpenVisual's English new-project template. The SDK's <see cref="Ihc.Vis.ProjectAppService.CreateNew"/> seeds
/// the vendor's authentic Danish default localities (Stue, Køkken, …) — byte-identical to IHC Visual's empty
/// project. Because English is OpenVisual's product language (product.md), a project OpenVisual <i>authors</i>
/// starts from English room names, while a <i>loaded</i> file keeps its own names verbatim. This renames the ten
/// default localities in place (an attribute edit — no ids allocated, tree structure unchanged); it is applied
/// only to a freshly created project, never on load.
/// </summary>
public static class DefaultLocalities
{
    /// <summary>The ten default localities, in the fixed vendor order, in OpenVisual's product language.</summary>
    public static readonly IReadOnlyList<string> English = new[]
    {
        "Living room", "Hall", "Kitchen", "Bedroom", "Room",
        "Bathroom", "Utility room", "Garage", "Basement", "Outdoors",
    };

    /// <summary>
    /// Returns <paramref name="project"/> with its ten default localities renamed to <see cref="English"/> by
    /// position. A no-op (the project is returned unchanged) unless it holds exactly the ten-locality default
    /// skeleton, so a loaded or already-customised project is never rewritten.
    /// </summary>
    public static Project ApplyEnglish(Project project, ILogger? logger = null)
    {
        using Activity? activity = Telemetry.ActivitySource.StartActivity($"{nameof(DefaultLocalities)}.{nameof(ApplyEnglish)}");
        IReadOnlyList<ProjectElement> groups = project.Groups;
        activity?.SetTag("localities.count", groups.Count);
        if (groups.Count != English.Count)
        {
            // Not the standard ten-locality template — leave it exactly as the SDK produced it.
            return project;
        }

        ProjectEditor editor = project.Edit();
        for (int i = 0; i < English.Count; i++)
        {
            string current = project.View(groups[i]).Name ?? string.Empty;
            if (current.Length == 0)
            {
                continue;   // an unnamed group is not a default room; Group("") would seed a new one
            }
            editor.Group(current).Name(English[i]);
        }
        (logger ?? NullLogger.Instance).LogDebug("Applied English default localities to a new project");
        return editor.ToProject();
    }
}
