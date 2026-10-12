using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class BeerTotalsPresentationTests
{
    private const string PlayerColumns = "180,2";
    private const string StatisticColumns = "72,88,72,72,88,100,88,100,88";

    [Fact]
    public void Grid_freezes_headers_and_player_column_with_shared_row_alignment()
    {
        var grid = LoadXaml("Views", "Controls", "BeerTotalsGrid.xaml");
        var ns = grid.Root!.Name.Namespace;
        var named = NamedElements(grid);
        var playerRow = Assert.Single(named["BeerTotalsPlayerNames"]
            .Descendants(ns + "DataTemplate")).Elements().Single();
        var statisticRow = Assert.Single(named["BeerTotalsPlayerRows"]
            .Descendants(ns + "DataTemplate")).Elements().Single();
        var scrolls = grid.Descendants(ns + "ScrollView").ToArray();

        Assert.Equal("Auto,*", named["BeerTotalsTable"].Attribute("RowDefinitions")?.Value);
        Assert.Equal("182,*", named["BeerTotalsHeader"].Attribute("ColumnDefinitions")?.Value);
        Assert.Equal("182,*", named["BeerTotalsBody"].Attribute("ColumnDefinitions")?.Value);
        Assert.Equal(PlayerColumns, named["BeerTotalsPlayerHeader"].Attribute("ColumnDefinitions")?.Value);
        Assert.Equal(PlayerColumns, playerRow.Attribute("ColumnDefinitions")?.Value);
        Assert.Equal(StatisticColumns, named["BeerTotalsScrollableHeader"].Attribute("ColumnDefinitions")?.Value);
        Assert.Equal(StatisticColumns, statisticRow.Attribute("ColumnDefinitions")?.Value);
        Assert.Equal("52,1", playerRow.Attribute("RowDefinitions")?.Value);
        Assert.Equal("52,1", statisticRow.Attribute("RowDefinitions")?.Value);
        Assert.Equal("{Binding Rows}", named["BeerTotalsPlayerNames"].Attribute("BindableLayout.ItemsSource")?.Value);
        Assert.Equal("{Binding Rows}", named["BeerTotalsPlayerRows"].Attribute("BindableLayout.ItemsSource")?.Value);
        Assert.Equal(2, scrolls.Length);
        Assert.Equal("Vertical", named["BeerTotalsVerticalScroll"].Attribute("Orientation")?.Value);
        Assert.Equal("Horizontal", named["BeerTotalsHorizontalScroll"].Attribute("Orientation")?.Value);
        Assert.Equal("Always", named["BeerTotalsHorizontalScroll"].Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Contains(named["BeerTotalsVerticalScroll"], named["BeerTotalsHorizontalScroll"].Ancestors());
        Assert.Empty(named["BeerTotalsHeader"].Ancestors(ns + "ScrollView"));
        Assert.Equal("OnStatisticsScrolled", named["BeerTotalsHorizontalScroll"].Attribute("Scrolled")?.Value);
        Assert.Contains("BeerTotalsScrollableHeader.TranslationX = -e.ScrollX", ReadText("Views", "Controls", "BeerTotalsGrid.xaml.cs"));
        Assert.Empty(grid.Descendants(ns + "VisualStateManager.VisualStateGroups"));
        Assert.Equal(new[]
        {
            "{x:Static strings:AppResources.BeerTotalsOwedHeader}",
            "{x:Static strings:AppResources.BeerTotalsBroughtHeader}",
            "{x:Static strings:AppResources.BeerTotalsDboHeader}",
            "{x:Static strings:AppResources.BeerTotalsSoloHomeRunHeader}",
            "{x:Static strings:AppResources.BeerTotalsDoublePlayHeader}",
            "{x:Static strings:AppResources.BeerTotalsTriplePlayHeader}",
            "{x:Static strings:AppResources.BeerTotalsTwoOutsHeader}",
            "{x:Static strings:AppResources.BeerTotalsStrikeoutHeader}",
            "{x:Static strings:AppResources.BeerTotalsLowManHeader}",
        }, named["BeerTotalsScrollableHeader"].Elements(ns + "Label")
            .Select(item => item.Attribute("Text")?.Value));
        Assert.Contains(named["BeerTotalsPlayerHeader"].Elements(ns + "Label"), item =>
            item.Attribute("Text")?.Value == "{x:Static strings:AppResources.BeerTotalsPlayerHeader}");
        Assert.DoesNotContain("StillOwed", grid.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TeamTotal", grid.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Editable_cells_are_neutral_and_owed_is_read_only_yellow_bold_and_first()
    {
        var grid = LoadXaml("Views", "Controls", "BeerTotalsGrid.xaml");
        var ns = grid.Root!.Name.Namespace;
        var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2009/xaml");
        var named = NamedElements(grid);
        var playerRow = Assert.Single(named["BeerTotalsPlayerNames"]
            .Descendants(ns + "DataTemplate")).Elements().Single();
        var row = Assert.Single(named["BeerTotalsPlayerRows"]
            .Descendants(ns + "DataTemplate")).Elements().Single();
        var buttons = row.Descendants(ns + "Button").ToArray();
        var entries = row.Descendants(ns + "Entry").ToArray();
        var styles = grid.Descendants(ns + "Style").ToDictionary(
            item => item.Attribute(x + "Key")!.Value);
        var editButtonSetters = styles["BeerTotalEditButton"].Elements(ns + "Setter")
            .ToDictionary(item => item.Attribute("Property")!.Value,
                item => item.Attribute("Value")!.Value);
        var entrySetters = styles["BeerTotalEditEntry"].Elements(ns + "Setter")
            .ToDictionary(item => item.Attribute("Property")!.Value,
                item => item.Attribute("Value")!.Value);
        var owedSetters = styles["BeerTotalOwedValue"].Elements(ns + "Setter")
            .ToDictionary(item => item.Attribute("Property")!.Value,
                item => item.Attribute("Value")!.Value);

        Assert.Equal(8, buttons.Length);
        Assert.Equal(8, entries.Length);
        Assert.All(buttons, button => Assert.Equal(
            "{StaticResource BeerTotalEditButton}", button.Attribute("Style")?.Value));
        Assert.Equal("48", editButtonSetters["MinimumHeightRequest"]);
        Assert.Equal("Transparent", editButtonSetters["BackgroundColor"]);
        Assert.Equal("0", editButtonSetters["BorderWidth"]);
        Assert.Equal("0", editButtonSetters["CornerRadius"]);
        Assert.Equal("Numeric", entrySetters["Keyboard"]);
        Assert.Equal("Done", entrySetters["ReturnType"]);
        Assert.Contains("WarningSurface", entrySetters["BackgroundColor"]);
        Assert.Contains(playerRow.Elements(ns + "Label"), item =>
            item.Attribute("Text")?.Value == "{Binding PlayerDisplayName}");
        Assert.Contains(row.Elements(ns + "Label"), item =>
            item.Attribute("Text")?.Value == "{Binding Owed}"
            && item.Attribute("Grid.Column") is null
            && item.Attribute("Style")?.Value == "{StaticResource BeerTotalOwedValue}");
        Assert.Contains(buttons, item => item.Attribute("Text")?.Value == "{Binding Brought}"
            && item.Parent?.Attribute("Grid.Column")?.Value == "1");
        Assert.All(entries, entry =>
        {
            Assert.EndsWith("Editing}", entry.Attribute("IsVisible")?.Value);
            Assert.Equal("OnEditCompleted", entry.Attribute("Completed")?.Value);
            Assert.Equal("{StaticResource BeerTotalEditEntry}", entry.Attribute("Style")?.Value);
        });
        Assert.Equal(8, row.Descendants(ns + "DataTrigger").Count(trigger =>
            trigger.Descendants(ns + "Setter").Any(setter =>
                setter.Attribute("Value")?.Value.Contains("WarningSurface") == true)));
        Assert.Equal("Bold", owedSetters["FontAttributes"]);
        Assert.Contains("WarningSurface", owedSetters["BackgroundColor"]);
        Assert.Contains("WarningText", owedSetters["TextColor"]);
    }

    [Fact]
    public void View_model_delegates_authoritative_load_and_writes_then_reloads()
    {
        var source = ReadText("ViewModels", "TeamBeerTotalsViewModel.cs");

        Assert.Contains("beerAccountingRepository.GetTeamLifetimeAsync(TeamId)", source);
        Assert.Contains("beerAccountingRepository.SetEffectiveRuleCountAsync(", source);
        Assert.Contains("beerAccountingRepository.SetBroughtAsync(", source);
        Assert.Contains("int.TryParse(enteredValue?.Trim()", source);
        Assert.Contains("value < 0", source);
        Assert.Contains("NonNegativeWholeNumberRequiredMessage", source);
        Assert.Contains("if (!await LoadSummaryAsync())", source);
        Assert.DoesNotContain("BeerLedgerEntry", source);
        Assert.DoesNotContain("RuleCountAdjustment", source);
        Assert.DoesNotContain("Owed =", source);
        Assert.DoesNotContain("OrderBy", source);
    }

    [Fact]
    public void View_model_preserves_context_for_empty_modal_navigation_query()
    {
        var source = ReadText("ViewModels", "TeamBeerTotalsViewModel.cs");

        Assert.Matches(
            new Regex(
                @"if\s*\(\s*!query\.TryGetValue\([^)]*TeamIdQueryParameter[^)]*\)\s*\)\s*\{\s*if\s*\(\s*!hasValidTeamId\s*\)\s*\{\s*Reset\(\);\s*ValidationMessage\s*=\s*AppResources\.BeerTotalsLoadFailed;\s*\}\s*return;\s*\}",
                RegexOptions.Singleline),
            source);
    }

    [Fact]
    public void Inline_editing_replaces_the_modal_and_supports_done_or_cell_switch_save()
    {
        var shell = ReadText("AppShell.xaml.cs");
        var composition = ReadText("MauiProgram.cs");
        var page = ReadText("Views", "TeamBeerTotalsPage.xaml.cs");
        var gridCode = ReadText("Views", "Controls", "BeerTotalsGrid.xaml.cs");
        var grid = LoadXaml("Views", "Controls", "BeerTotalsGrid.xaml");
        var ns = grid.Root!.Name.Namespace;

        Assert.Contains("TeamBeerTotalsRoute", shell);
        Assert.Contains("typeof(Views.TeamBeerTotalsPage)", shell);
        Assert.Contains("AddSingleton<BeerAccountingRepository>()", composition);
        Assert.Contains("AddTransient<TeamBeerTotalsViewModel>()", composition);
        Assert.Contains("AddTransient<TeamBeerTotalsPage>()", composition);
        Assert.Contains("TotalsGrid.SaveRequested = viewModel.SaveEditAsync", page);
        Assert.DoesNotContain("PushModalAsync", page);
        Assert.Contains("await SaveActiveEditAsync()", gridCode);
        Assert.Contains("activeRow is not null && !await SaveActiveEditAsync()", gridCode);
        Assert.Contains("SelectionLength", gridCode);
        Assert.Contains("isSaving", gridCode);
        Assert.Contains("OnAndroidEditorAction", gridCode);
        Assert.Contains("ImeAction.Done", gridCode);
        Assert.Contains("completedEntry.Unfocus()", gridCode);
        Assert.Equal("Numeric", grid.Descendants(ns + "Style")
            .Single(style => style.Attribute(XNamespace.Get(
                "http://schemas.microsoft.com/winfx/2009/xaml") + "Key")?.Value
                == "BeerTotalEditEntry")
            .Elements(ns + "Setter").Single(setter =>
                setter.Attribute("Property")?.Value == "Keyboard")
            .Attribute("Value")?.Value);
        Assert.DoesNotContain("BeerTotalEditDialog", page);
        Assert.False(File.Exists(Path.Combine(
            RepositoryRoot, "SoftballScorekeeper", "Views", "BeerTotalEditDialog.xaml")));
    }

    [Fact]
    public void Beer_totals_hosts_schedule_backed_deliberate_low_man_calculation()
    {
        var page = LoadXaml("Views", "TeamBeerTotalsPage.xaml");
        var ns = page.Root!.Name.Namespace;
        var source = ReadText("ViewModels", "TeamBeerTotalsViewModel.cs");
        var composition = ReadText("MauiProgram.cs");
        var grid = ReadText("Views", "Controls", "BeerTotalsGrid.xaml");

        var picker = Assert.Single(page.Descendants(ns + "Picker"));
        var button = Assert.Single(page.Descendants(ns + "Button").Where(item =>
            item.Attribute("AutomationId")?.Value == "CalculateLowManButton"));
        Assert.Equal("{Binding GameNights}", picker.Attribute("ItemsSource")?.Value);
        Assert.Equal("{Binding SelectedGameNight}", picker.Attribute("SelectedItem")?.Value);
        Assert.Equal("OnCalculateLowManClicked", button.Attribute("Clicked")?.Value);
        Assert.Contains("lowManCalculationRepository.GetGameNightsAsync(TeamId)", source);
        Assert.Contains("lowManCalculationRepository.CalculateAsync(", source);
        Assert.Contains("gameNight.IncompleteGameCount", source);
        Assert.Contains("await LoadSummaryAsync()", source);
        Assert.Contains("AddSingleton<LowManCalculationRepository>()", composition);
        Assert.DoesNotContain("CalculateLowMan", grid);
        Assert.DoesNotContain("BattingStatistics", grid);
    }

    [Fact]
    public void Low_man_result_message_uses_authoritative_winner_names_and_preserves_no_result_text()
    {
        var source = ReadText("ViewModels", "TeamBeerTotalsViewModel.cs");
        var resources = XDocument.Load(Path.Combine(
            RepositoryRoot,
            "SoftballScorekeeper",
            "Resources",
            "Strings",
            "AppResources.resx"));
        var values = resources.Root!.Elements("data").ToDictionary(
            item => item.Attribute("name")!.Value,
            item => item.Element("value")!.Value);

        Assert.Equal("Low Man: {0}", values["LowManCalculationSucceeded"]);
        Assert.Contains("result.Winners.Count > 0", source);
        Assert.Contains("AppResources.LowManCalculationNoResult", source);
        Assert.Contains(
            "string.Join(\", \", winners.Select(item => item.DisplayName))",
            source);
        Assert.DoesNotContain("Rows.Where", source);
    }

    [Fact]
    public void Selected_date_loads_persisted_low_man_state_and_switches_action_text()
    {
        var page = LoadXaml("Views", "TeamBeerTotalsPage.xaml");
        var ns = page.Root!.Name.Namespace;
        var source = ReadText("ViewModels", "TeamBeerTotalsViewModel.cs");
        var button = Assert.Single(page.Descendants(ns + "Button").Where(item =>
            item.Attribute("AutomationId")?.Value == "CalculateLowManButton"));

        Assert.Equal("{Binding LowManActionText}", button.Attribute("Text")?.Value);
        Assert.Equal("{Binding CanSelectGameNight}",
            Assert.Single(page.Descendants(ns + "Picker")).Attribute("IsEnabled")?.Value);
        Assert.Contains("AppResources.RecalculateLowManButton", source);
        Assert.Contains("AppResources.CalculateLowManButton", source);
        Assert.Contains("GetPersistedAutomaticWinnersAsync(TeamId, selected.PlayingDate)",
            source);
        Assert.Contains("partial void OnSelectedGameNightChanged", source);
        Assert.Contains("ClearSelectedLowManState();", source);
        Assert.Contains("HasCalculatedLowMan = winners.Count > 0", source);
        Assert.Contains("if (SelectedGameNight != selected)", source);
        Assert.Contains("await LoadSelectedLowManStateAsync();", source);
        Assert.DoesNotContain("SetEffectiveRuleCountAsync(TeamId", source);
    }

    private static XDocument LoadXaml(params string[] path) => XDocument.Load(
        Path.Combine(new[] { RepositoryRoot, "SoftballScorekeeper" }.Concat(path).ToArray()));

    private static string ReadText(params string[] path) => File.ReadAllText(
        Path.Combine(new[] { RepositoryRoot, "SoftballScorekeeper" }.Concat(path).ToArray()));

    private static Dictionary<string, XElement> NamedElements(XDocument page)
    {
        var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2009/xaml");
        return page.Descendants().Where(item => item.Attribute(x + "Name") is not null)
            .ToDictionary(item => item.Attribute(x + "Name")!.Value);
    }

    private static string RepositoryRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));
}
