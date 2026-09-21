namespace SoftballScorekeeper.Configuration;

public sealed class ApplicationSettings
{
    public string DatabaseFileName { get; init; } = "SoftballScorekeeper.db3";

    public string TestDatabaseFileName { get; init; } = "SoftballScorekeeper.Test.db3";

    public string AndroidUiTestDatabaseFileName { get; init; } =
        "SoftballScorekeeper.AndroidUiTests.db3";
}
