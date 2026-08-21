using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class RunnerDestinationDialog : ContentPage
{
    private readonly TaskCompletionSource<RunnerDestination?> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool isClosing;

    public RunnerDestinationDialog(
        string playerName, IReadOnlyList<RunnerDestination> destinations,
        bool isFielderChoiceResolution)
    {
        InitializeComponent();
        PlayerNameLabel.Text = playerName;
        FielderChoiceContextLabel.IsVisible = isFielderChoiceResolution;
        FielderChoiceContextLabel.Text = isFielderChoiceResolution
            ? AppResources.ResolveFielderChoiceLabel : string.Empty;
        foreach (var destination in destinations)
        {
            var button = new Button
            {
                Text = ScoringViewModel.FormatDestination(destination),
                MinimumHeightRequest = 56,
                WidthRequest = 170,
                Margin = 5,
                CommandParameter = destination,
            };
            button.Clicked += OnDestinationClicked;
            DestinationButtons.Add(button);
        }
    }

    public async Task<RunnerDestination?> ShowAsync(INavigation navigation)
    {
        await navigation.PushModalAsync(this);
        return await completion.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    private async void OnDestinationClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: RunnerDestination destination })
            await CloseAsync(destination);
    }

    private async Task CloseAsync(RunnerDestination? destination)
    {
        if (isClosing) return;
        isClosing = true;
        await Navigation.PopModalAsync();
        completion.TrySetResult(destination);
    }
}
