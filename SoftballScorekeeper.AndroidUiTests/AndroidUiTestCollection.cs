namespace SoftballScorekeeper.AndroidUiTests;

[CollectionDefinition(Name)]
public sealed class AndroidUiTestCollection : ICollectionFixture<AndroidAppiumFixture>
{
    public const string Name = "Android UI tests";
}
