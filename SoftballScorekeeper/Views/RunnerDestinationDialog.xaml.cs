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
        RunnerOutcomeChoice currentOutcome,
        IReadOnlyList<RunnerOutcomeChoice> outcomes,
        bool showsGuidedResolutionContext)
    {
        InitializeComponent();
        PlayerNameLabel.Text = playerName;
        StartingLocationLabel.Text = startingLocationLabel;
        StartingLocationLabel.IsVisible = !string.IsNullOrWhiteSpace(
            startingLocationLabel);
        CurrentOutcomeLabel.Text = string.Format(
            AppResources.CurrentRunnerOutcomeFormat,
            ScoringViewModel.FormatRunnerOutcome(currentOutcome));
        FielderChoiceContextLabel.IsVisible = showsGuidedResolutionContext;
        FielderChoiceContextLabel.Text = showsGuidedResolutionContext
            ? AppResources.ResolveFielderChoiceLabel : string.Empty;
        foreach (var outcome in outcomes)
        {
            var button = new Button
            {
                Text = ScoringViewModel.FormatRunnerOutcome(outcome),
                AutomationId = GetAutomationId(outcome),
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

    private async void OnCancelClicked(object? sender, EventArgs e) =>
        await CloseAsync(null);

    private static string GetAutomationId(RunnerOutcomeChoice outcome)
    {
        if (outcome.IsOutAttemptingAdvance)
            return outcome.AttemptedDestination switch
            {
                RunnerDestination.Second => "RunnerOutAttemptingSecondButton",
                RunnerDestination.Third => "RunnerOutAttemptingThirdButton",
                RunnerDestination.Scores => "RunnerOutAttemptingHomeButton",
                _ => "RunnerOutAttemptingButton",
            };

        return outcome.Destination switch
        {
            RunnerDestination.First => "RunnerDestinationFirstButton",
            RunnerDestination.Second => "RunnerDestinationSecondButton",
            RunnerDestination.Third => "RunnerDestinationThirdButton",
            RunnerDestination.Scores => "RunnerDestinationScoresButton",
            RunnerDestination.Out => "RunnerDestinationOutButton",
            _ => "RunnerDestinationButton",
        };
    }

    private async Task CloseAsync(RunnerOutcomeChoice? outcome)
    {
        if (isClosing) return;
        isClosing = true;
        await Navigation.PopModalAsync();
        completion.TrySetResult(outcome);
    }
}
