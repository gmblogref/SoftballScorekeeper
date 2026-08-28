namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class AndroidSessionSmokeTests
{
    private readonly AndroidAppiumFixture fixture;

    public AndroidSessionSmokeTests(AndroidAppiumFixture fixture)
    {
        this.fixture = fixture;
    }

    [AndroidUiFact]
    public void Installed_application_creates_an_Appium_session()
    {
        fixture.ResetApplication();
        Assert.False(string.IsNullOrWhiteSpace(fixture.Driver.SessionId?.ToString()));
    }
}
