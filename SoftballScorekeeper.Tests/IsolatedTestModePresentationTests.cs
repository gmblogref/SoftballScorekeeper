using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class IsolatedTestModePresentationTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")
    );

    [Fact]
    public void Shell_exposes_mutually_visible_mode_actions_and_absolute_navigation_reset()
    {
        var shell = XDocument.Load(ReadPath("SoftballScorekeeper", "AppShell.xaml"));
        var code = File.ReadAllText(ReadPath("SoftballScorekeeper", "AppShell.xaml.cs"));
        var enter = shell
            .Descendants()
            .Single(item => item.Attribute("AutomationId")?.Value == "EnterTestModeMenuItem");
        var exit = shell
            .Descendants()
            .Single(item => item.Attribute("AutomationId")?.Value == "ExitTestModeMenuItem");

        Assert.Equal("EnterTestModeMenuItem", enter.Attribute("{http://schemas.microsoft.com/winfx/2009/xaml}Name")?.Value);
        Assert.Equal("ExitTestModeMenuItem", exit.Attribute("{http://schemas.microsoft.com/winfx/2009/xaml}Name")?.Value);
        Assert.Contains("UpdateModeMenuVisibility()", code);
        Assert.Contains("databaseModeState.IsProductionMode", code);
        Assert.Contains("databaseModeState.IsTestMode", code);
        Assert.Contains("EnterTestModeAsync", code);
        Assert.Contains("ExitTestModeAsync", code);
        Assert.Contains("mainPage.ReloadAsync()", code);
        Assert.Contains("GoToAsync(TeamsSectionRoute)", code);
        Assert.Contains("DeleteTestDataChoice", code);
        Assert.Contains("KeepTestDataChoice", code);
    }

    [Fact]
    public void Shared_content_page_style_attaches_test_mode_indicator_without_a_control_template()
    {
        var styles = XDocument.Load(
            ReadPath("SoftballScorekeeper", "Resources", "Styles", "Styles.xaml")
        );
        var contentPageStyle = styles
            .Descendants()
            .Single(item =>
                item.Name.LocalName == "Style"
                && item.Attribute("TargetType")?.Value == "ContentPage"
            );

        Assert.Contains("TestModeIndicator.IsEnabled", contentPageStyle.ToString());
        Assert.DoesNotContain("ControlTemplate", contentPageStyle.ToString());

        var indicator = File.ReadAllText(
            ReadPath("SoftballScorekeeper", "Services", "TestModeIndicator.cs")
        );
        Assert.Contains("AppResources.TestModeIndicator", indicator);
        Assert.Contains("AutomationId = \"TestModeIndicator\"", indicator);
        Assert.Contains("nameof(DatabaseModeState.IsTestMode)", indicator);
        Assert.Contains("FontAttributes.Bold", indicator);
    }

    [Fact]
    public void Test_mode_indicator_detaches_windows_content_before_wrapping_it()
    {
        var indicator = File.ReadAllText(
            ReadPath("SoftballScorekeeper", "Services", "TestModeIndicator.cs")
        );
        var originalContentCapture = indicator.IndexOf(
            "var originalContent = page.Content;",
            StringComparison.Ordinal
        );
        var windowsGuard = indicator.IndexOf("#if WINDOWS", StringComparison.Ordinal);
        var contentDetach = indicator.IndexOf("page.Content = null;", StringComparison.Ordinal);
        var indicatorBinding = indicator.IndexOf(
            "nameof(DatabaseModeState.IsTestMode)",
            StringComparison.Ordinal
        );
        var originalContentWrap = indicator.IndexOf(
            "layout.Add(originalContent, 0, 1);",
            StringComparison.Ordinal
        );
        var wrappedContentAssignment = indicator.IndexOf(
            "page.Content = layout;",
            StringComparison.Ordinal
        );

        Assert.True(originalContentCapture >= 0);
        Assert.True(windowsGuard > originalContentCapture);
        Assert.True(contentDetach > windowsGuard);
        Assert.True(originalContentWrap > contentDetach);
        Assert.True(wrappedContentAssignment > originalContentWrap);
        Assert.True(indicatorBinding >= 0);
        Assert.Contains("(bool)page.GetValue(IsWrappedProperty)", indicator);
        Assert.Contains("page.SetValue(IsWrappedProperty, true)", indicator);
    }

    [Fact]
    public void Data_management_commands_and_services_have_test_mode_guards()
    {
        var viewModel = File.ReadAllText(
            ReadPath("SoftballScorekeeper", "ViewModels", "DataManagementViewModel.cs")
        );
        var page = XDocument.Load(
            ReadPath("SoftballScorekeeper", "Views", "DataManagementPage.xaml")
        );
        var backupService = File.ReadAllText(
            ReadPath("SoftballScorekeeper.Infrastructure", "Backup", "ManualBackupService.cs")
        );
        var restoreService = File.ReadAllText(
            ReadPath("SoftballScorekeeper.Infrastructure", "Backup", "ManualRestoreService.cs")
        );

        Assert.Contains("CanUsePortableBackupAndRestore", viewModel);
        Assert.Contains("NotifyCanExecuteChanged", viewModel);
        Assert.Contains("CanUsePortableBackupAndRestore", backupService);
        Assert.Contains("CanUsePortableBackupAndRestore", restoreService);
        Assert.NotNull(
            page.Descendants()
                .Single(item =>
                    item.Attribute("AutomationId")?.Value == "PortableDataProductionOnlyMessage"
                )
        );
    }

    [Fact]
    public void Startup_resolves_persisted_mode_and_preserves_android_ui_normal_identity()
    {
        var startup = File.ReadAllText(ReadPath("SoftballScorekeeper", "MauiProgram.cs"));
        var settings = File.ReadAllText(
            ReadPath("SoftballScorekeeper", "Configuration", "ApplicationSettings.cs")
        );

        Assert.Contains("SoftballScorekeeper.Test.db3", settings);
        Assert.Contains("preferenceStore.Load()", startup);
        Assert.Contains("new AppDatabasePaths", startup);
        Assert.Contains("IsUiTestModeRequested", startup);
        Assert.Contains("preferenceStore.Save(DatabaseMode.Production)", startup);
        Assert.Contains("return new AppDatabase(paths, databaseModeState.CurrentMode)", startup);
    }

    private static string ReadPath(params string[] parts) =>
        Path.Combine([RepositoryRoot, .. parts]);
}
