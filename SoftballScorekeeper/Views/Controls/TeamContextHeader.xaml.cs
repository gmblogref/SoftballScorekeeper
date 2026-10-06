namespace SoftballScorekeeper.Views.Controls;

public partial class TeamContextHeader : ContentView
{
    public static readonly BindableProperty TeamNameProperty = BindableProperty.Create(
        nameof(TeamName),
        typeof(string),
        typeof(TeamContextHeader),
        string.Empty
    );

    public static readonly BindableProperty PageTitleProperty = BindableProperty.Create(
        nameof(PageTitle),
        typeof(string),
        typeof(TeamContextHeader),
        string.Empty
    );

    public static readonly BindableProperty TeamNameAutomationIdProperty = BindableProperty.Create(
        nameof(TeamNameAutomationId),
        typeof(string),
        typeof(TeamContextHeader),
        string.Empty
    );

    public TeamContextHeader() => InitializeComponent();

    public string TeamName
    {
        get => (string)GetValue(TeamNameProperty);
        set => SetValue(TeamNameProperty, value);
    }

    public string PageTitle
    {
        get => (string)GetValue(PageTitleProperty);
        set => SetValue(PageTitleProperty, value);
    }

    public string TeamNameAutomationId
    {
        get => (string)GetValue(TeamNameAutomationIdProperty);
        set => SetValue(TeamNameAutomationIdProperty, value);
    }
}
