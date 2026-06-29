using Kairos.Shared.Models;
using Kairos.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Kairos.ValidationTest;

public class ActivityEventSyncServiceTests
{
    private readonly StubSupabaseActivityEventStore _eventStoreStub;
    private readonly Mock<ITimeTrackingService> _timeTrackingServiceMock;
    private readonly StubSupabaseAuthService _authServiceStub;
    private readonly Mock<ISyncConflictNotifier> _conflictNotifierMock;
    private readonly StubSettingsService _settingsServiceStub;
    private readonly StubSupabaseRealtimeService _realtimeServiceStub;
    private readonly ActivityEventSyncService _sut;

    public ActivityEventSyncServiceTests()
    {
        _eventStoreStub = new StubSupabaseActivityEventStore();
        _timeTrackingServiceMock = new Mock<ITimeTrackingService>();
        _authServiceStub = new StubSupabaseAuthService();
        _conflictNotifierMock = new Mock<ISyncConflictNotifier>();
        _settingsServiceStub = new StubSettingsService();
        _realtimeServiceStub = new StubSupabaseRealtimeService();

        _timeTrackingServiceMock.Setup(m => m.Account).Returns(new TimeAccount());

        _sut = new ActivityEventSyncService(
            _eventStoreStub,
            _timeTrackingServiceMock.Object,
            _authServiceStub,
            _conflictNotifierMock.Object,
            _settingsServiceStub,
            _realtimeServiceStub,
            NullLogger<ActivityEventSyncService>.Instance);
    }

    [Fact]
    public async Task RealtimeConnected_TriggersImmediateSync()
    {
        // Act
        _authServiceStub.IsAuthenticated = true;
        _realtimeServiceStub.TriggerConnected();

        // Assert
        Assert.True(await WaitUntilAsync(() => _eventStoreStub.LoadEventsCalled, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task RealtimeTableChanged_ActivityEvents_TriggersImmediateSync()
    {
        // Act
        _authServiceStub.IsAuthenticated = true;
        _realtimeServiceStub.RaiseTableChanged("activity_events");

        // Assert
        Assert.True(await WaitUntilAsync(() => _eventStoreStub.LoadEventsCalled, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task RealtimeTableChanged_OtherTable_DoesNotTriggerSync()
    {
        // Act
        _authServiceStub.IsAuthenticated = true;
        _realtimeServiceStub.RaiseTableChanged("other_table");

        // Assert
        Assert.False(await WaitUntilAsync(() => _eventStoreStub.LoadEventsCalled, TimeSpan.FromMilliseconds(250)));
        Assert.False(_eventStoreStub.LoadEventsCalled);
    }

    [Fact]
    public async Task TriggerImmediateSync_LastModifiedOnlyChange_DoesNotTreatAsLocalEventChange()
    {
        // Arrange
        _authServiceStub.IsAuthenticated = true;
        var activityEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-10),
            EndTime = DateTimeOffset.UtcNow.AddMinutes(-5),
            ActivityName = "Work",
            ActivityEmoji = "",
            ActivityColor = "#10B981",
            Comment = "Deep work",
            Metadata = string.Empty
        };

        var account = new TimeAccount
        {
            Events = new List<ActivityEvent> { activityEvent.Clone() },
            LastModifiedAtUtc = DateTimeOffset.UtcNow
        };

        _timeTrackingServiceMock.Setup(m => m.Account).Returns(account);
        _eventStoreStub.ServerEvents = new List<ActivityEvent> { activityEvent.Clone() };

        // First sync establishes snapshots.
        await _sut.TriggerImmediateSyncAsync();

        var saveCallsAfterFirstSync = _eventStoreStub.SaveEventsCallCount;

        // Simulate unrelated local state update that only changes account timestamp.
        account.LastModifiedAtUtc = account.LastModifiedAtUtc.AddSeconds(5);

        // Act
        await _sut.TriggerImmediateSyncAsync();

        // Assert
        Assert.Equal(saveCallsAfterFirstSync, _eventStoreStub.SaveEventsCallCount);
        _conflictNotifierMock.Verify(m => m.ResolveConflictAsync(), Times.Never);
    }

    [Fact]
    public async Task TriggerImmediateSync_EventContentChange_PushesToServer()
    {
        // Arrange
        _authServiceStub.IsAuthenticated = true;
        var activityEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-30),
            EndTime = DateTimeOffset.UtcNow.AddMinutes(-10),
            ActivityName = "Work",
            ActivityEmoji = "",
            ActivityColor = "#10B981",
            Comment = "Initial",
            Metadata = string.Empty
        };

        var account = new TimeAccount
        {
            Events = new List<ActivityEvent> { activityEvent.Clone() },
            LastModifiedAtUtc = DateTimeOffset.UtcNow
        };

        _timeTrackingServiceMock.Setup(m => m.Account).Returns(account);
        _eventStoreStub.ServerEvents = new List<ActivityEvent> { activityEvent.Clone() };

        // First sync establishes snapshots.
        await _sut.TriggerImmediateSyncAsync();
        var saveCallsAfterFirstSync = _eventStoreStub.SaveEventsCallCount;

        // Simulate real local event edit.
        account.Events[0].Comment = "Edited comment";

        // Act
        await _sut.TriggerImmediateSyncAsync();

        // Assert
        Assert.Equal(saveCallsAfterFirstSync + 1, _eventStoreStub.SaveEventsCallCount);
        Assert.Equal("Edited comment", _eventStoreStub.ServerEvents.Single().Comment);
        _conflictNotifierMock.Verify(m => m.ResolveConflictAsync(), Times.Never);
    }

    [Fact]
    public async Task TriggerImmediateSync_FirstSyncDivergence_PrefersServerWithoutConflict()
    {
        // Arrange
        _authServiceStub.IsAuthenticated = true;

        var localEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            StartTime = DateTimeOffset.UtcNow.AddHours(-3),
            EndTime = DateTimeOffset.UtcNow.AddHours(-2),
            ActivityName = "Local",
            ActivityColor = "#10B981",
            Comment = "Local",
            Metadata = string.Empty
        };

        var serverEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            StartTime = DateTimeOffset.UtcNow.AddHours(-1),
            EndTime = DateTimeOffset.UtcNow.AddMinutes(-30),
            ActivityName = "Server",
            ActivityColor = "#10B981",
            Comment = "Server",
            Metadata = string.Empty
        };

        var account = new TimeAccount
        {
            Events = new List<ActivityEvent> { localEvent },
            LastModifiedAtUtc = DateTimeOffset.UtcNow
        };

        _timeTrackingServiceMock.Setup(m => m.Account).Returns(account);
        _eventStoreStub.ServerEvents = new List<ActivityEvent> { serverEvent.Clone() };

        // Act
        await _sut.TriggerImmediateSyncAsync();

        // Assert
        _timeTrackingServiceMock.Verify(
            m => m.UpdateEventsFromServer(It.Is<IReadOnlyList<ActivityEvent>>(events =>
                events.Count == 1 &&
                events[0].Id == serverEvent.Id &&
                events[0].Comment == serverEvent.Comment)),
            Times.Once);
        Assert.Equal(0, _eventStoreStub.SaveEventsCallCount);
        _conflictNotifierMock.Verify(m => m.ResolveConflictAsync(), Times.Never);
    }

