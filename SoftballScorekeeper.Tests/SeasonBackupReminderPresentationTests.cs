using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class SeasonBackupReminderPresentationTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")
    );

    [Fact]
    public void Root_ready_workflow_uses_resource_backed_three_action_dialog_and_existing_backup()
    {
        var mainPage = Read("SoftballScorekeeper", "Views", "MainPage.xaml.cs");
        var reminderService = Read(
            "SoftballScorekeeper.Infrastructure",
            "Backup",
            "SeasonBackupReminderService.cs"
        );
        var resources = XDocument.Load(
            Path.Combine(
                RepositoryRoot,
                "SoftballScorekeeper",
                "Resources",
                "Strings",
                "AppResources.resx"
            )
        );
        var startup = Read("SoftballScorekeeper", "MauiProgram.cs");

        Assert.True(
            mainPage.IndexOf("await ReloadAsync()", StringComparison.Ordinal)
            < mainPage.IndexOf("RunAtStartupAsync", StringComparison.Ordinal)
        );
        Assert.Contains("DisplayActionSheetAsync(", mainPage);
        Assert.Contains("AppResources.OkButton", mainPage);
        Assert.Contains("AppResources.SeasonBackupReminderRemindLater", mainPage);
        Assert.Contains("AppResources.SeasonBackupReminderDismiss", mainPage);
        Assert.Contains("manualBackupService.RunAsync", reminderService);
        Assert.DoesNotContain("GoToAsync", reminderService);
        Assert.Contains("AddSingleton<SeasonBackupReminderService>()", startup);
        Assert.Contains("AddSingleton<SeasonBackupReminderRepository>()", startup);

        var values = resources
            .Descendants("data")
            .ToDictionary(
                item => item.Attribute("name")!.Value,
                item => item.Element("value")!.Value
            );
        Assert.Equal(
            "One or more League Seasons have finished. Back up your Softball Scorekeeper data?",
            values["SeasonBackupReminderMessage"]
        );
        Assert.Equal("Remind Later", values["SeasonBackupReminderRemindLater"]);
        Assert.Equal("Dismiss", values["SeasonBackupReminderDismiss"]);
    }

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, .. path]));
}
