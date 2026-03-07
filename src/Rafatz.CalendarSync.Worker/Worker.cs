using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Rafatz.CalendarSync;

public class Worker(
    ILogger<Worker> logger,
    SourceCalDavClient sourceCalDav,
    TargetCalDavClient targetCalDav,
    IOptions<CalendarSyncSettings> options) : BackgroundService
{
    private readonly CalendarSyncSettings _settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CalendarSync worker started");
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
                logger.LogError(ex, "Sync cycle failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(_settings.SyncIntervalMinutes), stoppingToken);
        }

        logger.LogInformation("CalendarSync worker stopped");
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        var pattern = new Regex(_settings.SourceEventPattern, RegexOptions.IgnoreCase);
        var today = DateTime.UtcNow.Date;

        logger.LogInformation("Starting sync for {Days} days from {Today}", _settings.SyncDaysAhead, today);

        for (var dayOffset = 0; dayOffset < _settings.SyncDaysAhead; dayOffset++)
        {
            var dayStart = today.AddDays(dayOffset);
            var dayEnd = dayStart.AddDays(1);

            await SyncDayAsync(pattern, dayStart, dayEnd, ct);
        }

        logger.LogInformation("Sync complete");
    }

    private async Task SyncDayAsync(
        Regex pattern,
        DateTime dayStart,
        DateTime dayEnd,
        CancellationToken ct)
    {
        // 1. Fetch matching source events for this day
        var sourceEvents = await sourceCalDav.GetEventsAsync(dayStart, dayEnd, ct);

        var matchingEvents = sourceEvents
            .Where(e => pattern.IsMatch(e.Summary ?? string.Empty))
            .OrderBy(e => e.StartTime)
            .ToList();

        // 2. Clean up existing target events for this day
        var targetEvents = await targetCalDav.GetEventsAsync(dayStart, dayEnd, ct);

        var staleTargetEvents = targetEvents
            .Where(e => string.Equals(
                e.Summary?.Trim(),
                _settings.TargetEventName.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var stale in staleTargetEvents)
        {
            logger.LogInformation("Deleting stale target event on {Day}: {Url}", dayStart.Date, stale.Href);
            await targetCalDav.DeleteEventAsync(stale.Href, stale.ETag, ct);
        }

        // 3. Create merged event if any matching source events exist
        if (matchingEvents.Count == 0)
        {
            logger.LogDebug("No matching events on {Day}, skipping", dayStart.Date);
            return;
        }

        var firstStart = matchingEvents.First().StartTime;
        var lastEnd = matchingEvents.Max(e => e.EndTime);

        var targetStart = firstStart.AddMinutes(-_settings.PrependMinutes);
        var targetEnd = lastEnd;

        logger.LogInformation(
            "Creating target event on {Day}: '{Name}' {Start} -> {End} (from {Count} source event(s))",
            dayStart.Date,
            _settings.TargetEventName,
            targetStart,
            targetEnd,
            matchingEvents.Count);

        await targetCalDav.CreateEventAsync(_settings.TargetEventName, targetStart, targetEnd, ct);
    }
}
