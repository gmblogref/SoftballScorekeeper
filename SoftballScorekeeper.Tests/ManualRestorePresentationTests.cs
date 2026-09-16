using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class ManualRestorePresentationTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")
    );

    [Fact]
    public void Data_management_restore_action_and_dependencies_are_wired()
    {
        var page = XDocument.Load(
            Path.Combine(
                RepositoryRoot,
                "SoftballScorekeeper",
                "Views",
                "DataManagementPage.xaml"
            )
        );
        var button = page
            .Descendants()
            .Single(item => item.Attribute("AutomationId")?.Value == "RestoreDataButton");
        var startup = Read("SoftballScorekeeper", "MauiProgram.cs");

        Assert.Equal(
            "{x:Static strings:AppResources.RestoreDataButton}",
            button.Attribute("Text")?.Value
        );
        Assert.Equal("{Binding RestoreDataCommand}", button.Attribute("Command")?.Value);
        Assert.Equal("56", button.Attribute("MinimumHeightRequest")?.Value);
        Assert.Contains("AddSingleton<IRestoreSource, MauiRestoreSource>()", startup);
        Assert.Contains("AddSingleton<DatabaseRestoreService>()", startup);
        Assert.Contains("AddSingleton<ManualRestoreService>()", startup);
    }

    [Fact]
    public void Android_uses_system_open_document_and_private_copy_without_storage_permissions()
    {
        var source = Read(
            "SoftballScorekeeper",
            "Platforms",
            "Android",
            "Services",
            "MauiRestoreSource.Android.cs"
        );
        var manifest = Read(
            "SoftballScorekeeper",
            "Platforms",
            "Android",
            "AndroidManifest.xml"
        );

        Assert.Contains("Intent.ActionOpenDocument", source);
        Assert.Contains("ContentResolver?.OpenInputStream", source);
        Assert.Contains("CopyToPrivateCacheAsync", source);
        Assert.DoesNotContain("READ_EXTERNAL_STORAGE", manifest);
        Assert.DoesNotContain("WRITE_EXTERNAL_STORAGE", manifest);
        Assert.DoesNotContain("MANAGE_EXTERNAL_STORAGE", manifest);
    }

    [Fact]
    public void Restore_requires_validated_metadata_confirmation_and_resets_navigation_on_success()
    {
        var viewModel = Read(
            "SoftballScorekeeper",
            "ViewModels",
            "DataManagementViewModel.cs"
        );
        var page = Read("SoftballScorekeeper", "Views", "DataManagementPage.xaml.cs");

        Assert.Contains("RestoreConfirmationRequested", viewModel);
        Assert.Contains("BackupDatabaseCompatibility.OlderSupported", page);
        Assert.Contains("AppResources.RestoreDataConfirmationMessage", page);
        Assert.Contains("AppResources.RestoreDataOlderConfirmationMessage", page);
        Assert.Contains("GoToAsync(AppShell.TeamsSectionRoute)", viewModel);
        Assert.Contains("[NotifyCanExecuteChangedFor(nameof(RestoreDataCommand))]", viewModel);
    }

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, .. path]));
}
