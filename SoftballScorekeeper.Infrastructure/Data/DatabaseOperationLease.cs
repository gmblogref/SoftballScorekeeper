using SQLite;

namespace SoftballScorekeeper.Infrastructure.Data;

public sealed class DatabaseOperationLease : IAsyncDisposable
{
    private DatabaseLifecycleCoordinator? coordinator;

    internal DatabaseOperationLease(
        DatabaseLifecycleCoordinator coordinator,
        SQLiteAsyncConnection connection
    )
    {
        this.coordinator = coordinator;
        Connection = connection;
    }

    public SQLiteAsyncConnection Connection { get; }

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref coordinator, null)?.ExitOperation();
        return ValueTask.CompletedTask;
    }
}
