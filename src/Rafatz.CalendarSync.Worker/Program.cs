using Microsoft.Extensions.Options;
using Rafatz.CalendarSync;
using Rafatz.CalendarSync.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IOptions<CalendarSyncSettings>>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var settings = new CalendarSyncSettings
    {
        SourceServerUrl = config.GetValueOrThrow<Uri>("SOURCE_SERVER_URL"),
        SourceCalendarUrl = config.GetValueOrThrow<Uri>("SOURCE_CALENDAR_URL"),
        SourceUsername = config.GetValueOrThrow<string>("SOURCE_USERNAME"),
        SourcePassword = config.GetValueOrThrow<string>("SOURCE_PASSWORD"),
        TargetServerUrl = config.GetValueOrThrow<Uri>("TARGET_SERVER_URL"),
        TargetCalendarUrl = config.GetValueOrThrow<Uri>("TARGET_CALENDAR_URL"),
        TargetUsername = config.GetValueOrThrow<string>("TARGET_USERNAME"),
        TargetPassword = config.GetValueOrThrow<string>("TARGET_PASSWORD"),
        SourceEventPattern = config.GetValueOrThrow<string>("SOURCE_EVENT_PATTERN"),
        TargetEventName = config.GetValueOrThrow<string>("TARGET_EVENT_NAME"),
        SyncDaysAhead = config.GetValueOrThrow<int>("SYNC_DAYS_AHEAD"),
        PrependMinutes = config.GetValueOrThrow<int>("PREPEND_MINUTES"),
        SyncIntervalMinutes = config.GetValueOrThrow<int>("SYNC_INTERVAL_MINUTES"),
    };
    return Options.Create(settings);
});

builder.Services.AddSingleton<ISourceCalDavClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<CalendarSyncSettings>>().Value;
    var logger = sp.GetRequiredService<ILogger<CalDavClient>>();
    return new CalDavClient(settings.SourceServerUrl, settings.SourceCalendarUrl, settings.SourceUsername, settings.SourcePassword, logger);
});

builder.Services.AddSingleton<ITargetCalDavClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<CalendarSyncSettings>>().Value;
    var logger = sp.GetRequiredService<ILogger<CalDavClient>>();
    return new CalDavClient(settings.TargetServerUrl, settings.TargetCalendarUrl, settings.TargetUsername, settings.TargetPassword, logger);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
