namespace SoftballScorekeeper.Views.Controls;

public partial class PrimarySectionNavigation : ContentView
{
    public static readonly BindableProperty CurrentSectionProperty = BindableProperty.Create(
        nameof(CurrentSection),
        typeof(PrimarySection),
        typeof(PrimarySectionNavigation),
        PrimarySection.Teams,
        propertyChanged: OnCurrentSectionChanged);

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

    private static void OnCurrentSectionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((PrimarySectionNavigation)bindable).UpdateCurrentSection();
    }

    private void UpdateCurrentSection()
    {
        TeamsButton.IsEnabled = CurrentSection != PrimarySection.Teams;
        PlayersButton.IsEnabled = CurrentSection != PrimarySection.Players;
    }

    private async void OnTeamsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppShell.TeamsSectionRoute);
    }

    private async void OnPlayersClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AppShell.PlayersSectionRoute);
    }
}

public enum PrimarySection
{
    Teams,
    Players
}
