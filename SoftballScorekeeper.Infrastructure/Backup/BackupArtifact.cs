namespace SoftballScorekeeper.Infrastructure.Backup;

public sealed class BackupArtifact : IAsyncDisposable
{
    private readonly string workspacePath;
    private bool isDisposed;

    internal BackupArtifact(string filePath, string workspacePath, BackupMetadata metadata)
    {
        FilePath = filePath;
        this.workspacePath = workspacePath;
        Metadata = metadata;
    }

    public string FilePath { get; }

    public BackupMetadata Metadata { get; }

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
