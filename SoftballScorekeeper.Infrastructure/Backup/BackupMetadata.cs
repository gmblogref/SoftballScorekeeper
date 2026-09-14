namespace SoftballScorekeeper.Infrastructure.Backup;

public sealed record BackupMetadata(
    int BackupFormatVersion,
    string AppVersion,
    int DatabaseVersion,
    DateTimeOffset CreatedAt);

public enum BackupDatabaseCompatibility
{
    OlderSupported = 1,
    Current = 2,
}

public enum BackupInspectionFailure
{
    None = 0,
    FileNotFound = 1,
    InvalidPackage = 2,
    MissingRequiredContent = 3,
    InvalidMetadata = 4,
    UnsupportedBackupFormat = 5,
    InvalidDatabase = 6,
    DatabaseVersionMismatch = 7,
    UnsupportedDatabaseVersion = 8,
}

public sealed record BackupInspectionResult(
    bool IsValid,
    BackupInspectionFailure Failure,
    BackupMetadata? Metadata,
    BackupDatabaseCompatibility? DatabaseCompatibility)
{
    internal static BackupInspectionResult Valid(
        BackupMetadata metadata,
        BackupDatabaseCompatibility compatibility) =>
        new(true, BackupInspectionFailure.None, metadata, compatibility);

    internal static BackupInspectionResult Invalid(BackupInspectionFailure failure) =>
        new(false, failure, null, null);
}
