using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Rafatz.CalendarSync;

public class CalendarSyncWorker(
    ILogger<CalendarSyncWorker> _logger,
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

            _logger.LogInformation("Waiting for {Minutes} minutes until next sync cycle", _settings.SyncIntervalMinutes);
            await Task.Delay(TimeSpan.FromMinutes(_settings.SyncIntervalMinutes), stoppingToken);
        }

        _logger.LogInformation("CalendarSync worker stopped");
    }

    private async Task RunSyncAsync(CancellationToken cancellationToken)
    {
        var pattern = new Regex(_settings.SourceEventPattern, RegexOptions.IgnoreCase);
        var today = DateTime.UtcNow.Date;

        _logger.LogInformation("Starting sync for {Days} days from {Today}", _settings.SyncDaysAhead, today.ToString("dd.MM.yyyy"));

        await DeletePastTargetEventsAsync(today, cancellationToken);

        for (var dayOffset = 0; dayOffset < _settings.SyncDaysAhead; dayOffset++)
        {
            var dayStart = today.AddDays(dayOffset);
            var dayEnd = dayStart.AddDays(1);

            await SyncDayAsync(pattern, dayStart, dayEnd, cancellationToken);
        }

        _logger.LogInformation("Sync complete");
    }

    private async Task SyncDayAsync(
        Regex pattern,
        DateTime dayStart,
        DateTime dayEnd,
        CancellationToken cancellationToken)
    {
        var sourceEvents = await _sourceCalDav.GetEventsAsync(dayStart, dayEnd, cancellationToken);

        var matchingEvents = sourceEvents
            .Where(e => pattern.IsMatch(e.Summary))
            .OrderBy(e => e.StartTime)
            .ToList();

        var targetEvents = await _targetCalDav.GetEventsAsync(dayStart, dayEnd, cancellationToken);

        var existingTargetEvents = targetEvents
            .Where(e => string.Equals(
                e.Summary.Trim(),
                _settings.TargetEventName.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        if (matchingEvents.Count == 0)
        {
            _logger.LogDebug("No matching events on {Day}, skipping", dayStart.ToString("dd.MM.yyyy"));
            foreach (var existingTargetEvent in existingTargetEvents)
            {
                await _targetCalDav.DeleteEventAsync(existingTargetEvent.Href, existingTargetEvent.ETag, cancellationToken);
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
            _logger.LogInformation("Deleting stale target event on {Day} ({Start} to {End})", dayStart.ToString("dd.MM.yyyy"), stale.StartTime.ToString("dd.MM.yyyy HH:mm"), stale.EndTime.ToString("dd.MM.yyyy HH:mm"));
            await _targetCalDav.DeleteEventAsync(stale.Href, stale.ETag, cancellationToken);
        }

        var upToDateEvent = existingTargetEvents.FirstOrDefault(e => e.StartTime == targetStart && e.EndTime == targetEnd);
        
        if (upToDateEvent is not null)
        {
            _logger.LogDebug(
                "Target event on {Day} already up to date ({Start} -> {End}), skipping",
                dayStart.ToString("dd.MM.yyyy"), targetStart.ToString("HH:mm"), targetEnd.ToString("HH:mm"));
            return;
        }

        _logger.LogInformation(
            "Creating target event on {Day}: '{Name}' {Start} -> {End} (from {Count} source event(s))",
            dayStart.ToString("dd.MM.yyyy"),
            _settings.TargetEventName,
            targetStart.ToString("HH:mm"),
            targetEnd.ToString("HH:mm"),
            matchingEvents.Count);

        await _targetCalDav.CreateEventAsync(_settings.TargetEventName, targetStart, targetEnd, cancellationToken);
    }

    private async Task DeletePastTargetEventsAsync(DateTime today, CancellationToken cancellationToken)
    {
        var distantPast = today.AddYears(-1);
        var pastEvents = await _targetCalDav.GetEventsAsync(distantPast, today, cancellationToken);

        if (pastEvents.Count == 0)
        {
            _logger.LogDebug("No past target events to clean up");
            return;
        }

        _logger.LogInformation("Deleting {Count} past target event(s) before {Today}", pastEvents.Count, today.ToString("dd.MM.yyyy"));

        foreach (var pastEvent in pastEvents)
        {
            _logger.LogInformation("Deleting past target event ({Start} to {End})", pastEvent.StartTime.ToString("dd.MM.yyyy HH:mm"), pastEvent.EndTime.ToString("dd.MM.yyyy HH:mm"));
            await _targetCalDav.DeleteEventAsync(pastEvent.Href, pastEvent.ETag, cancellationToken);
        }
    }
}
