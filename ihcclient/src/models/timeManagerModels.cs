using System;

public record TimeManagerSettings
{
    public bool SynchroniseTimeAgainstServer { get; init; }

    public bool UseDST { get; init; }

    public int GmtOffsetInHours { get; init; }

    public string ServerName { get; init; }

    public int SyncIntervalInHours { get; init; }

    public DateTimeOffset? TimeAndDateInUTC { get; init; }

    public bool OnlineCalendarUpdateOnline { get; init; }

    public string OnlineCalendarCountry { get; init; }

    public int OnlineCalendarValidUntil { get; init; }
}

public record TimeServerConnectionResult
{
    public bool ConnectionWasSuccessful { get; init; }

    public DateTimeOffset? DateFromServer { get; init; }

    public bool ConnectionFailedDueToUnknownHost { get; init; }

    public bool ConnectionFailedDueToOtherErrors { get; init; }
}
