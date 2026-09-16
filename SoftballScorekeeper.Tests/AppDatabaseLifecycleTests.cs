using SoftballScorekeeper.Infrastructure.Data;

namespace SoftballScorekeeper.Tests;

public sealed class AppDatabaseLifecycleTests
{
    [Fact]
    public async Task Exclusive_waits_for_active_operation()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await using var operation = await db.Database.BeginOperationAsync();

        var exclusiveTask = db.Database.BeginExclusiveLifecycleAsync();

        await AssertWaitingAsync(exclusiveTask);
        await operation.DisposeAsync();
        await using var exclusive = await exclusiveTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Pending_exclusive_blocks_later_operation_and_gets_ownership_first()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await using var operationA = await db.Database.BeginOperationAsync();
        var exclusiveTask = db.Database.BeginExclusiveLifecycleAsync();
        await WaitUntilAsync(() => db.Database.IsExclusiveLifecyclePending);

        var operationBTask = db.Database.BeginOperationAsync();

        await AssertWaitingAsync(operationBTask);
        await operationA.DisposeAsync();
        await using var exclusive = await exclusiveTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, db.Database.ActiveOperationCount);
        await AssertWaitingAsync(operationBTask);

        await exclusive.DisposeAsync();
        await using var operationB = await operationBTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Exclusive_ownership_is_granted_only_with_zero_active_operations()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await using var operation = await db.Database.BeginOperationAsync();
        var exclusiveTask = db.Database.BeginExclusiveLifecycleAsync();

        Assert.Equal(1, db.Database.ActiveOperationCount);
        await operation.DisposeAsync();
        await using var exclusive = await exclusiveTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, db.Database.ActiveOperationCount);
    }

    [Fact]
    public async Task Suspend_waits_for_exclusive_ownership_after_active_operation_drains()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await using var operation = await db.Database.BeginOperationAsync();
        var exclusiveTask = db.Database.BeginExclusiveLifecycleAsync();

        await AssertWaitingAsync(exclusiveTask);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.Database.SuspendAccessAsync());
        await operation.DisposeAsync();
        await using var exclusive = await exclusiveTask.WaitAsync(TimeSpan.FromSeconds(5));

        await db.Database.SuspendAccessAsync();
        await db.Database.ReinitializeAccessAsync();
    }

    [Fact]
    public async Task Normal_operations_resume_after_exclusive_release()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var exclusive = await db.Database.BeginExclusiveLifecycleAsync();
        var operationTask = db.Database.BeginOperationAsync();

        await AssertWaitingAsync(operationTask);
        await exclusive.DisposeAsync();

        await using var operation = await operationTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, db.Database.ActiveOperationCount);
    }

    [Fact]
    public async Task Multiple_normal_operations_drain_before_exclusive()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await using var operationA = await db.Database.BeginOperationAsync();
        await using var operationB = await db.Database.BeginOperationAsync();
        var exclusiveTask = db.Database.BeginExclusiveLifecycleAsync();

        await operationA.DisposeAsync();
        await AssertWaitingAsync(exclusiveTask);
        await operationB.DisposeAsync();

        await using var exclusive = await exclusiveTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, db.Database.ActiveOperationCount);
    }

    [Fact]
    public async Task Cancelled_normal_waiter_does_not_change_active_count_or_block_later_work()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var exclusive = await db.Database.BeginExclusiveLifecycleAsync();
        using var cancellation = new CancellationTokenSource();
        var cancelledTask = db.Database.BeginOperationAsync(cancellation.Token);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledTask);
        Assert.Equal(0, db.Database.ActiveOperationCount);
        await exclusive.DisposeAsync();
        await using var operation = await db
            .Database.BeginOperationAsync()
            .WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Cancelled_exclusive_waiter_clears_writer_pending_and_unblocks_normal_work()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        await using var operationA = await db.Database.BeginOperationAsync();
        using var cancellation = new CancellationTokenSource();
        var exclusiveTask = db.Database.BeginExclusiveLifecycleAsync(cancellation.Token);
        await WaitUntilAsync(() => db.Database.IsExclusiveLifecyclePending);
        var operationBTask = db.Database.BeginOperationAsync();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => exclusiveTask);
        Assert.False(db.Database.IsExclusiveLifecyclePending);
        await using var operationB = await operationBTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, db.Database.ActiveOperationCount);
    }

    [Fact]
    public async Task Backup_snapshot_waits_behind_exclusive_lifecycle()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var snapshotPath = Path.Combine(db.DirectoryPath, "lifecycle-snapshot.db3");
        var exclusive = await db.Database.BeginExclusiveLifecycleAsync();

        var snapshotTask = db.Database.CreateSnapshotAsync(snapshotPath);

        await AssertWaitingAsync(snapshotTask);
        await exclusive.DisposeAsync();
        await snapshotTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(File.Exists(snapshotPath));
    }

    [Fact]
    public void Production_repositories_do_not_use_legacy_raw_connection_acquisition()
    {
        var repositoryDirectory = Path.Combine(
            RepositoryRoot,
            "SoftballScorekeeper.Infrastructure",
            "Data",
            "Repositories"
        );

        var offenders = Directory
            .EnumerateFiles(repositoryDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                File.ReadAllText(path).Contains("GetConnectionAsync", StringComparison.Ordinal)
            )
            .ToArray();

        Assert.Empty(offenders);
    }

    private static async Task AssertWaitingAsync(Task task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(100));
        Assert.NotSame(task, completed);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        Assert.True(condition());
    }

    private static string RepositoryRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
