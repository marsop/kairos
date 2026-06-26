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
}

internal class StubSupabaseActivityEventStore : ISupabaseActivityEventStore
{
    public bool LoadEventsCalled { get; private set; }

    public Task<IReadOnlyList<ActivityEvent>> LoadEventsAsync()
    {
        LoadEventsCalled = true;
        return Task.FromResult<IReadOnlyList<ActivityEvent>>(new List<ActivityEvent>());
    }

    public Task SaveEventsAsync(IReadOnlyList<ActivityEvent> events)
    {
        return Task.CompletedTask;
    }
}
