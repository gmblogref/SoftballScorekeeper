using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class MultiOutSelectionDialog : ContentPage
{
    private readonly TaskCompletionSource<IReadOnlyList<MultiOutRetirementSelection>?> completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly int requiredCount;
    private readonly List<int> selectedPlayerIds = [];
    private readonly Dictionary<int, Border> participantBorders = [];
    private bool isClosing;

    public MultiOutSelectionDialog(
        IReadOnlyList<RunnerOutcomeViewModel> participants,
        int requiredCount
    )
    {
        InitializeComponent();
        this.requiredCount = requiredCount;
        TitleLabel.Text = string.Format(
            requiredCount == 1
                ? AppResources.MultiOutSelectionSingleTitleFormat
                : AppResources.MultiOutSelectionTitleFormat,
            requiredCount
        );

        foreach (var participant in participants)
        {
            var content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                },
                RowDefinitions =
                {
                    new RowDefinition(GridLength.Auto),
                    new RowDefinition(GridLength.Auto),
                },
            };
            content.Add(
                new Label
                {
                    Text = participant.PlayerName,
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalTextAlignment = TextAlignment.Center,
                }
            );
            var role = new Label
            {
                Text = participant.RoleLabel,
                FontSize = 14,
                HorizontalTextAlignment = TextAlignment.Center,
            };
            content.Add(role, 0, 1);
            var selectionButton = new Button
            {
                BackgroundColor = Colors.Transparent,
                CommandParameter = participant.PlayerId,
                MinimumHeightRequest = 68,
            };
            selectionButton.Clicked += OnParticipantClicked;
            content.Add(selectionButton, 0, 0);
            Grid.SetRowSpan(selectionButton, 2);
            var border = new Border
            {
                Padding = new Thickness(12, 8),
                Stroke =
                    Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Colors.Gray
                        : Colors.DarkGoldenrod,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
                {
                    CornerRadius = 10,
                },
                Content = content,
            };
            participantBorders[participant.PlayerId] = border;
            ParticipantButtons.Add(border);
        }
    }

    public async Task<IReadOnlyList<MultiOutRetirementSelection>?> ShowAsync(
        INavigation navigation)
    {
        await navigation.PushModalAsync(this);
        return await completion.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    private void OnParticipantClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: int playerId })
            return;
        var existingIndex = selectedPlayerIds.IndexOf(playerId);
        if (existingIndex >= 0)
        {
            selectedPlayerIds.RemoveAt(existingIndex);
        }
        else
        {
            if (selectedPlayerIds.Count == requiredCount) return;
            selectedPlayerIds.Add(playerId);
        }
        RefreshSelectionState();
    }

    private void RefreshSelectionState()
    {
        foreach (var (playerId, border) in participantBorders)
        {
            var isSelected = selectedPlayerIds.Contains(playerId);
            border.BackgroundColor = isSelected
                ? Color.FromArgb("#4DFFD54F")
                : Colors.Transparent;
        }
        ConfirmButton.IsEnabled = selectedPlayerIds.Count == requiredCount;
    }

    private async void OnConfirmClicked(object? sender, EventArgs e) =>
        await CloseAsync(selectedPlayerIds.Select(playerId =>
            new MultiOutRetirementSelection(
                playerId, RunnerOutcomeChoice.ForDestination(
                    RunnerDestination.Out))).ToArray());

    private async void OnCancelClicked(object? sender, EventArgs e) => await CloseAsync(null);

    private async Task CloseAsync(
        IReadOnlyList<MultiOutRetirementSelection>? selected)
    {
        if (isClosing)
            return;
        isClosing = true;
        await Navigation.PopModalAsync();
        completion.TrySetResult(selected);
    }

}
