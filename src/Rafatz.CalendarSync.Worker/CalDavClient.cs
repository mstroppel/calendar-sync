using CalDAV;
using CalDAV.Models;
using CalDAV.Utils;

namespace Rafatz.CalendarSync;

public class CalDavClient(ILogger<CalDavClient> logger)
{
    // -------------------------------------------------------------------------
    // Fetch all events in [start, end) from a CalDAV calendar URL
    // -------------------------------------------------------------------------
    public async Task<IReadOnlyList<(string Url, string Etag, CalendarEvent Event)>> GetEventsAsync(
        string calendarUrl,
        string username,
        string password,
        DateTime start,
        DateTime end,
        CancellationToken ct)
    {
        var client = CreateClient(calendarUrl, username, password);
        await client.InitializeAsync();

        var events = await client.GetEventsAsync(calendarUrl, start, end);

        return events
            .Select(e => (e.Url ?? string.Empty, e.ETag ?? string.Empty, e))
            .ToList();
    }

    // -------------------------------------------------------------------------
    // Delete a single event by its URL + ETag
    // -------------------------------------------------------------------------
    public async Task DeleteEventAsync(
        string calendarUrl,
        string username,
        string password,
        string eventUrl,
        string etag,
        CancellationToken ct)
    {
        var client = CreateClient(calendarUrl, username, password);
        await client.InitializeAsync();

        await client.DeleteEventAsync(eventUrl, etag);
        logger.LogDebug("Deleted event {Url}", eventUrl);
    }

    // -------------------------------------------------------------------------
    // Create a new event in the calendar
    // -------------------------------------------------------------------------
    public async Task CreateEventAsync(
        string calendarUrl,
        string username,
        string password,
        string summary,
        DateTime startTime,
        DateTime endTime,
        CancellationToken ct)
    {
        var client = CreateClient(calendarUrl, username, password);
        await client.InitializeAsync();

        var evt = new CalendarEvent
        {
            Uid = Guid.NewGuid().ToString(),
            Summary = summary,
            StartTime = startTime,
            EndTime = endTime,
        };

        var icsData = ICalendarGenerator.GenerateEvent(evt);
        var createdUrl = await client.CreateEventAsync(calendarUrl, icsData);
        logger.LogDebug("Created event '{Summary}' at {Url}", summary, createdUrl);
    }

    // -------------------------------------------------------------------------
    private static CalDAVClient CreateClient(string url, string username, string password)
        => new(url, username, password);
}
