namespace SoftballScorekeeper.Infrastructure.Data;

public sealed class AppDatabasePaths
{
    public const string DedicatedTestDatabaseFileName = "SoftballScorekeeper.Test.db3";

    public AppDatabasePaths(
        string dataDirectory,
        string productionDatabaseFileName,
        string testDatabaseFileName,
        string androidUiTestDatabaseFileName,
        bool useAndroidUiTestDatabaseAsNormal
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ValidateFileName(productionDatabaseFileName, nameof(productionDatabaseFileName));
        ValidateFileName(testDatabaseFileName, nameof(testDatabaseFileName));
        ValidateFileName(androidUiTestDatabaseFileName, nameof(androidUiTestDatabaseFileName));

        if (
            !string.Equals(
                testDatabaseFileName,
                DedicatedTestDatabaseFileName,
                StringComparison.Ordinal
            )
        )
            throw new ArgumentException(
                $"The product Test database filename must be {DedicatedTestDatabaseFileName}.",
                nameof(testDatabaseFileName)
            );

        DataDirectory = Path.GetFullPath(dataDirectory);
        ProductionDatabasePath = Resolve(productionDatabaseFileName);
        TestDatabasePath = Resolve(testDatabaseFileName);
        AndroidUiTestDatabasePath = Resolve(androidUiTestDatabaseFileName);

        EnsureDistinct(ProductionDatabasePath, TestDatabasePath);
        EnsureDistinct(ProductionDatabasePath, AndroidUiTestDatabasePath);
        EnsureDistinct(TestDatabasePath, AndroidUiTestDatabasePath);

        NormalDatabasePath = useAndroidUiTestDatabaseAsNormal
            ? AndroidUiTestDatabasePath
            : ProductionDatabasePath;
    }

    public string DataDirectory { get; }

    public string ProductionDatabasePath { get; }

    public string TestDatabasePath { get; }

    public string AndroidUiTestDatabasePath { get; }

    public string NormalDatabasePath { get; }

    public string GetPath(DatabaseMode mode) =>
        mode == DatabaseMode.Test ? TestDatabasePath : NormalDatabasePath;

    internal void ValidateTestResetTarget()
    {
        var expectedDirectory = Path.TrimEndingDirectorySeparator(DataDirectory);
        var testPath = Path.GetFullPath(TestDatabasePath);
        var testDirectory = Path.GetDirectoryName(testPath);

        if (
            !string.Equals(testDirectory, expectedDirectory, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                Path.GetFileName(testPath),
                DedicatedTestDatabaseFileName,
                StringComparison.Ordinal
            )
            || PathsEqual(testPath, ProductionDatabasePath)
            || PathsEqual(testPath, AndroidUiTestDatabasePath)
            || string.Equals(
                Path.GetFileName(testPath),
                Path.GetFileName(ProductionDatabasePath),
                StringComparison.OrdinalIgnoreCase
            )
        )
            throw new InvalidOperationException(
                "The product Test database reset target is not the dedicated Test database."
            );
    }

    private string Resolve(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(DataDirectory, fileName));
        if (
            !string.Equals(
                Path.GetDirectoryName(path),
                DataDirectory,
                StringComparison.OrdinalIgnoreCase
            )
        )
            throw new InvalidOperationException(
                "A database path escaped the application-data directory."
            );
        return path;
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

    private static void EnsureDistinct(string first, string second)
    {
        if (PathsEqual(first, second))
            throw new ArgumentException("Configured database paths must be distinct.");
    }

    private static bool PathsEqual(string first, string second) =>
        string.Equals(
            Path.GetFullPath(first),
            Path.GetFullPath(second),
            StringComparison.OrdinalIgnoreCase
        );
}
