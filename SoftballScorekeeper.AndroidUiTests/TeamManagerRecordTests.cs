namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class TeamManagerRecordTests
{
    private readonly AndroidAppiumFixture fixture;

    public TeamManagerRecordTests(AndroidAppiumFixture fixture)
    {
        this.fixture = fixture;
    }

    [AndroidUiFact]
    public void Latest_year_record_card_opens_the_correct_team_workspace()
    {
        fixture.ResetApplication(AndroidAutomationConstants.TeamRecordTestSeedRequestFileName);
        var driver = fixture.Driver;
        try
        {
            AndroidElementWait.UntilAutomationIdText(
                driver,
                "TeamName-1",
                "Automation Record Team"
            );
            AndroidElementWait.UntilAutomationIdText(driver, "TeamYear-1", "2026");
            AndroidElementWait.UntilAutomationIdText(driver, "TeamRecord-1", "Record: 1-1");

            AndroidElementWait.UntilAutomationId(driver, "TeamCard-1").Click();

            AndroidElementWait.UntilText(driver, "Automation Record Team");
        }
        catch (Exception exception)
        {
            var artifacts = fixture.CaptureFailureArtifacts(
                nameof(Latest_year_record_card_opens_the_correct_team_workspace),
                exception
            );
            throw new Xunit.Sdk.XunitException(
                $"Android Team Manager record workflow failed. Diagnostics: {artifacts}"
                    + $"{Environment.NewLine}{exception}"
            );
        }
    }
}
