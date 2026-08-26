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
        string playerName,
        string startingLocationLabel,
        IReadOnlyList<RunnerOutcomeChoice> outcomes,
        bool showsGuidedResolutionContext)
    {
        InitializeComponent();
        PlayerNameLabel.Text = playerName;
        StartingLocationLabel.Text = startingLocationLabel;
        StartingLocationLabel.IsVisible = !string.IsNullOrWhiteSpace(
            startingLocationLabel);
        FielderChoiceContextLabel.IsVisible = showsGuidedResolutionContext;
        FielderChoiceContextLabel.Text = showsGuidedResolutionContext
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
