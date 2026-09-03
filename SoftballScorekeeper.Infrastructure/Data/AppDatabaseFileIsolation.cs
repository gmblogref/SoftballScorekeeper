namespace SoftballScorekeeper.Infrastructure.Data;

public static class AppDatabaseFileIsolation
{
    private static readonly string[] SqliteArtifactSuffixes = ["", "-journal", "-wal", "-shm"];

    public static string ResolveDatabasePath(
        string dataDirectory,
        string normalDatabaseFileName,
        string androidUiTestDatabaseFileName,
        bool useAndroidUiTestDatabase
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ValidateFileName(normalDatabaseFileName, nameof(normalDatabaseFileName));
        ValidateFileName(androidUiTestDatabaseFileName, nameof(androidUiTestDatabaseFileName));
        if (
            string.Equals(
                normalDatabaseFileName,
                androidUiTestDatabaseFileName,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new ArgumentException("Normal and Android UI test database files must differ.");
        }

        var fileName = useAndroidUiTestDatabase
            ? androidUiTestDatabaseFileName
            : normalDatabaseFileName;
        return Path.Combine(dataDirectory, fileName);
    }

    public static void ResetAndroidUiTestDatabase(
        string normalDatabasePath,
        string androidUiTestDatabasePath
    )
    {
        var normalPath = Path.GetFullPath(normalDatabasePath);
        var testPath = Path.GetFullPath(androidUiTestDatabasePath);
        if (string.Equals(normalPath, testPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "The Android UI test database path must differ from the normal database path."
            );

        foreach (var suffix in SqliteArtifactSuffixes)
        {
            var artifactPath = testPath + suffix;
            if (File.Exists(artifactPath))
                File.Delete(artifactPath);
        }
    }

    private static void ValidateFileName(string fileName, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName, parameterName);
        if (!string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
            throw new ArgumentException(
                "A database file name cannot contain a path.",
                parameterName
            );
    }
}
