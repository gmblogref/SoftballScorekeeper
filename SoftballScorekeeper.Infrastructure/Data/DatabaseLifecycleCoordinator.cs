namespace SoftballScorekeeper.Infrastructure.Data;

internal sealed class DatabaseLifecycleCoordinator
{
    private readonly object sync = new();
    private readonly LinkedList<Waiter> waiters = new();
    private int activeOperationCount;
    private int pendingExclusiveCount;
    private bool isExclusiveActive;

    internal int ActiveOperationCount
    {
        get
        {
            lock (sync)
                return activeOperationCount;
        }
    }

    internal bool IsExclusiveActive
    {
        get
        {
            lock (sync)
                return isExclusiveActive;
        }
    }

    internal bool IsExclusivePending
    {
        get
        {
            lock (sync)
                return pendingExclusiveCount > 0;
        }
    }

    internal Task EnterOperationAsync(CancellationToken cancellationToken) =>
        EnterAsync(WaiterKind.Operation, cancellationToken);

    internal Task EnterExclusiveAsync(CancellationToken cancellationToken) =>
        EnterAsync(WaiterKind.Exclusive, cancellationToken);

    internal void ExitOperation()
    {
        lock (sync)
        {
            if (activeOperationCount <= 0)
                throw new InvalidOperationException("No database operation lease is active.");

            activeOperationCount--;
            ProcessWaiters();
        }
    }

    internal void ExitExclusive()
    {
        lock (sync)
        {
            if (!isExclusiveActive)
                throw new InvalidOperationException(
                    "No exclusive database lifecycle lease is active."
                );

            isExclusiveActive = false;
            ProcessWaiters();
        }
    }

    private async Task EnterAsync(WaiterKind kind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Waiter? waiter = null;

        lock (sync)
        {
            if (CanEnterImmediately(kind))
            {
                Grant(kind);
                return;
            }

            waiter = new Waiter(kind);
            waiter.Node = waiters.AddLast(waiter);
            if (kind == WaiterKind.Exclusive)
                pendingExclusiveCount++;
        }

        using var registration = cancellationToken.Register(
            static state =>
            {
                var request = (CancellationRequest)state!;
                request.Coordinator.Cancel(request.Waiter, request.CancellationToken);
            },
            new CancellationRequest(this, waiter, cancellationToken)
        );
        await waiter.Completion.Task.ConfigureAwait(false);
    }

    private bool CanEnterImmediately(WaiterKind kind) =>
        kind == WaiterKind.Operation
            ? !isExclusiveActive && pendingExclusiveCount == 0 && waiters.Count == 0
            : !isExclusiveActive && activeOperationCount == 0 && waiters.Count == 0;

    private void Grant(WaiterKind kind)
    {
        if (kind == WaiterKind.Operation)
            activeOperationCount++;
        else
            isExclusiveActive = true;
    }

    private void Cancel(Waiter waiter, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            if (waiter.Node is null)
                return;

            waiters.Remove(waiter.Node);
            waiter.Node = null;
            if (waiter.Kind == WaiterKind.Exclusive)
                pendingExclusiveCount--;
            waiter.Completion.TrySetCanceled(cancellationToken);
            ProcessWaiters();
        }
    }

    private void ProcessWaiters()
    {
        if (isExclusiveActive || waiters.First is null)
            return;

        if (waiters.First.Value.Kind == WaiterKind.Exclusive)
        {
            if (activeOperationCount > 0)
                return;

            var waiter = RemoveFirst();
            pendingExclusiveCount--;
            isExclusiveActive = true;
            waiter.Completion.TrySetResult();
            return;
        }

        while (waiters.First is { Value.Kind: WaiterKind.Operation })
        {
            var waiter = RemoveFirst();
            activeOperationCount++;
            waiter.Completion.TrySetResult();
        }
    }

    private Waiter RemoveFirst()
    {
        var waiter = waiters.First!.Value;
        waiters.RemoveFirst();
        waiter.Node = null;
        return waiter;
    }

    private enum WaiterKind
    {
        Operation,
        Exclusive,
    }

    private sealed class Waiter(WaiterKind kind)
    {
        internal WaiterKind Kind { get; } = kind;
        internal TaskCompletionSource Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal LinkedListNode<Waiter>? Node { get; set; }
    }

    private sealed record CancellationRequest(
        DatabaseLifecycleCoordinator Coordinator,
        Waiter Waiter,
        CancellationToken CancellationToken
    );
}
