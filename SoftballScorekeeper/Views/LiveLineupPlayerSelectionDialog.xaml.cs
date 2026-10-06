using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class LiveLineupPlayerSelectionDialog : ContentPage
{
    private readonly TaskCompletionSource<LiveLineupPlayerSelection?> completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly IReadOnlyList<LiveLineupPlayerOption> rosteredPlayers;
    private readonly IReadOnlyList<LiveLineupPlayerOption> allPlayers;
    private bool isClosing;

    public LiveLineupPlayerSelectionDialog(
        string title,
        IReadOnlyList<LiveLineupPlayerOption> rosteredPlayers,
        IReadOnlyList<LiveLineupPlayerOption> allPlayers,
        bool allowCreate
    )
    {
        InitializeComponent();
        DialogTitle.Text = title;
        this.rosteredPlayers = rosteredPlayers;
        this.allPlayers = allPlayers;
        CreatePlayerButton.IsVisible = allowCreate;
        ShowRosteredPlayers();
    }

    public async Task<LiveLineupPlayerSelection?> ShowAsync(INavigation navigation)
    {
        await navigation.PushModalAsync(this);
        return await completion.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    private void OnRosteredClicked(object sender, EventArgs e) => ShowRosteredPlayers();

    private void OnAllPlayersClicked(object sender, EventArgs e) =>
        ShowPlayers(allPlayers, showsRostered: false);

    private void ShowRosteredPlayers() => ShowPlayers(rosteredPlayers, showsRostered: true);

    private void ShowPlayers(
        IReadOnlyList<LiveLineupPlayerOption> players,
        bool showsRostered
    )
    {
        PlayersCollection.ItemsSource = players;
        PlayersCollection.IsVisible = players.Count > 0;
        EmptyMessage.IsVisible = players.Count == 0;
        SetSelectedState(RosteredFilterButton, showsRostered);
        SetSelectedState(AllPlayersFilterButton, !showsRostered);
    }

    private static void SetSelectedState(Button button, bool isSelected)
    {
        button.ClearValue(BackgroundColorProperty);
        button.ClearValue(Button.TextColorProperty);
        if (!isSelected)
            return;

        button.SetDynamicResource(BackgroundColorProperty, "Primary");
        button.TextColor = Colors.White;
    }

    private async void OnPlayerClicked(object sender, EventArgs e)
    {
        if (sender is Button { BindingContext: LiveLineupPlayerOption player })
            await CloseAsync(new LiveLineupPlayerSelection(player, false));
    }

    private async void OnCreatePlayerClicked(object sender, EventArgs e) =>
        await CloseAsync(new LiveLineupPlayerSelection(null, true));

    private async void OnCancelClicked(object sender, EventArgs e) => await CloseAsync(null);

    private async Task CloseAsync(LiveLineupPlayerSelection? selection)
    {
        if (isClosing)
            return;

        isClosing = true;
        await Navigation.PopModalAsync();
        completion.TrySetResult(selection);
    }
}

public sealed record LiveLineupPlayerSelection(
    LiveLineupPlayerOption? Player,
    bool CreateNewPlayer
);
