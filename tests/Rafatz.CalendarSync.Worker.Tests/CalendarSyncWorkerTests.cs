using AutoFixture;
using AutoFixture.AutoMoq;
using CalDAV.Models;
using Microsoft.Extensions.Options;
using Moq;

namespace Rafatz.CalendarSync.Tests;

public class CalendarSyncWorkerTests
{
    private readonly IFixture _fixture = new Fixture().Customize(new AutoMoqCustomization());
    private readonly Mock<ISourceCalDavClient> _sourceCalDavMock;
    private readonly Mock<ITargetCalDavClient> _targetCalDavMock;
    private readonly Mock<IOptions<CalendarSyncSettings>> _optionsMock;
    private readonly CalendarSyncSettings _settings;
    private readonly Rafatz.CalendarSync.CalendarSyncWorker _sut;

    public CalendarSyncWorkerTests()
    {
        _sourceCalDavMock = _fixture.Freeze<Mock<ISourceCalDavClient>>();
        _targetCalDavMock = _fixture.Freeze<Mock<ITargetCalDavClient>>();
        _optionsMock = _fixture.Freeze<Mock<IOptions<CalendarSyncSettings>>>();

        _settings = _fixture.Build<CalendarSyncSettings>()
            .With(x => x.SourceServerUrl, new Uri("https://source.com/"))
            .With(x => x.SourceCalendarUrl, new Uri("https://source.com/cal/"))
            .With(x => x.TargetServerUrl, new Uri("https://target.com/"))
            .With(x => x.TargetCalendarUrl, new Uri("https://target.com/cal/"))
            .With(x => x.SourceEventPattern, "SYNC")
            .With(x => x.TargetEventName, "Merged Event")
            .With(x => x.SyncDaysAhead, 1)
            .With(x => x.PrependMinutes, 0)
            .With(x => x.SyncIntervalMinutes, 60)
            .Create();

        _optionsMock.Setup(x => x.Value).Returns(_settings);

        _sut = _fixture.Create<Rafatz.CalendarSync.CalendarSyncWorker>();
    }

    [Fact]
    public async Task RunSyncAsync_WhenMatchingEventsFound_CreatesTargetEvent()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var cancellationToken = CancellationToken.None;

        var sourceEvents = new List<CalendarEvent>
        {
            _fixture.Build<CalendarEvent>()
                .With(x => x.Summary, "SYNC: Meeting")
                .With(x => x.StartTime, today.AddHours(10))
                .With(x => x.EndTime, today.AddHours(11))
                .Create(),
            _fixture.Build<CalendarEvent>()
                .With(x => x.Summary, "SYNC: Call")
                .With(x => x.StartTime, today.AddHours(14))
                .With(x => x.EndTime, today.AddHours(15))
                .Create()
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
            _fixture.Build<CalendarEvent>()
                .With(x => x.Summary, "Other: Meeting")
                .With(x => x.StartTime, today.AddHours(10))
                .With(x => x.EndTime, today.AddHours(11))
                .Create()
        };

        var targetEvents = new List<CalendarEvent>
        {
            _fixture.Build<CalendarEvent>()
                .With(x => x.Summary, _settings.TargetEventName)
                .With(x => x.Href, "/event1")
                .With(x => x.ETag, "tag1")
                .With(x => x.StartTime, today.AddHours(10))
                .With(x => x.EndTime, today.AddHours(11))
                .Create()
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
            _fixture.Build<CalendarEvent>()
                .With(x => x.Summary, "SYNC: Meeting")
                .With(x => x.StartTime, today.AddHours(10))
                .With(x => x.EndTime, today.AddHours(12))
                .Create()
        };

        // Stale because of different time
        var targetEvents = new List<CalendarEvent>
        {
            _fixture.Build<CalendarEvent>()
                .With(x => x.Summary, _settings.TargetEventName)
                .With(x => x.Href, "/stale")
                .With(x => x.ETag, "tag")
                .With(x => x.StartTime, today.AddHours(10))
                .With(x => x.EndTime, today.AddHours(11))
                .Create()
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
        var runSyncAsync = typeof(Rafatz.CalendarSync.CalendarSyncWorker).GetMethod("RunSyncAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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