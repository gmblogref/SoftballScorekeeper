using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class PhysicalRcFollowUpPresentationTests
{
    [Fact]
    public void Completed_game_view_buttons_share_inactive_style_and_accent_only_active_view()
    {
        var page = LoadXaml("GameWorkspacePage.xaml");
        var style = page.Descendants()
            .Single(element =>
                element.Name.LocalName == "Style"
                && element.Attribute(XName.Get("Key", XamlNamespace))?.Value
                    == "CompletedGameViewButton"
            );

        AssertSetter(style, "BackgroundColor", "{StaticResource Black}");
        AssertSetter(style, "BorderColor", "{StaticResource White}");
        AssertSetter(style, "BorderWidth", "1");
        AssertSetter(style, "TextColor", "{StaticResource White}");

        AssertActiveTrigger(
            ElementByAutomationId(page, "GameStatsButton"),
            "ShowingGameStats",
            "GameStatsInactiveAccessibilityText",
            "GameStatsActiveAccessibilityText"
        );
        AssertActiveTrigger(
            ElementByAutomationId(page, "GamePlaysButton"),
            "ShowingGamePlays",
            "GamePlaysInactiveAccessibilityText",
            "GamePlaysActiveAccessibilityText"
        );
    }

    [Fact]
    public void Runner_destination_dialog_tightens_only_compact_landscape_chrome()
    {
        var dialog = LoadXaml("RunnerDestinationDialog.xaml");
        var named = NamedElements(dialog);

        AssertCompactLandscapeSetter(named["DialogOverlay"], "Padding", "12");
        AssertCompactLandscapeSetter(named["DialogCard"], "Padding", "16");
        AssertCompactLandscapeSetter(named["DialogContent"], "Spacing", "8");

        var cancel = ElementByAutomationId(dialog, "RunnerDestinationCancelButton");
        Assert.Equal("56", cancel.Attribute("MinimumHeightRequest")?.Value);
        Assert.Contains(
            "MinimumHeightRequest = 56",
            File.ReadAllText(
                Path.Combine(
                    RepositoryRoot,
                    "SoftballScorekeeper",
                    "Views",
                    "RunnerDestinationDialog.xaml.cs"
                )
            )
        );
    }

    private static void AssertActiveTrigger(
        XElement button,
        string binding,
        string inactiveAccessibilityResource,
        string activeAccessibilityResource
    )
    {
        Assert.Equal("{StaticResource CompletedGameViewButton}", button.Attribute("Style")?.Value);
        Assert.Equal(
            $"{{x:Static strings:AppResources.{inactiveAccessibilityResource}}}",
            button.Attribute("SemanticProperties.Description")?.Value
        );
        var trigger = button
            .Descendants()
            .Single(element => element.Name.LocalName == "DataTrigger");
        Assert.Equal($"{{Binding {binding}}}", trigger.Attribute("Binding")?.Value);
        Assert.Equal("True", trigger.Attribute("Value")?.Value);
        AssertSetter(trigger, "BackgroundColor", "{StaticResource Primary}");
        AssertSetter(trigger, "BorderColor", "{StaticResource Primary}");
        AssertSetter(
            trigger,
            "SemanticProperties.Description",
            $"{{x:Static strings:AppResources.{activeAccessibilityResource}}}"
        );
    }

    private static void AssertCompactLandscapeSetter(
        XElement element,
        string property,
        string value
    )
    {
        var trigger = element
            .Descendants()
            .Single(item =>
                item.Name.LocalName == "DataTrigger"
                && item.Descendants()
                    .Any(setter =>
                        setter.Name.LocalName == "Setter"
                        && setter.Attribute("Property")?.Value == property
                        && setter.Attribute("Value")?.Value == value
                    )
            );
        Assert.Equal(
            "{Binding IsCompactLandscape, Source={x:Static services:ResponsiveLayoutState.Current}, x:DataType=services:ResponsiveLayoutState}",
            trigger.Attribute("Binding")?.Value
        );
        Assert.Equal("True", trigger.Attribute("Value")?.Value);
        AssertSetter(trigger, property, value);
    }

    private static void AssertSetter(XElement parent, string property, string value) =>
        Assert.Contains(
            parent.Descendants(),
            setter =>
                setter.Name.LocalName == "Setter"
                && setter.Attribute("Property")?.Value == property
                && setter.Attribute("Value")?.Value == value
        );

    private static Dictionary<string, XElement> NamedElements(XDocument document) =>
        document
            .Descendants()
            .Where(element => element.Attribute(XName.Get("Name", XamlNamespace)) is not null)
            .ToDictionary(
                element => element.Attribute(XName.Get("Name", XamlNamespace))!.Value,
                element => element
            );

    private static XElement ElementByAutomationId(XDocument document, string automationId) =>
        document
            .Descendants()
            .Single(element => element.Attribute("AutomationId")?.Value == automationId);

    private static XDocument LoadXaml(string fileName) =>
        XDocument.Load(Path.Combine(RepositoryRoot, "SoftballScorekeeper", "Views", fileName));

    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2009/xaml";

    private static string RepositoryRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
