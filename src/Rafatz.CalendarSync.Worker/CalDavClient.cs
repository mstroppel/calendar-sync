using CalDAV;
using CalDAV.Models;
using CalDAV.Utils;

namespace Rafatz.CalendarSync;

public class CalDavClient(
    Uri _serverUrl,
    Uri _calendarUrl,
    string _username,
    string _password,
    ILogger<CalDavClient> _logger) : ICalDavClient
{
    private readonly CalDAVClient _client = new(_serverUrl.ToString(), _username, _password);
    private bool _initialized;
    
    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        await InitializeAsync();

        var events = await _client.GetEventsAsync(_calendarUrl.ToString(), start, end);
        return events;
    }

    public async Task DeleteEventAsync(string eventUrl, string etag, CancellationToken cancellationToken)
    {
        await InitializeAsync();

        await _client.DeleteEventAsync(eventUrl, etag);
        _logger.LogDebug("Deleted event {Url}", eventUrl);
    }

    public async Task CreateEventAsync(
        string summary,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken)
    {
        await InitializeAsync();

        var evt = new CalendarEvent
        {
            Uid = Guid.NewGuid().ToString(),
            Summary = summary,
            StartTime = startTime,
            EndTime = endTime,
        };

        var icsData = ICalendarGenerator.GenerateEvent(evt);
        var createdUrl = await _client.CreateEventAsync(_calendarUrl.ToString(), icsData);
        _logger.LogDebug("Created event '{Summary}' at {Url}", summary, createdUrl);
    }

    private async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }
        await _client.InitializeAsync();
        _initialized = true;
    }
}

public interface ICalDavClient
{
    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken);

    Task DeleteEventAsync(string eventUrl, string etag, CancellationToken cancellationToken);

    Task CreateEventAsync(
        string summary,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken);
}
