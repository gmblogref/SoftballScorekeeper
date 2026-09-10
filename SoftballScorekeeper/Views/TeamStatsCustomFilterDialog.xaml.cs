using System.Collections.ObjectModel;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class TeamStatsCustomFilterDialog : ContentPage
{
    private readonly TaskCompletionSource<IReadOnlyList<int>?> completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ObservableCollection<TeamStatsGameSelectionItem> games;
    private bool isClosing;

    public TeamStatsCustomFilterDialog(
        IEnumerable<TeamCompletedGame> completedGames,
        IEnumerable<int> selectedGameIds)
    {
        InitializeComponent();
        var selected = selectedGameIds.ToHashSet();
        games = new(completedGames.Select(game =>
            new TeamStatsGameSelectionItem(game, selected.Contains(game.GameId))));
        GamesCollection.ItemsSource = games;
    }

    public async Task<IReadOnlyList<int>?> ShowAsync(INavigation navigation)
    {
        await navigation.PushModalAsync(this);
        return await completion.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    private void OnSelectAllClicked(object sender, EventArgs e)
    {
        foreach (var game in games)
            game.IsSelected = true;
    }

    private void OnClearAllClicked(object sender, EventArgs e)
    {
        foreach (var game in games)
            game.IsSelected = false;
    }

    private async void OnApplyClicked(object sender, EventArgs e) =>
        await CloseAsync(games.Where(item => item.IsSelected).Select(item => item.GameId).ToArray());

    private async void OnCancelClicked(object sender, EventArgs e) => await CloseAsync(null);

    private async Task CloseAsync(IReadOnlyList<int>? selectedIds)
    {
        if (isClosing)
            return;
        isClosing = true;
        await Navigation.PopModalAsync();
        completion.TrySetResult(selectedIds);
    }
}
