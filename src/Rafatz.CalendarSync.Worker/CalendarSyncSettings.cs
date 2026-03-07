namespace Rafatz.CalendarSync;

public class CalendarSyncSettings
{
    public const string SectionName = "CalendarSync";

    /// <summary>CalDAV URL of the source Nextcloud calendar.</summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>Username for the source calendar.</summary>
    public string SourceUsername { get; set; } = string.Empty;

    /// <summary>Password for the source calendar.</summary>
    public string SourcePassword { get; set; } = string.Empty;

    /// <summary>CalDAV URL of the target Nextcloud calendar.</summary>
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>Username for the target calendar.</summary>
    public string TargetUsername { get; set; } = string.Empty;

    /// <summary>Password for the target calendar.</summary>
    public string TargetPassword { get; set; } = string.Empty;

    /// <summary>Regex pattern matched against source event titles.</summary>
    public string SourceEventPattern { get; set; } = string.Empty;

    /// <summary>Title of the merged event written to the target calendar.</summary>
    public string TargetEventName { get; set; } = string.Empty;

    /// <summary>How many days ahead of today to sync (inclusive).</summary>
    public int SyncDaysAhead { get; set; } = 7;

    /// <summary>Minutes before the first matching event the target event should start.</summary>
    public int PrependMinutes { get; set; } = 0;

    /// <summary>How often the worker runs the sync, in minutes.</summary>
    public int SyncIntervalMinutes { get; set; } = 15;
}
