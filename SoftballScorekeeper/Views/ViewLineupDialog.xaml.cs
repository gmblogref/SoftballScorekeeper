using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class ViewLineupDialog : ContentPage
{
    private bool isClosing;

    public ViewLineupDialog(IReadOnlyList<LiveLineupPlayerOption> players)
    {
        InitializeComponent();
        LineupCollection.ItemsSource = players;
    }

    public Task ShowAsync(INavigation navigation) => navigation.PushModalAsync(this);

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync();
        return true;
    }

    private async void OnCloseClicked(object sender, EventArgs e) => await CloseAsync();

    private async Task CloseAsync()
    {
        if (isClosing)
            return;

        isClosing = true;
        await Navigation.PopModalAsync();
    }
}
