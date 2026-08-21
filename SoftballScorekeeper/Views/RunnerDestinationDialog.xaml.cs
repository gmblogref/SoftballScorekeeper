using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class RunnerDestinationDialog : ContentPage
{
    private readonly TaskCompletionSource<RunnerOutcomeChoice?> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool isClosing;

    public RunnerDestinationDialog(
        string playerName, IReadOnlyList<RunnerOutcomeChoice> outcomes,
        bool isFielderChoiceResolution)
    {
        InitializeComponent();
        PlayerNameLabel.Text = playerName;
        FielderChoiceContextLabel.IsVisible = isFielderChoiceResolution;
        FielderChoiceContextLabel.Text = isFielderChoiceResolution
            ? AppResources.ResolveFielderChoiceLabel : string.Empty;
        foreach (var outcome in outcomes)
        {
            var button = new Button
            {
                Text = ScoringViewModel.FormatRunnerOutcome(outcome),
                MinimumHeightRequest = 56,
                WidthRequest = outcome.IsOutAttemptingAdvance ? 220 : 170,
                Margin = 5,
                CommandParameter = outcome,
            };
            button.Clicked += OnDestinationClicked;
            DestinationButtons.Add(button);
        }
    }

    public async Task<RunnerOutcomeChoice?> ShowAsync(INavigation navigation)
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
        if (sender is Button { CommandParameter: RunnerOutcomeChoice outcome })
            await CloseAsync(outcome);
    }

    private async Task CloseAsync(RunnerOutcomeChoice? outcome)
    {
        if (isClosing) return;
        isClosing = true;
        await Navigation.PopModalAsync();
        completion.TrySetResult(outcome);
    }
}
