using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class ManualBackupPresentationTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")
    );

    [Fact]
    public void Data_management_route_ui_and_dependencies_are_wired()
    {
        var shellXaml = Read("SoftballScorekeeper", "AppShell.xaml");
        var shellCode = Read("SoftballScorekeeper", "AppShell.xaml.cs");
        var page = XDocument.Load(Path.Combine(RepositoryRoot, "SoftballScorekeeper", "Views", "DataManagementPage.xaml"));
        var button = page.Descendants().Single(item => item.Attribute("AutomationId")?.Value == "BackupDataButton");
        var startup = Read("SoftballScorekeeper", "MauiProgram.cs");

        Assert.Contains("AppResources.DataManagementTitle", shellXaml);
        Assert.Contains("GoToAsync(DataManagementRoute)", shellCode);
        Assert.Equal("{x:Static strings:AppResources.BackupDataButton}", button.Attribute("Text")?.Value);
        Assert.Equal("{Binding BackupDataCommand}", button.Attribute("Command")?.Value);
        Assert.Equal("56", button.Attribute("MinimumHeightRequest")?.Value);
        Assert.Contains("AddSingleton<IBackupDestination, MauiBackupDestination>()", startup);
        Assert.Contains("AddSingleton<ManualBackupService>()", startup);
        Assert.Contains("AddTransient<DataManagementViewModel>()", startup);
    }

    [Fact]
    public void Android_uses_system_create_document_without_storage_permissions_or_cloud_dependencies()
    {
        var destination = Read("SoftballScorekeeper", "Platforms", "Android", "Services", "MauiBackupDestination.Android.cs");
        var manifest = Read("SoftballScorekeeper", "Platforms", "Android", "AndroidManifest.xml");

        Assert.Contains("Intent.ActionCreateDocument", destination);
        Assert.Contains("ContentResolver?.OpenOutputStream", destination);
        Assert.Contains("source.CopyToAsync(destination", destination);
        Assert.DoesNotContain("READ_EXTERNAL_STORAGE", manifest);
        Assert.DoesNotContain("WRITE_EXTERNAL_STORAGE", manifest);
        Assert.DoesNotContain("MANAGE_EXTERNAL_STORAGE", manifest);
        Assert.DoesNotContain("Google", destination);
    }

    [Fact]
    public void View_model_reports_only_saved_or_failed_and_uses_busy_command_state()
    {
        var viewModel = Read("SoftballScorekeeper", "ViewModels", "DataManagementViewModel.cs");

        Assert.Contains("result == ManualBackupResult.Saved", viewModel);
        Assert.Contains("AppResources.BackupDataSucceeded", viewModel);
        Assert.Contains("AppResources.BackupDataFailed", viewModel);
        Assert.Contains("[NotifyCanExecuteChangedFor(nameof(BackupDataCommand))]", viewModel);
        Assert.Contains("private bool CanBackupData() => !IsBusy", viewModel);
        Assert.DoesNotContain("ManualBackupResult.Cancelled)", viewModel);
    }

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, .. path]));
}
