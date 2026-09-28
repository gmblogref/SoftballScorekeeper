using OpenQA.Selenium;

namespace SoftballScorekeeper.AndroidUiTests;

[Collection(AndroidUiTestCollection.Name)]
public sealed class TeamLifecycleTests(AndroidAppiumFixture fixture)
{
    [AndroidUiFact]
    public void Team_can_be_created_edited_deactivated_and_reactivated_through_navigation()
    {
        PhaseAUiWorkflow.Run(fixture, nameof(
            Team_can_be_created_edited_deactivated_and_reactivated_through_navigation), driver =>
        {
            AndroidElementWait.UntilText(driver, "Automation Team");
            PhaseAUiWorkflow.NavigateSection(driver, "Teams", "Create Team");
            AndroidElementWait.UntilText(driver, "Create Team").Click();
            AndroidElementWait.UntilText(driver, "Create Team");
            var entries = PhaseAUiWorkflow.VisibleEditTexts(driver);
            Assert.Equal(2, entries.Length);
            entries[0].SendKeys("PHASE A TEAM");
            PhaseAUiWorkflow.ReplaceText(entries[1], "2027");
            AndroidElementWait.UntilText(driver, "Save").Click();

            AndroidElementWait.UntilAutomationIdText(driver, "TeamYear-2", "2027");
            AndroidElementWait.UntilAutomationIdText(driver, "TeamName-2", "PHASE A TEAM");
            AndroidElementWait.UntilAutomationId(driver, "TeamCard-2").Click();
            AndroidElementWait.UntilText(driver, "PHASE A TEAM");
            AndroidElementWait.UntilAutomationId(driver, "TeamWorkspaceGamesSectionButton").Click();
            AndroidElementWait.UntilText(driver, "No Games have been scheduled for this Team.");
            AndroidElementWait.UntilAutomationId(driver, "TeamWorkspaceRosterSectionButton").Click();
            AndroidElementWait.UntilText(
                driver, "This Team does not have any active roster Players.");

            AndroidElementWait.UntilAutomationId(driver, "EditTeamNameButton").Click();
            var edit = PhaseAUiWorkflow.VisibleEditTexts(driver).Single();
            PhaseAUiWorkflow.ReplaceText(edit, "PHASE A TEAM EDITED");
            AndroidElementWait.UntilText(driver, "Save").Click();
            AndroidElementWait.UntilText(driver, "PHASE A TEAM EDITED");

            fixture.SetOrientation(ScreenOrientation.Portrait);
            Assert.True(AndroidTeamWorkspaceMenu.UntilAvailable(driver).Displayed);
            fixture.SetOrientation(ScreenOrientation.Landscape);
            AndroidElementWait.UntilText(driver, "PHASE A TEAM EDITED");

            AndroidTeamWorkspaceMenu.Select(driver, "Deactivate Team");
            AndroidElementWait.UntilText(driver, "Deactivate Team");
            AndroidElementWait.UntilText(driver, "Deactivate").Click();
            AndroidElementWait.UntilText(driver, "Automation Team");
            PhaseAUiWorkflow.AssertTextAbsent(driver, "PHASE A TEAM EDITED");

            PhaseAUiWorkflow.OpenShellFlyout(driver);
            AndroidElementWait.UntilText(driver, "Manage Inactive Teams").Click();
            var inactiveName = AndroidElementWait.UntilText(driver, "PHASE A TEAM EDITED");
            Assert.True(inactiveName.Displayed);
            PhaseAUiWorkflow.NearestByText(driver, "Reactivate", inactiveName).Click();
            AndroidElementWait.UntilText(driver, "Reactivate Team");
            AndroidElementWait.UntilText(driver, "Reactivate").Click();

            AndroidElementWait.UntilAutomationIdText(driver, "TeamYear-2", "2027");
            AndroidElementWait.UntilAutomationIdText(driver, "TeamName-2", "PHASE A TEAM EDITED");
            AndroidElementWait.UntilAutomationId(driver, "TeamCard-2").Click();
            AndroidElementWait.UntilText(driver, "PHASE A TEAM EDITED");
        });
    }
}
