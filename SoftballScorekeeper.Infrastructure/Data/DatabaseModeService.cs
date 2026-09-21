namespace SoftballScorekeeper.Infrastructure.Data;

public sealed class DatabaseModeService(
    AppDatabase database,
    DatabaseModeState state,
    IDatabaseModePreferenceStore preferenceStore,
    ApplicationWorkflowCoordinator workflowCoordinator
)
{
    public Task<DatabaseModeChangeResult> EnterTestModeAsync(
        Func<Task<bool>> confirm,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(confirm);
        return RunAsync(
            DatabaseMode.Production,
            async () =>
                await confirm() ? ModeChangeDecision.Switch : ModeChangeDecision.Cancel,
            DatabaseMode.Test,
            cancellationToken
        );
    }

    public Task<DatabaseModeChangeResult> ExitTestModeAsync(
        Func<Task<TestModeExitChoice>> choose,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(choose);
        return RunAsync(
            DatabaseMode.Test,
            async () =>
                await choose() switch
                {
                    TestModeExitChoice.KeepTestData => ModeChangeDecision.Switch,
                    TestModeExitChoice.DeleteTestData => ModeChangeDecision.ResetTestData,
                    _ => ModeChangeDecision.Cancel,
                },
            DatabaseMode.Production,
            cancellationToken
        );
    }

    private async Task<DatabaseModeChangeResult> RunAsync(
        DatabaseMode requiredMode,
        Func<Task<ModeChangeDecision>> choose,
        DatabaseMode targetMode,
        CancellationToken cancellationToken
    )
    {
        await using var workflow = await workflowCoordinator.TryBeginAsync(cancellationToken);
        if (workflow is null)
            return DatabaseModeChangeResult.AlreadyInProgress;
        if (state.CurrentMode != requiredMode)
            return DatabaseModeChangeResult.InvalidMode;

        var decision = await choose();
        if (decision == ModeChangeDecision.Cancel)
            return DatabaseModeChangeResult.Cancelled;

        var previousMode = state.CurrentMode;
        await using var exclusive = await database.BeginExclusiveLifecycleAsync(cancellationToken);
        try
        {
            if (decision == ModeChangeDecision.ResetTestData)
                await database.ResetTestDatabaseAsync();
            else
                await database.SwitchModeAsync(targetMode);

            try
            {
                preferenceStore.Save(targetMode);
            }
            catch (Exception preferenceFailure)
            {
                try
                {
                    await database.SwitchModeAsync(previousMode);
                    TryRestorePreference(previousMode);
                }
                catch (Exception rollbackFailure)
                {
                    await database.SuspendAccessAsync();
                    throw new DatabaseModeRecoveryException(preferenceFailure, rollbackFailure);
                }

                throw;
            }

            state.SetCurrentMode(targetMode);
            return DatabaseModeChangeResult.Changed;
        }
        catch (DatabaseModeRecoveryException)
        {
            return DatabaseModeChangeResult.RecoveryFailed;
        }
        catch
        {
            return DatabaseModeChangeResult.Failed;
        }
    }

    private void TryRestorePreference(DatabaseMode mode)
    {
        try
        {
            preferenceStore.Save(mode);
        }
        catch
        {
            // The database is restored or safely suspended. The original failure is reported.
        }
    }

    private enum ModeChangeDecision
    {
        Switch,
        ResetTestData,
        Cancel,
    }
}

public enum DatabaseModeChangeResult
{
    Changed,
    Cancelled,
    AlreadyInProgress,
    InvalidMode,
    Failed,
    RecoveryFailed,
}

public enum TestModeExitChoice
{
    KeepTestData,
    DeleteTestData,
    Cancel,
}
