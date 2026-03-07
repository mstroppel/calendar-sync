using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Rafatz.CalendarSync;

public class Worker(
    ILogger<Worker> logger,
    CalDavClient calDav,
    IOptions<CalendarSyncSettings> options) : BackgroundService
{
    private readonly CalendarSyncSettings _settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CalendarSync worker started.");
        logger.LogInformation(
            "Source: {SourceUrl} | Target: {TargetUrl} | Pattern: {Pattern} | DaysAhead: {Days} | PrependMinutes: {Prepend} | Interval: {Interval}min",
            _settings.SourceUrl,
            _settings.TargetUrl,
            _settings.SourceEventPattern,
            _settings.SyncDaysAhead,
            _settings.PrependMinutes,
            _settings.SyncIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sync cycle failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(_settings.SyncIntervalMinutes), stoppingToken);
        }

        logger.LogInformation("CalendarSync worker stopped.");
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        var pattern = new Regex(_settings.SourceEventPattern, RegexOptions.IgnoreCase);
        var today = DateTimeOffset.UtcNow.Date;

        logger.LogInformation("Starting sync for {Days} days from {Today}.", _settings.SyncDaysAhead, today);

        for (var dayOffset = 0; dayOffset < _settings.SyncDaysAhead; dayOffset++)
        {
            var day = today.AddDays(dayOffset);
            var dayStart = new DateTimeOffset(day, TimeSpan.Zero);
            var dayEnd = dayStart.AddDays(1);

            await SyncDayAsync(pattern, dayStart, dayEnd, ct);
        }

        logger.LogInformation("Sync complete.");
    }

    private async Task SyncDayAsync(
        Regex pattern,
        DateTimeOffset dayStart,
        DateTimeOffset dayEnd,
        CancellationToken ct)
    {
        // 1. Fetch matching source events for this day
        var sourceEvents = await calDav.GetEventsAsync(
            _settings.SourceUrl,
            _settings.SourceUsername,
            _settings.SourcePassword,
            dayStart,
            dayEnd,
            ct);

        var matchingEvents = sourceEvents
            .Where(e => pattern.IsMatch(e.Event.Summary ?? string.Empty))
            .OrderBy(e => e.Event.DtStart.AsDateTimeOffset)
            .ToList();

        // 2. Clean up existing target events for this day
        var targetEvents = await calDav.GetEventsAsync(
            _settings.TargetUrl,
            _settings.TargetUsername,
            _settings.TargetPassword,
            dayStart,
            dayEnd,
            ct);

        var staleTargetEvents = targetEvents
            .Where(e => string.Equals(
                e.Event.Summary?.Trim(),
                _settings.TargetEventName.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var stale in staleTargetEvents)
        {
            logger.LogInformation("Deleting stale target event on {Day}: {Href}", dayStart.Date, stale.Href);
            await calDav.DeleteEventAsync(
                _settings.TargetUrl,
                stale.Href,
                _settings.TargetUsername,
                _settings.TargetPassword,
                ct);
        }

        // 3. Create merged event if any matching source events exist
        if (matchingEvents.Count == 0)
        {
            logger.LogDebug("No matching events on {Day}, skipping.", dayStart.Date);
            return;
        }

        var firstStart = matchingEvents.First().Event.DtStart.AsDateTimeOffset;
        var lastEnd = matchingEvents.Max(e => e.Event.DtEnd?.AsDateTimeOffset ?? e.Event.DtStart.AsDateTimeOffset);

        var targetStart = firstStart.AddMinutes(-_settings.PrependMinutes);
        var targetEnd = lastEnd;

        logger.LogInformation(
            "Creating target event on {Day}: '{Name}' {Start} -> {End} (from {Count} source event(s))",
            dayStart.Date,
            _settings.TargetEventName,
            targetStart,
            targetEnd,
            matchingEvents.Count);

        await calDav.CreateEventAsync(
            _settings.TargetUrl,
            _settings.TargetUsername,
            _settings.TargetPassword,
            _settings.TargetEventName,
            targetStart,
            targetEnd,
            ct);
    }
}
