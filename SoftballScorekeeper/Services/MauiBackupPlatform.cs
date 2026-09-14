using SoftballScorekeeper.Infrastructure.Backup;

namespace SoftballScorekeeper.Services;

public sealed class MauiBackupPlatform : IBackupPlatform
{
    public string CacheDirectory => FileSystem.CacheDirectory;

    public string AppVersion => AppInfo.Current.VersionString;
}
