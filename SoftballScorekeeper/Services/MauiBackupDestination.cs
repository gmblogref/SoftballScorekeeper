using SoftballScorekeeper.Infrastructure.Backup;

namespace SoftballScorekeeper.Services;

public sealed partial class MauiBackupDestination : IBackupDestination
{
    public Task<BackupDestinationResult> SaveAsync(
        string sourceFilePath,
        string suggestedFileName,
        CancellationToken cancellationToken = default
    ) => SavePlatformAsync(sourceFilePath, suggestedFileName, cancellationToken);

    private partial Task<BackupDestinationResult> SavePlatformAsync(
        string sourceFilePath,
        string suggestedFileName,
        CancellationToken cancellationToken
    );
}
