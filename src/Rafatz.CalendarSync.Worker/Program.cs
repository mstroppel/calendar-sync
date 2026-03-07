using Microsoft.Extensions.Options;
using Rafatz.CalendarSync;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<CalendarSyncSettings>()
    .BindConfiguration(CalendarSyncSettings.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<SourceCalDavClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<CalendarSyncSettings>>().Value;
    var logger = sp.GetRequiredService<ILogger<CalDavClient>>();
    return new SourceCalDavClient(settings.SourceServerUrl, settings.SourceCalendarUrl, settings.SourceUsername, settings.SourcePassword, logger);
});

builder.Services.AddSingleton<TargetCalDavClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<CalendarSyncSettings>>().Value;
    var logger = sp.GetRequiredService<ILogger<CalDavClient>>();
    return new TargetCalDavClient(settings.TargetServerUrl, settings.TargetCalendarUrl, settings.TargetUsername, settings.TargetPassword, logger);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
