using Microsoft.Extensions.Options;
using Rafatz.CalendarSync;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<CalendarSyncSettings>(
    builder.Configuration.GetSection(CalendarSyncSettings.SectionName));

builder.Services.AddSingleton<SourceCalDavClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<CalendarSyncSettings>>().Value;
    var logger = sp.GetRequiredService<ILogger<CalDavClient>>();
    return new SourceCalDavClient(settings.SourceUrl, settings.SourceUsername, settings.SourcePassword, logger);
});

builder.Services.AddSingleton<TargetCalDavClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<CalendarSyncSettings>>().Value;
    var logger = sp.GetRequiredService<ILogger<CalDavClient>>();
    return new TargetCalDavClient(settings.TargetUrl, settings.TargetUsername, settings.TargetPassword, logger);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
