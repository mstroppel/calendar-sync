using System.ComponentModel.DataAnnotations;

namespace Rafatz.CalendarSync;

public class CalendarSyncSettings
{
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
    public required string SourceUsername { get; init; }

    /// <summary>Password for the source calendar.</summary>
    [Required(AllowEmptyStrings = false)]
    public required string SourcePassword { get; init; }

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
    public required string TargetUsername { get; init; }

    /// <summary>Password for the target calendar.</summary>
    [Required(AllowEmptyStrings = false)]
    public required string TargetPassword { get; init; }

    /// <summary>Regex pattern matched against source event titles.</summary>
    [Required(AllowEmptyStrings = false)]
    public required string SourceEventPattern { get; init; }

    /// <summary>Title of the merged event written to the target calendar.</summary>
    [Required(AllowEmptyStrings = false)]
    public required string TargetEventName { get; init; }

    /// <summary>How many days ahead of today to sync (inclusive).</summary>
    public required int SyncDaysAhead { get; init; }

    /// <summary>Minutes before the first matching event the target event should start.</summary>
    public required int PrependMinutes { get; init; }

    /// <summary>How often the worker runs the sync, in minutes.</summary>
    public required int SyncIntervalMinutes { get; init; }
}