    [Fact]
    public async Task TriggerImmediateSync_FirstSyncServerEmpty_SeedsFromLocalWithoutConflict()
    {
        // Arrange
        _authServiceStub.IsAuthenticated = true;

        var localEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            StartTime = DateTimeOffset.UtcNow.AddHours(-2),
            EndTime = DateTimeOffset.UtcNow.AddHours(-1),
            ActivityName = "Local",
            ActivityColor = "#10B981",
            Comment = "Seed me",
            Metadata = string.Empty
        };

        var account = new TimeAccount
        {
            Events = new List<ActivityEvent> { localEvent.Clone() },
            LastModifiedAtUtc = DateTimeOffset.UtcNow
        };

        _timeTrackingServiceMock.Setup(m => m.Account).Returns(account);
        _eventStoreStub.ServerEvents = new List<ActivityEvent>();

        // Act
        await _sut.TriggerImmediateSyncAsync();

        // Assert
        Assert.Equal(1, _eventStoreStub.SaveEventsCallCount);
        Assert.Single(_eventStoreStub.ServerEvents);
        Assert.Equal(localEvent.Id, _eventStoreStub.ServerEvents.Single().Id);
        _timeTrackingServiceMock.Verify(m => m.UpdateEventsFromServer(It.IsAny<IReadOnlyList<ActivityEvent>>()), Times.Never);
        _conflictNotifierMock.Verify(m => m.ResolveConflictAsync(), Times.Never);
    }

    [Fact]
    public async Task RealtimeTableChanged_AfterLocalPush_IsSuppressedDuringEchoWindow()
    {
        // Arrange
        _authServiceStub.IsAuthenticated = true;
        var activityEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-20),
            EndTime = DateTimeOffset.UtcNow.AddMinutes(-10),
            ActivityName = "Work",
            ActivityEmoji = "",
            ActivityColor = "#10B981",
            Comment = "Before",
            Metadata = string.Empty
        };

        var account = new TimeAccount
        {
            Events = new List<ActivityEvent> { activityEvent.Clone() },
            LastModifiedAtUtc = DateTimeOffset.UtcNow
        };

        _timeTrackingServiceMock.Setup(m => m.Account).Returns(account);
        _eventStoreStub.ServerEvents = new List<ActivityEvent> { activityEvent.Clone() };

        // Establish baseline.
        await _sut.TriggerImmediateSyncAsync();
        var loadCallsAfterBaseline = _eventStoreStub.LoadEventsCallCount;

        // Local edit triggers server push and starts realtime suppression window.
        account.Events[0].Comment = "After";
        await _sut.TriggerImmediateSyncAsync();
        var loadCallsAfterLocalPush = _eventStoreStub.LoadEventsCallCount;

        // Act: realtime echo arrives immediately after our own push.
        _realtimeServiceStub.RaiseTableChanged("activity_events");

        // Assert: no extra sync fetch was triggered by the echo.
        Assert.False(await WaitUntilAsync(
            () => _eventStoreStub.LoadEventsCallCount > loadCallsAfterLocalPush,
            TimeSpan.FromMilliseconds(300)));
        Assert.Equal(loadCallsAfterLocalPush, _eventStoreStub.LoadEventsCallCount);
        Assert.True(loadCallsAfterLocalPush > loadCallsAfterBaseline);
    }

    [Fact]
    public async Task RealtimeTableChanged_AfterSuppressionWindow_TriggersSyncAgain()
    {
        // Arrange
        _authServiceStub.IsAuthenticated = true;
        var activityEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-20),
            EndTime = DateTimeOffset.UtcNow.AddMinutes(-10),
            ActivityName = "Work",
            ActivityEmoji = "",
            ActivityColor = "#10B981",
            Comment = "Before",
            Metadata = string.Empty
        };

        var account = new TimeAccount
        {
            Events = new List<ActivityEvent> { activityEvent.Clone() },
            LastModifiedAtUtc = DateTimeOffset.UtcNow
        };

        _timeTrackingServiceMock.Setup(m => m.Account).Returns(account);
        _eventStoreStub.ServerEvents = new List<ActivityEvent> { activityEvent.Clone() };

        // Baseline + local push to start suppression window.
        await _sut.TriggerImmediateSyncAsync();
        account.Events[0].Comment = "After";
        await _sut.TriggerImmediateSyncAsync();

        var loadCallsBeforeRealtime = _eventStoreStub.LoadEventsCallCount;

        // Wait until suppression window passes.
        await Task.Delay(2200);

        // Act
        _realtimeServiceStub.RaiseTableChanged("activity_events");

        // Assert
        Assert.True(await WaitUntilAsync(
            () => _eventStoreStub.LoadEventsCallCount > loadCallsBeforeRealtime,
            TimeSpan.FromSeconds(1)));
        Assert.True(_eventStoreStub.LoadEventsCallCount > loadCallsBeforeRealtime);
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout, TimeSpan? pollInterval = null)
    {
        if (condition())
        {
            return true;
        }

        var interval = pollInterval ?? TimeSpan.FromMilliseconds(15);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            await Task.Delay(interval);
            if (condition())
            {
                return true;
            }
        }

        return condition();
    }
}

internal class StubSupabaseActivityEventStore : ISupabaseActivityEventStore
{
    public bool LoadEventsCalled { get; private set; }
    public int LoadEventsCallCount { get; private set; }
    public int SaveEventsCallCount { get; private set; }
    public IReadOnlyList<ActivityEvent> ServerEvents { get; set; } = new List<ActivityEvent>();

    public Task<IReadOnlyList<ActivityEvent>> LoadEventsAsync()
    {
        LoadEventsCalled = true;
        LoadEventsCallCount++;
        return Task.FromResult(ServerEvents);
    }

    public Task SaveEventsAsync(IReadOnlyList<ActivityEvent> events)
    {
        SaveEventsCallCount++;
        ServerEvents = events.Select(e => e.Clone()).ToList();
        return Task.CompletedTask;
    }
}
