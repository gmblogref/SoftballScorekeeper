using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class PlayerRosterEditingPresentationTests
{
    [Fact]
    public void Player_workspace_exposes_touch_friendly_edit_icon_for_existing_inline_editor()
    {
        var page = LoadXaml("Views", "PlayerWorkspacePage.xaml");
        var resources = XDocument.Load(
            Path.Combine(
                RepositoryRoot,
                "SoftballScorekeeper",
                "Resources",
                "Strings",
                "AppResources.resx"
            )
        );
        var ns = page.Root!.Name.Namespace;
        Assert.Contains(
            page.Descendants(ns + "Grid"),
            grid =>
                grid.Attribute("ColumnDefinitions")?.Value == "Auto,Auto"
                && grid.Attribute("HorizontalOptions")?.Value == "Center"
        );

        Assert.Contains(
            page.Descendants(ns + "TapGestureRecognizer"),
            gesture =>
                gesture.Attribute("Command")?.Value == "{Binding BeginPlayerNameEditCommand}"
        );
        var editIcon = Assert.Single(
            page.Descendants(ns + "Grid"),
            grid =>
                grid.Attribute("SemanticProperties.Description")?.Value
                    == "{x:Static strings:AppResources.PlayerNameEditHint}"
        );
        Assert.Equal("Transparent", editIcon.Attribute("BackgroundColor")?.Value);
        Assert.Equal("44", editIcon.Attribute("WidthRequest")?.Value);
        Assert.Equal("44", editIcon.Attribute("MinimumHeightRequest")?.Value);
        Assert.Contains(
            editIcon.Descendants(ns + "TapGestureRecognizer"),
            gesture =>
                gesture.Attribute("Command")?.Value == "{Binding BeginPlayerNameEditCommand}"
        );
        Assert.Contains(
            editIcon.Descendants(ns + "Label"),
            label =>
                label.Attribute("Text")?.Value == "{x:Static strings:AppResources.EditIconGlyph}"
                && label.Attribute("FontSize")?.Value == "20"
        );
        Assert.Empty(editIcon.Descendants(ns + "Button"));
        Assert.Contains(
            resources.Descendants("data"),
            element => element.Attribute("name")?.Value == "EditIconGlyph"
        );
        Assert.Contains(
            page.Descendants(ns + "Entry"),
            entry => entry.Attribute("Text")?.Value == "{Binding EditableFirstName, Mode=TwoWay}"
        );
        Assert.Contains(
            page.Descendants(ns + "Entry"),
            entry => entry.Attribute("Text")?.Value == "{Binding EditableLastName, Mode=TwoWay}"
        );
    }

    [Fact]
    public void Team_roster_exposes_resource_backed_number_display_and_inline_editor()
    {
        var page = LoadXaml("Views", "TeamWorkspacePage.xaml");
        var resources = XDocument.Load(
            Path.Combine(
                RepositoryRoot,
                "SoftballScorekeeper",
                "Resources",
                "Strings",
                "AppResources.resx"
            )
        );
        var viewModel = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "SoftballScorekeeper",
                "ViewModels",
                "TeamWorkspaceViewModel.cs"
            )
        );
        var ns = page.Root!.Name.Namespace;
        var rosterTemplates = page.Descendants(ns + "DataTemplate")
            .Where(template =>
                template.Attribute(XName.Get("DataType", XamlNamespace))?.Value
                == "viewModels:TeamRosterItem"
            )
            .ToArray();

        Assert.Equal(2, rosterTemplates.Length);
        Assert.Contains(
            rosterTemplates.SelectMany(template => template.Descendants(ns + "Label")),
            label =>
                label.Attribute("Text")?.Value == "{Binding JerseyNumberDisplay}"
                && label.Attribute("IsVisible")?.Value == "{Binding HasJerseyNumber}"
        );
        Assert.Contains(
            rosterTemplates.SelectMany(template => template.Descendants(ns + "Entry")),
            entry =>
                entry.Attribute("Text")?.Value == "{Binding EditableJerseyNumber, Mode=TwoWay}"
                && entry.Attribute("Keyboard")?.Value == "Numeric"
        );
        foreach (
            var command in new[]
            {
                "BeginJerseyNumberEditCommand",
                "SaveJerseyNumberCommand",
                "CancelJerseyNumberEditCommand",
            }
        )
            Assert.Contains(
                rosterTemplates.SelectMany(template => template.Descendants(ns + "Button")),
                button => button.Attribute("Command")?.Value?.Contains(command) == true
            );

        foreach (
            var resourceName in new[]
            {
                "JerseyNumberLabel",
                "JerseyNumberDisplayFormat",
                "JerseyNumberUpdateFailed",
            }
        )
            Assert.Contains(
                resources.Descendants("data"),
                element => element.Attribute("name")?.Value == resourceName
            );
        Assert.Contains("GetActiveRosterForTeamAsync", viewModel);
        Assert.Contains("UpdateJerseyNumberAsync", viewModel);
    }

    private static XDocument LoadXaml(params string[] pathParts) =>
        XDocument.Load(
            Path.Combine(
                new[] { RepositoryRoot, "SoftballScorekeeper" }.Concat(pathParts).ToArray()
            )
        );

    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2009/xaml";

    private static string RepositoryRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
