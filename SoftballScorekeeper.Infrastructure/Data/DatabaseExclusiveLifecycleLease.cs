namespace SoftballScorekeeper.Infrastructure.Data;

public sealed class DatabaseExclusiveLifecycleLease : IAsyncDisposable
{
    private DatabaseLifecycleCoordinator? coordinator;

    internal DatabaseExclusiveLifecycleLease(DatabaseLifecycleCoordinator coordinator)
    {
        this.coordinator = coordinator;
    }

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref coordinator, null)?.ExitExclusive();
        return ValueTask.CompletedTask;
    }
}
