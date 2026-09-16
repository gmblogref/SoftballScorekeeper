namespace SoftballScorekeeper.Infrastructure.Backup;

public sealed class RestoreSourceArtifact : IAsyncDisposable
{
    private readonly string workspacePath;
    private bool isDisposed;

    public RestoreSourceArtifact(string filePath, string workspacePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        FilePath = filePath;
        this.workspacePath = workspacePath;
    }

    public string FilePath { get; }

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
