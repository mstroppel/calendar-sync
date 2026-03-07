using Rafatz.CalendarSync;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<CalendarSyncSettings>(
    builder.Configuration.GetSection(CalendarSyncSettings.SectionName));

builder.Services.AddSingleton<CalDavClient>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
