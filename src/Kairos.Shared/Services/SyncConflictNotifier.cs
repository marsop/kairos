namespace Kairos.Shared.Services;

public sealed class SyncConflictNotifier : ISyncConflictNotifier
{
    public event Action<Action<bool>>? OnConflictDetected;

    public Task<bool> ResolveConflictAsync()
    {
        var tcs = new TaskCompletionSource<bool>();

        if (OnConflictDetected is not null)
        {
            // Trigger the UI, passing a callback to resolve the task
            OnConflictDetected.Invoke(choice => tcs.TrySetResult(choice));
        }
        else
        {
            // If nothing is listening, default to overwriting local with server data
            tcs.TrySetResult(true);
        }

        return tcs.Task;
    }
}
