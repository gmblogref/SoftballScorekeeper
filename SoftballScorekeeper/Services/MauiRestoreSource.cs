using SoftballScorekeeper.Infrastructure.Backup;

namespace SoftballScorekeeper.Services;

public sealed partial class MauiRestoreSource(IBackupPlatform backupPlatform) : IRestoreSource
{
    public Task<RestoreSourceArtifact?> SelectAsync(
        CancellationToken cancellationToken = default
    ) => SelectPlatformAsync(cancellationToken);

    private partial Task<RestoreSourceArtifact?> SelectPlatformAsync(
        CancellationToken cancellationToken
    );

    private async Task<RestoreSourceArtifact> CopyToPrivateCacheAsync(
        Stream source,
        CancellationToken cancellationToken
    )
    {
        var workspacePath = Path.Combine(
            backupPlatform.CacheDirectory,
            $"SoftballScorekeeper.Restore-source-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(workspacePath);
        var filePath = Path.Combine(workspacePath, "selected-backup.zip");
        try
        {
            await using var destination = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None
            );
            await source.CopyToAsync(destination, cancellationToken);
            await destination.FlushAsync(cancellationToken);
            return new RestoreSourceArtifact(filePath, workspacePath);
        }
        catch
        {
            if (Directory.Exists(workspacePath))
                Directory.Delete(workspacePath, recursive: true);
            throw;
        }
    }
}
