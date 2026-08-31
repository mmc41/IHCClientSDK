using System;
using System.IO;
using Ihc.Vis.Projects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ihc_openvisual.Configuration;

namespace ihc_openvisual.Services;

/// <summary>
/// The installer's own contact details, held as an application setting and stamped into every new project's
/// <c>installer_info</c> (US-002). Only the installer identity is remembered: the <i>programmer</i> is whoever is
/// signed in, so it is read from the OS user rather than stored. Persisted as JSON in the user's app-data
/// directory, Avalonia-free and testable.
/// </summary>
public sealed class InstallerIdentityStore
{
    private readonly string _filePath;
    private readonly ILogger _logger;

    public InstallerIdentityStore(string filePath, string? userName = null, ILoggerFactory? loggerFactory = null)
    {
        _filePath = filePath;
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<InstallerIdentityStore>();
        Programmer = string.IsNullOrWhiteSpace(userName) ? Environment.UserName : userName;
        Identity = Load();
    }

    public static InstallerIdentityStore CreateDefault(ILoggerFactory? loggerFactory = null) =>
        new(DefaultFilePath(), loggerFactory: loggerFactory);

    public static string DefaultFilePath() => Constants.AppDataPath("installer.json");

    public event EventHandler? Changed;

    /// <summary>The stored installer contact details; every field is optional.</summary>
    public InstallerIdentity Identity { get; private set; }

    /// <summary>The signed-in user, written as the new project's programmer.</summary>
    public string Programmer { get; }

    public void Update(InstallerIdentity identity)
    {
        Identity = identity ?? new InstallerIdentity();
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The metadata a File→New project is created with.</summary>
    public ProjectDetails NewProjectDetails() =>
        new(Programmer, Identity.Name ?? string.Empty, Identity.Country ?? string.Empty)
        {
            InstallerAddress = Blank(Identity.Address),
            InstallerCity = Blank(Identity.City),
            InstallerZipCode = Blank(Identity.ZipCode),
            InstallerPhone = Blank(Identity.Phone),
            InstallerMobilePhone = Blank(Identity.MobilePhone),
            InstallerEmail = Blank(Identity.Email),
        };

    // An unset field must not reach the file at all, so blanks collapse back to "not written".
    private static string? Blank(string? value) => string.IsNullOrEmpty(value) ? null : value;

    // A corrupt or unreadable setting must not stop the app starting; an empty identity is the fallback.
    private InstallerIdentity Load() =>
        JsonPreferenceStore.TryLoad<InstallerIdentity>(_filePath, _logger) ?? new InstallerIdentity();

    private void Save() => JsonPreferenceStore.TrySave(_filePath, Identity, _logger);

}

/// <summary>The installer contact fields IHC Visual keeps as an application setting (<c>installer_info</c>).</summary>
public sealed record InstallerIdentity
{
    public string? Name { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? ZipCode { get; init; }
    public string? Country { get; init; }
    public string? Phone { get; init; }
    public string? MobilePhone { get; init; }
    public string? Email { get; init; }
}
