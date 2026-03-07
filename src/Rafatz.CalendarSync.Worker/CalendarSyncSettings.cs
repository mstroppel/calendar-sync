using System.ComponentModel.DataAnnotations;

namespace Rafatz.CalendarSync;

public class CalendarSyncSettings
{
    public const string SectionName = "CalendarSync";

    /// <summary>
    /// CalDAV URL of the Nextcloud server hosting the source calendar, e.g. https://nextcloud.example.com/remote.php/dav/ 
    /// </summary>
    [Required]
    public required Uri SourceServerUrl { get; init; }
    
    /// <summary>CalDAV URL of the source Nextcloud calendar.</summary>
    [Required]
    public required Uri SourceCalendarUrl { get; init; }

    /// <summary>Username for the source calendar.</summary>
    [Required(AllowEmptyStrings = false)]
    public string SourceUsername { get; init; } = string.Empty;

    /// <summary>Password for the source calendar.</summary>
    [Required(AllowEmptyStrings = false)]
    public string SourcePassword { get; init; } = string.Empty;

    /// <summary>
    /// CalDAV URL of the Nextcloud server hosting the target calendar, e.g. https://nextcloud.example.com/remote.php/dav/
    /// </summary>
    [Required]
    public required Uri TargetServerUrl { get; init; }
    
    /// <summary>CalDAV URL of the target Nextcloud calendar.</summary>
    [Required]
    public required Uri TargetCalendarUrl { get; init; }

    /// <summary>Username for the target calendar.</summary>
    [Required(AllowEmptyStrings = false)]
    public string TargetUsername { get; init; } = string.Empty;

    /// <summary>Password for the target calendar.</summary>
    [Required(AllowEmptyStrings = false)]
    public string TargetPassword { get; init; } = string.Empty;

    /// <summary>Regex pattern matched against source event titles.</summary>
    [Required(AllowEmptyStrings = false)]
    public string SourceEventPattern { get; init; } = string.Empty;

    /// <summary>Title of the merged event written to the target calendar.</summary>
    [Required(AllowEmptyStrings = false)]
    public string TargetEventName { get; init; } = string.Empty;

    /// <summary>How many days ahead of today to sync (inclusive).</summary>
    public int SyncDaysAhead { get; init; } = 7;

    /// <summary>Minutes before the first matching event the target event should start.</summary>
    public int PrependMinutes { get; init; } = 0;

    /// <summary>How often the worker runs the sync, in minutes.</summary>
    public int SyncIntervalMinutes { get; init; } = 15;
}
