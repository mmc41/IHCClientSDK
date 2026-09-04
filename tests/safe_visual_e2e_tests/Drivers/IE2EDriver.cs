namespace safe_visual_e2e_tests;

/// <summary>
/// What a scenario in this suite is driven THROUGH. One verb in, one envelope out.
/// </summary>
/// <remarks>
/// <para>The seam exists because there are two ways to reach the application and they answer different
/// questions. <see cref="UiaDriver"/> launches the real <c>ihc_openvisual.exe</c> and drives it over Windows UI
/// Automation, which is the only thing that proves the Avalonia-to-UIA bridge, real focus, real pointer input
/// and the desktop's modal stack. <see cref="HeadlessDriver"/> runs the same window in-process on Avalonia's
/// headless backend, which proves the scenario paths and runs anywhere — including CI, which has no desktop
/// to take.</para>
///
/// <para><b>They are not interchangeable, and a green headless run does not stand in for a real one.</b> The
/// headless driver reads Avalonia's automation peers directly, so it cannot see a defect in the bridge that
/// projects those peers onto Windows UIA — the class of defect this suite was originally written for. Treat a
/// headless pass as "the scenario still works", never as "the application is driveable".</para>
///
/// <para><b>And the ROUTE differs, which is the subtler half.</b> The real driver reaches every outcome the way
/// a person does: a menu opened and a leaf invoked, a chord pressed, a pointer clicked at a real screen
/// position. The headless driver reaches several of the same outcomes by setting view-model state instead —
/// assigning the selected row, toggling a tier, invoking a command object. Such a verb may ARRANGE state, but
/// it can never answer which action triggers which response. So a headless pass says the scenario's OUTCOMES
/// still hold; only the real mode says a user's route still reaches them.</para>
/// </remarks>
internal interface IE2EDriver
{
    /// <summary>How a failure message names this mode.</summary>
    string Name { get; }

    /// <summary>
    /// Why this driver cannot run here, or null when it can. Read once, by the assembly's setup fixture, to
    /// decide whether to ignore the suite.
    /// </summary>
    /// <remarks>
    /// A property of the DRIVER because the requirement is the driver's: only the process driver needs Windows,
    /// and only because <c>pwsh</c> and UI Automation do. Asked of the assembly instead, the answer withheld the
    /// headless mode on the platforms it was built to reach.
    /// </remarks>
    string? UnmetRequirement { get; }

    /// <summary>Runs one verb. Never throws on a refusal — an <c>ok:false</c> envelope is a valid answer.</summary>
    E2E.Envelope Run(string[] args);

    /// <summary>
    /// Disposes of whatever is hosting the application, so the next fixture starts from nothing. Kills the real
    /// process in one mode and closes the window in the other.
    /// </summary>
    void KillApp();
}
