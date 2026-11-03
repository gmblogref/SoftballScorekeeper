using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class ViewLineupDialog : ContentPage
{
    private readonly Func<Task<IReadOnlyList<LiveLineupPlayerOption>?>>? reorderRequested;
    private bool isClosing;

    public ViewLineupDialog(
        IReadOnlyList<LiveLineupPlayerOption> players,
        Func<Task<IReadOnlyList<LiveLineupPlayerOption>?>>? reorderRequested = null
    )
    {
        InitializeComponent();
        this.reorderRequested = reorderRequested;
        LineupCollection.ItemsSource = players;
        ReorderButton.IsVisible = reorderRequested is not null;
    }

    public Task ShowAsync(INavigation navigation) => navigation.PushModalAsync(this);

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync();
        return true;
    }

    private async void OnCloseClicked(object sender, EventArgs e) => await CloseAsync();

    private async void OnReorderClicked(object sender, EventArgs e)
    {
        if (reorderRequested is null)
            return;

        ReorderButton.IsEnabled = false;
        try
        {
            var updatedPlayers = await reorderRequested();
            if (updatedPlayers is not null)
                LineupCollection.ItemsSource = updatedPlayers;
        }
        finally
        {
            ReorderButton.IsEnabled = true;
        }
    }

    private async Task CloseAsync()
    {
        if (isClosing)
            return;

        isClosing = true;
        await Navigation.PopModalAsync();
    }
}
