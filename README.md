# calendar-sync

Will search for calendar events in source calendar and create events in another calendar.

## Usage

```yaml

services:
  calendar-sync:
    image: ghcr.io/mstroppel/rafatz-calendar-sync:latest
    restart: unless-stopped
    environment:
      - SOURCE_SERVER_URL="https://my.nexcloud.com/remote.php/dav/"
      - SOURCE_CALENDAR_URL="https://my.nexcloud.com/remote.php/dav/calendars/your_name/your_source_calendar_id/"
      - SOURCE_USERNAME= "your_username"
      - SOURCE_PASSWORD= "your_password"
      - TARGET_SERVER_URL="https://my.nexcloud.com/remote.php/dav/"
      - TARGET_CALENDAR_URL="https://my.nexcloud.com/remote.php/dav/calendars/your_name/your_target_calendar_id/"
      - TARGET_USERNAME="your_email"
      - TARGET_PASSWORD="your_password"
      - SOURCE_EVENT_PATTERN=".*[SCH|STR|FA].*"
      - TARGET_EVENT_NAME=Sammeltermin
      - SYNC_DAYS_AHEAD=7
      - PREPEND_MINUTES=0
      - SYNC_INTERVAL_MINUTES=15

```

Parameters:

- SOURCE_PASSWORD and TARGET_PASSWORD: Password for source calendar authentication - use app password if 2FA is enabled
- SOURCE_EVENT_PATTERN: Regular expression pattern to match event titles in source calendar (default: ".*[SCH|STR|FA].*")
- TARGET_EVENT_NAME: Name of the event to create in target calendar (default: "Sammeltermin")

Optional parameters:

- SYNC_DAYS_AHEAD: Number of days ahead to search for events in source calendar (default: 7)
- PREPEND_MINUTES: Number of minutes to prepend to event start time (default: 0)
- SYNC_INTERVAL_MINUTES: Interval in minutes to run the sync (default: 15)

## Logging Integration

The calendar-sync service logs its output to the console as JSON. So it can be easily integrated with logging systems like Grafana Loki.

Logs are structured as follows:

```json
{
  "EventId": 0,
  "LogLevel": "Error",
  "Category": "Rafatz.CalendarSync.CalendarSyncWorker",
  "Message": "Sync cycle failed",
  "Exception": "System.ArgumentException: Exception message\n   at Rafatz.CalendarSync.CalendarSyncWorker.ExecuteAsync(CancellationToken stoppingToken) in D:\\github\\calendar-sync\\src\\Rafatz.CalendarSync.Worker\\CalendarSyncWorker.cs:line 30",
  "State": {
    "{OriginalFormat}": "Sync cycle failed"
  }
}
```