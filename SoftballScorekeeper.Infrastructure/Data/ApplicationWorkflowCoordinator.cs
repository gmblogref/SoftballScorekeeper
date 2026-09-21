namespace SoftballScorekeeper.Infrastructure.Data;

public sealed class ApplicationWorkflowCoordinator
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<ApplicationWorkflowLease?> TryBeginAsync(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await gate.WaitAsync(0, cancellationToken)
            ? new ApplicationWorkflowLease(gate)
            : null;
    }
}

public sealed class ApplicationWorkflowLease : IAsyncDisposable
{
    private SemaphoreSlim? gate;

    internal ApplicationWorkflowLease(SemaphoreSlim gate)
    {
        this.gate = gate;
    }

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref gate, null)?.Release();
        return ValueTask.CompletedTask;
    }
}
