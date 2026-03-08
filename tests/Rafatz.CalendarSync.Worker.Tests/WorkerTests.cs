using CalDAV.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Rafatz.CalendarSync;

namespace Rafatz.CalendarSync.Tests;

public class WorkerTests
{
    private readonly Mock<ILogger<Rafatz.CalendarSync.Worker>> _loggerMock;
    private readonly Mock<ISourceCalDavClient> _sourceCalDavMock;
    private readonly Mock<ITargetCalDavClient> _targetCalDavMock;
    private readonly Mock<IOptions<CalendarSyncSettings>> _optionsMock;
    private readonly CalendarSyncSettings _settings;
    private readonly Rafatz.CalendarSync.Worker _sut;

    public WorkerTests()
    {
        _loggerMock = new Mock<ILogger<Rafatz.CalendarSync.Worker>>();
        _sourceCalDavMock = new Mock<ISourceCalDavClient>();
        _targetCalDavMock = new Mock<ITargetCalDavClient>();
        _optionsMock = new Mock<IOptions<CalendarSyncSettings>>();

        _settings = new CalendarSyncSettings
        {
            SourceServerUrl = new Uri("https://source.com/"),
            SourceCalendarUrl = new Uri("https://source.com/cal/"),
            SourceUsername = "user",
            SourcePassword = "pass",
            TargetServerUrl = new Uri("https://target.com/"),
            TargetCalendarUrl = new Uri("https://target.com/cal/"),
            TargetUsername = "user",
            TargetPassword = "pass",
            SourceEventPattern = "SYNC",
            TargetEventName = "Merged Event",
            SyncDaysAhead = 1,
            PrependMinutes = 0,
            SyncIntervalMinutes = 60
        };

        _optionsMock.Setup(x => x.Value).Returns(_settings);

        _sut = new Rafatz.CalendarSync.Worker(
            _loggerMock.Object,
            _sourceCalDavMock.Object,
            _targetCalDavMock.Object,
            _optionsMock.Object);
    }

    [Fact]
    public async Task RunSyncAsync_WhenMatchingEventsFound_CreatesTargetEvent()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var cancellationToken = CancellationToken.None;

        var sourceEvents = new List<CalendarEvent>
        {
            new() { Summary = "SYNC: Meeting", StartTime = today.AddHours(10), EndTime = today.AddHours(11) },
            new() { Summary = "SYNC: Call", StartTime = today.AddHours(14), EndTime = today.AddHours(15) }
        };

        SetupSourceEvents(sourceEvents);
        SetupTargetEvents(new List<CalendarEvent>());
        SetupPastTargetEvents(new List<CalendarEvent>(), today);

        // Act
        await CallRunSyncAsync(cancellationToken);

        // Assert
        _targetCalDavMock.Verify(x => x.CreateEventAsync(
            _settings.TargetEventName,
            today.AddHours(10),
            today.AddHours(15),
            cancellationToken), Times.Once);
    }

    [Fact]
    public async Task RunSyncAsync_WhenNoMatchingEventsFound_DeletesExistingTargetEvent()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var cancellationToken = CancellationToken.None;

        var sourceEvents = new List<CalendarEvent>
        {
            new() { Summary = "Other: Meeting", StartTime = today.AddHours(10), EndTime = today.AddHours(11) }
        };

        var targetEvents = new List<CalendarEvent>
        {
            new() { Summary = _settings.TargetEventName, Href = "/event1", ETag = "tag1", StartTime = today.AddHours(10), EndTime = today.AddHours(11) }
        };

        SetupSourceEvents(sourceEvents);
        SetupTargetEvents(targetEvents);
        SetupPastTargetEvents(new List<CalendarEvent>(), today);

        // Act
        await CallRunSyncAsync(cancellationToken);

        // Assert
        _targetCalDavMock.Verify(x => x.DeleteEventAsync("/event1", "tag1", cancellationToken), Times.Once);
    }

    [Fact]
    public async Task RunSyncAsync_WhenStaleEventExists_DeletesStaleAndCreatesNew()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var cancellationToken = CancellationToken.None;

        var sourceEvents = new List<CalendarEvent>
        {
            new() { Summary = "SYNC: Meeting", StartTime = today.AddHours(10), EndTime = today.AddHours(12) }
        };

        // Stale because of different time
        var targetEvents = new List<CalendarEvent>
        {
            new() { Summary = _settings.TargetEventName, Href = "/stale", ETag = "tag", StartTime = today.AddHours(10), EndTime = today.AddHours(11) }
        };

        SetupSourceEvents(sourceEvents);
        SetupTargetEvents(targetEvents);
        SetupPastTargetEvents(new List<CalendarEvent>(), today);

        // Act
        await CallRunSyncAsync(cancellationToken);

        // Assert
        _targetCalDavMock.Verify(x => x.DeleteEventAsync("/stale", "tag", cancellationToken), Times.Once);
        _targetCalDavMock.Verify(x => x.CreateEventAsync(
            _settings.TargetEventName,
            today.AddHours(10),
            today.AddHours(12),
            cancellationToken), Times.Once);
    }

    private async Task CallRunSyncAsync(CancellationToken cancellationToken)
    {
        var runSyncAsync = typeof(Rafatz.CalendarSync.Worker).GetMethod("RunSyncAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)runSyncAsync!.Invoke(_sut, [cancellationToken])!;
    }

    private void SetupSourceEvents(IReadOnlyList<CalendarEvent> events)
    {
        _sourceCalDavMock.Setup(x => x.GetEventsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);
    }

    private void SetupTargetEvents(IReadOnlyList<CalendarEvent> events)
    {
        _targetCalDavMock.Setup(x => x.GetEventsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);
    }

    private void SetupPastTargetEvents(IReadOnlyList<CalendarEvent> events, DateTime today)
    {
        _targetCalDavMock.Setup(x => x.GetEventsAsync(
                It.Is<DateTime>(d => d < today),
                It.Is<DateTime>(d => d.Date == today.Date),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);
    }
}