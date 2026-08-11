namespace SoftballScorekeeper.Views.Controls;

public partial class PrimarySectionNavigation : ContentView
{
    public static readonly BindableProperty CurrentSectionProperty = BindableProperty.Create(
        nameof(CurrentSection),
        typeof(PrimarySection),
        typeof(PrimarySectionNavigation),
        PrimarySection.Teams,
        propertyChanged: OnCurrentSectionChanged);

    public static readonly BindableProperty IsCurrentSectionRootProperty = BindableProperty.Create(
        nameof(IsCurrentSectionRoot),
        typeof(bool),
        typeof(PrimarySectionNavigation),
        false);

    public PrimarySectionNavigation()
    {
        InitializeComponent();
        UpdateCurrentSection();
    }

    public PrimarySection CurrentSection
    {
        get => (PrimarySection)GetValue(CurrentSectionProperty);
        set => SetValue(CurrentSectionProperty, value);
    }

    public bool IsCurrentSectionRoot
    {
        get => (bool)GetValue(IsCurrentSectionRootProperty);
        set => SetValue(IsCurrentSectionRootProperty, value);
    }

    private static void OnCurrentSectionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((PrimarySectionNavigation)bindable).UpdateCurrentSection();
    }

    private void UpdateCurrentSection()
    {
        TeamsButton.IsEnabled = true;
        PlayersButton.IsEnabled = true;
    }

    private async void OnTeamsClicked(object sender, EventArgs e)
    {
        if (CurrentSection == PrimarySection.Teams && IsCurrentSectionRoot)
        {
            return;
        }

        await Shell.Current.GoToAsync(AppShell.TeamsSectionRoute);
    }

    private async void OnPlayersClicked(object sender, EventArgs e)
    {
        if (CurrentSection == PrimarySection.Players && IsCurrentSectionRoot)
        {
            return;
        }

        await Shell.Current.GoToAsync(AppShell.PlayersSectionRoute);
    }
}

public enum PrimarySection
{
    Teams,
    Players
}
