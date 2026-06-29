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
    public void RealtimeConnected_TriggersImmediateSync()
    {
        // Act
        _authServiceStub.IsAuthenticated = true;
        _realtimeServiceStub.TriggerConnected();

        // Need a small delay because TriggerImmediateSyncAsync runs in a fire-and-forget task
        Thread.Sleep(100);

        // Assert
        Assert.True(_eventStoreStub.LoadEventsCalled);
    }

    [Fact]
    public void RealtimeTableChanged_ActivityEvents_TriggersImmediateSync()
    {
        // Act
        _authServiceStub.IsAuthenticated = true;
        _realtimeServiceStub.RaiseTableChanged("activity_events");

        Thread.Sleep(100);

        // Assert
        Assert.True(_eventStoreStub.LoadEventsCalled);
    }

    [Fact]
    public void RealtimeTableChanged_OtherTable_DoesNotTriggerSync()
    {
        // Act
        _authServiceStub.IsAuthenticated = true;
        _realtimeServiceStub.RaiseTableChanged("other_table");

        Thread.Sleep(50);

        // Assert
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
}

internal class StubSupabaseActivityEventStore : ISupabaseActivityEventStore
{
    public bool LoadEventsCalled { get; private set; }
    public int SaveEventsCallCount { get; private set; }
    public IReadOnlyList<ActivityEvent> ServerEvents { get; set; } = new List<ActivityEvent>();

    public Task<IReadOnlyList<ActivityEvent>> LoadEventsAsync()
    {
        LoadEventsCalled = true;
        return Task.FromResult(ServerEvents);
    }

    public Task SaveEventsAsync(IReadOnlyList<ActivityEvent> events)
    {
        SaveEventsCallCount++;
        ServerEvents = events.Select(e => e.Clone()).ToList();
        return Task.CompletedTask;
    }
}
