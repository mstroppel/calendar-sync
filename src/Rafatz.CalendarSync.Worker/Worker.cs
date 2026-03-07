using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Rafatz.CalendarSync;

public class Worker(
    ILogger<Worker> _logger,
    ISourceCalDavClient _sourceCalDav,
    ITargetCalDavClient _targetCalDav,
    IOptions<CalendarSyncSettings> _options) : BackgroundService
{
    private readonly CalendarSyncSettings _settings = _options.Value;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CalendarSync worker started");
        _logger.LogInformation(
            "Source: {SourceUrl} | Target: {TargetUrl} | Pattern: {Pattern} | DaysAhead: {Days} | PrependMinutes: {Prepend} | Interval: {Interval}min",
            _settings.SourceCalendarUrl,
            _settings.TargetCalendarUrl,
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
                _logger.LogError(ex, "Sync cycle failed");
            }

            _logger.LogDebug("Waiting for {Minutes} minutes until next sync cycle", _settings.SyncIntervalMinutes);
            await Task.Delay(TimeSpan.FromMinutes(_settings.SyncIntervalMinutes), stoppingToken);
        }

        _logger.LogInformation("CalendarSync worker stopped");
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        var pattern = new Regex(_settings.SourceEventPattern, RegexOptions.IgnoreCase);
        var today = DateTime.UtcNow.Date;

        _logger.LogInformation("Starting sync for {Days} days from {Today}", _settings.SyncDaysAhead, today);

        for (var dayOffset = 0; dayOffset < _settings.SyncDaysAhead; dayOffset++)
        {
            var dayStart = today.AddDays(dayOffset);
            var dayEnd = dayStart.AddDays(1);

            await SyncDayAsync(pattern, dayStart, dayEnd, ct);
        }

        _logger.LogInformation("Sync complete");
    }

    private async Task SyncDayAsync(
        Regex pattern,
        DateTime dayStart,
        DateTime dayEnd,
        CancellationToken ct)
    {
        var sourceEvents = await _sourceCalDav.GetEventsAsync(dayStart, dayEnd, ct);

        var matchingEvents = sourceEvents
            .Where(e => pattern.IsMatch(e.Summary))
            .OrderBy(e => e.StartTime)
            .ToList();

        var targetEvents = await _targetCalDav.GetEventsAsync(dayStart, dayEnd, ct);

        var existingTargetEvents = targetEvents
            .Where(e => string.Equals(
                e.Summary.Trim(),
                _settings.TargetEventName.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        if (matchingEvents.Count == 0)
        {
            _logger.LogDebug("No matching events on {Day}, skipping", dayStart.Date);
            foreach (var existingTargetEvent in existingTargetEvents)
            {
                await _targetCalDav.DeleteEventAsync(existingTargetEvent.Href, existingTargetEvent.ETag, ct);
            }
            return;
        }

        var firstStart = matchingEvents.First().StartTime;
        var lastEnd = matchingEvents.Max(e => e.EndTime);

        var targetStart = firstStart.AddMinutes(-_settings.PrependMinutes);
        var targetEnd = lastEnd;


        var staleTargetEvents = existingTargetEvents
            .Where(e => e.StartTime != targetStart || e.EndTime != targetEnd)
            .ToList();

        foreach (var stale in staleTargetEvents)
        {
            _logger.LogInformation("Deleting stale target event on {Day} ({Start} to {End})", dayStart.Date, stale.StartTime, stale.EndTime);
            await _targetCalDav.DeleteEventAsync(stale.Href, stale.ETag, ct);
        }

        var upToDateEvent = existingTargetEvents.FirstOrDefault(e => e.StartTime == targetStart && e.EndTime == targetEnd);
        
        if (upToDateEvent is not null)
        {
            _logger.LogDebug(
                "Target event on {Day} already up to date ({Start} -> {End}), skipping",
                dayStart.Date, targetStart, targetEnd);
            return;
        }

        _logger.LogInformation(
            "Creating target event on {Day}: '{Name}' {Start} -> {End} (from {Count} source event(s))",
            dayStart.Date,
            _settings.TargetEventName,
            targetStart,
            targetEnd,
            matchingEvents.Count);

        await _targetCalDav.CreateEventAsync(_settings.TargetEventName, targetStart, targetEnd, ct);
    }
}
