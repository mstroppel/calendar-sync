using CalDAV;
using CalDAV.Models;
using CalDAV.Utils;

namespace Rafatz.CalendarSync;

public class CalDavClient(
    Uri _serverUrl,
    Uri _calendarUrl,
    string _username,
    string _password,
    HttpClient _httpClient,
    ILogger<CalDavClient> _logger) : ISourceCalDavClient, ITargetCalDavClient
{
    private readonly CalDAVClient _client = new(_serverUrl.ToString(), _username, _password, _httpClient);
    private bool _initialized;
    
    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);

        var events = await _client.GetEventsAsync(_calendarUrl.ToString(), start, end, cancellationToken);
        return events;
    }

    public async Task DeleteEventAsync(string eventHref, string etag, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        
        var eventUrl = $"{_serverUrl.Scheme}://{_serverUrl.Host}{eventHref}"; 

        await _client.DeleteEventAsync(eventUrl, etag, cancellationToken);
        _logger.LogDebug("Deleted event {Url}", eventUrl);
    }

    public async Task CreateEventAsync(
        string summary,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);

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

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }
        await _client.InitializeAsync(cancellationToken);
        _initialized = true;
    }
}

public interface ICalDavClient
{
    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken);

    Task DeleteEventAsync(string eventHref, string etag, CancellationToken cancellationToken);

    Task CreateEventAsync(
        string summary,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken);
}

public interface ISourceCalDavClient : ICalDavClient
{
}

public interface ITargetCalDavClient : ICalDavClient
{
}