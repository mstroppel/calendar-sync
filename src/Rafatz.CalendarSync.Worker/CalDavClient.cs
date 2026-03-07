using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;

namespace Rafatz.CalendarSync;

public class CalDavClient
{
    private readonly HttpClient _http;
    private readonly ILogger<CalDavClient> _logger;

    private static readonly XNamespace Dav = "DAV:";
    private static readonly XNamespace Cal = "urn:ietf:params:xml:ns:caldav";

    public CalDavClient(HttpClient http, ILogger<CalDavClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // Fetch all events in [start, end) from a CalDAV calendar URL
    // -------------------------------------------------------------------------
    public async Task<IReadOnlyList<(string Href, CalendarEvent Event)>> GetEventsAsync(
        string calendarUrl,
        string username,
        string password,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken ct)
    {
        var body = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <C:calendar-query xmlns:D="DAV:" xmlns:C="urn:ietf:params:xml:ns:caldav">
              <D:prop>
                <D:getetag/>
                <C:calendar-data/>
              </D:prop>
              <C:filter>
                <C:comp-filter name="VCALENDAR">
                  <C:comp-filter name="VEVENT">
                    <C:time-range start="{start:yyyyMMdd'T'HHmmss'Z'}" end="{end:yyyyMMdd'T'HHmmss'Z'}"/>
                  </C:comp-filter>
                </C:comp-filter>
              </C:filter>
            </C:calendar-query>
            """;

        using var request = new HttpRequestMessage(new HttpMethod("REPORT"), calendarUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/xml"),
        };
        request.Headers.Add("Depth", "1");
        AddBasicAuth(request, username, password);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.MultiStatus)
        {
            _logger.LogError("CalDAV REPORT failed: {Status} {Url}", response.StatusCode, calendarUrl);
            response.EnsureSuccessStatusCode();
        }

        var xml = await response.Content.ReadAsStringAsync(ct);
        return ParseMultiStatus(xml);
    }

    // -------------------------------------------------------------------------
    // Delete a single event by its full href
    // -------------------------------------------------------------------------
    public async Task DeleteEventAsync(
        string baseUrl,
        string href,
        string username,
        string password,
        CancellationToken ct)
    {
        var uri = BuildUri(baseUrl, href);
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        AddBasicAuth(request, username, password);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent)
        {
            _logger.LogWarning("Delete failed for {Href}: {Status}", href, response.StatusCode);
        }
    }

    // -------------------------------------------------------------------------
    // Create a new event in the calendar
    // -------------------------------------------------------------------------
    public async Task CreateEventAsync(
        string calendarUrl,
        string username,
        string password,
        string summary,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken ct)
    {
        var uid = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;

        var calendar = new Ical.Net.Calendar();
        var evt = new CalendarEvent
        {
            Uid = uid,
            Summary = summary,
            DtStart = new CalDateTime(startTime.UtcDateTime, "UTC"),
            DtEnd = new CalDateTime(endTime.UtcDateTime, "UTC"),
            DtStamp = new CalDateTime(now.UtcDateTime, "UTC"),
        };
        calendar.Events.Add(evt);

        var serializer = new CalendarSerializer();
        var icsContent = serializer.SerializeToString(calendar);

        var eventUrl = calendarUrl.TrimEnd('/') + $"/{uid}.ics";
        using var request = new HttpRequestMessage(HttpMethod.Put, eventUrl)
        {
            Content = new StringContent(icsContent, Encoding.UTF8, "text/calendar"),
        };
        AddBasicAuth(request, username, password);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Created && response.StatusCode != HttpStatusCode.NoContent)
        {
            _logger.LogError("Create event failed: {Status} {Url}", response.StatusCode, eventUrl);
            response.EnsureSuccessStatusCode();
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private static void AddBasicAuth(HttpRequestMessage request, string username, string password)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
    }

    private static string BuildUri(string baseUrl, string href)
    {
        if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return href;

        var base_ = new Uri(baseUrl);
        return new Uri(base_, href).ToString();
    }

    private static IReadOnlyList<(string Href, CalendarEvent Event)> ParseMultiStatus(string xml)
    {
        var results = new List<(string, CalendarEvent)>();
        var doc = XDocument.Parse(xml);

        foreach (var response in doc.Descendants(Dav + "response"))
        {
            var href = response.Element(Dav + "href")?.Value ?? string.Empty;
            var calData = response.Descendants(Cal + "calendar-data").FirstOrDefault()?.Value;
            if (string.IsNullOrWhiteSpace(calData)) continue;

            var cal = Calendar.Load(calData);
            foreach (var evt in cal.Events)
                results.Add((href, evt));
        }

        return results;
    }
}
