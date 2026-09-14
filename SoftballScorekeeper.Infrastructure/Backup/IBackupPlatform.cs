namespace SoftballScorekeeper.Infrastructure.Backup;

public interface IBackupPlatform
{
    string CacheDirectory { get; }

    string AppVersion { get; }
}
