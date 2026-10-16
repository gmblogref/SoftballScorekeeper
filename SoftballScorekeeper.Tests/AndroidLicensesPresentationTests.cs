using System.Text.RegularExpressions;
using System.Text.Json;
using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class AndroidLicensesPresentationTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")
    );

    [Fact]
    public void Shell_exposes_android_only_licenses_navigation()
    {
        var shell = XDocument.Load(ReadPath("SoftballScorekeeper", "AppShell.xaml"));
        var shellCode = Read("SoftballScorekeeper", "AppShell.xaml.cs");
        var menuItem = shell
            .Descendants()
            .Single(item => item.Attribute("AutomationId")?.Value == "LicensesMenuItem");

        Assert.Equal(
            "{x:Static strings:AppResources.LicensesTitle}",
            menuItem.Attribute("Text")?.Value
        );
        Assert.Contains("Routing.RegisterRoute(LicensesRoute", shellCode);
        Assert.Contains("GoToAsync(LicensesRoute)", shellCode);
        Assert.Contains("DeviceInfo.Platform == DevicePlatform.Android", shellCode);
    }

    [Fact]
    public void Page_is_localized_scrollable_and_inherits_the_test_mode_indicator()
    {
        var page = XDocument.Load(
            ReadPath("SoftballScorekeeper", "Views", "LicensesPage.xaml")
        );
        var styles = Read("SoftballScorekeeper", "Resources", "Styles", "Styles.xaml");
        var scrollView = page
            .Descendants()
            .Single(item => item.Attribute("AutomationId")?.Value == "LicenseDocumentScrollView");
        var picker = page
            .Descendants()
            .Single(item => item.Attribute("AutomationId")?.Value == "LicenseDocumentPicker");

        Assert.Equal("ScrollView", scrollView.Name.LocalName);
        Assert.Equal("Picker", picker.Name.LocalName);
        Assert.Contains("AppResources.LicensesTitle", page.ToString());
        Assert.Contains("AppResources.LicensesRequiredNotice", page.ToString());
        Assert.Contains("AppResources.LicenseDocumentUnavailable", Read("SoftballScorekeeper", "Views", "LicensesPage.xaml.cs"));
        Assert.Contains("services:TestModeIndicator.IsEnabled", styles);
    }

    [Fact]
    public void Android_catalog_maps_every_document_to_an_existing_non_windows_source()
    {
        var catalog = Read("SoftballScorekeeper", "Services", "LicenseDocumentCatalog.cs");
        var paths = Regex
            .Matches(catalog, "\"(licenses/[^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.Equal(21, paths.Length);
        Assert.Equal(paths.Length, paths.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(paths, path => path.Contains("/windows/", StringComparison.Ordinal));
        foreach (var assetPath in paths)
            Assert.True(
                File.Exists(SourcePath(assetPath)),
                $"Missing source for packaged asset {assetPath}"
            );
    }

    [Fact]
    public void Android_asset_items_include_only_application_common_and_android_documents()
    {
        var project = Read("SoftballScorekeeper", "SoftballScorekeeper.csproj");

        Assert.Contains("GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'", project);
        Assert.Contains("..\\LICENSE", project);
        Assert.Contains("..\\THIRD-PARTY-NOTICES.md", project);
        Assert.Contains("..\\licenses\\distribution\\common\\*", project);
        Assert.Contains("..\\licenses\\distribution\\android\\*", project);
        Assert.DoesNotContain("..\\licenses\\distribution\\windows\\*", project);
    }

    [Fact]
    public void Mono_android_inventory_records_both_release_abis_at_version_9_0_21()
    {
        using var manifest = JsonDocument.Parse(
            Read("licenses", "distribution", "production-license-manifest.json")
        );
        var component = manifest
            .RootElement.GetProperty("components")
            .EnumerateArray()
            .Single(item =>
                item.GetProperty("component").GetString()
                    == ".NET Mono Android arm64 and x86_64 runtimes"
            );

        Assert.Equal("9.0.21", component.GetProperty("version").GetString());
        Assert.Equal(
            [
                "Microsoft.NETCore.App.Runtime.Mono.android-arm64/9.0.21",
                "Microsoft.NETCore.App.Runtime.Mono.android-x64/9.0.21",
            ],
            component
                .GetProperty("packages")
                .EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray()
        );

        var noticePath = component
            .GetProperty("required_notice_files")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Single(path => path!.Contains("THIRD-PARTY-NOTICES", StringComparison.Ordinal));
        Assert.EndsWith(
            "Microsoft.NETCore.App.Runtime.Mono.android-9.0.21-THIRD-PARTY-NOTICES.txt",
            noticePath,
            StringComparison.Ordinal
        );
        Assert.True(File.Exists(ReadPath(noticePath!.Split('/'))));
    }

    private static string SourcePath(string assetPath)
    {
        if (assetPath == "licenses/application/LICENSE.txt")
            return ReadPath("LICENSE");
        if (assetPath == "licenses/THIRD-PARTY-NOTICES.txt")
            return ReadPath("THIRD-PARTY-NOTICES.md");
        if (assetPath == "licenses/common/OpenSans-Apache-2.0.txt")
            return ReadPath("licenses", "OpenSans-Apache-2.0.txt");
        if (assetPath.StartsWith("licenses/common/", StringComparison.Ordinal))
            return ReadPath(
                "licenses",
                "distribution",
                "common",
                Path.GetFileName(assetPath)
            );
        if (assetPath.StartsWith("licenses/android/", StringComparison.Ordinal))
            return ReadPath(
                "licenses",
                "distribution",
                "android",
                Path.GetFileName(assetPath)
            );

        throw new InvalidOperationException($"Unexpected Android license asset: {assetPath}");
    }

    private static string Read(params string[] parts) => File.ReadAllText(ReadPath(parts));

    private static string ReadPath(params string[] parts) =>
        Path.Combine([RepositoryRoot, .. parts]);
}
