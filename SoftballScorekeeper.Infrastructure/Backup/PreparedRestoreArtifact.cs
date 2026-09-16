namespace SoftballScorekeeper.Infrastructure.Backup;

public sealed class PreparedRestoreArtifact : IAsyncDisposable
{
    private readonly string workspacePath;
    private bool isDisposed;

    internal PreparedRestoreArtifact(
        string databasePath,
        string workspacePath,
        BackupMetadata metadata,
        BackupDatabaseCompatibility compatibility
    )
    {
        DatabasePath = databasePath;
        this.workspacePath = workspacePath;
        Metadata = metadata;
        Compatibility = compatibility;
    }

    public string DatabasePath { get; }

    public BackupMetadata Metadata { get; }

    public BackupDatabaseCompatibility Compatibility { get; }

    public ValueTask DisposeAsync()
    {
        if (!isDisposed)
        {
            isDisposed = true;
            BackupPackageService.DeleteWorkspace(workspacePath);
        }

        return ValueTask.CompletedTask;
    }
}

public sealed record PrepareRestoreResult(
    BackupInspectionResult Inspection,
    PreparedRestoreArtifact? Artifact
);
