using SoftballScorekeeper.ViewModels;

namespace SoftballScorekeeper.Views;

public partial class FielderChoiceThirdOutDialog : ContentPage
{
    private readonly TaskCompletionSource<int?> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool isClosing;

    public FielderChoiceThirdOutDialog(
        IReadOnlyList<RunnerOutcomeViewModel> participants)
    {
        InitializeComponent();
        foreach (var participant in participants)
        {
            var content = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition(GridLength.Auto),
                    new RowDefinition(GridLength.Auto),
                },
            };
            content.Add(new Label
            {
                Text = participant.PlayerName,
                FontSize = 22,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
            });
            var role = new Label
            {
                Text = participant.RoleLabel,
                FontSize = 14,
                HorizontalTextAlignment = TextAlignment.Center,
            };
            content.Add(role, 0, 1);
            var selectionButton = new Button
            {
                AutomationId = participant.RetirementAutomationId,
                BackgroundColor = Colors.Transparent,
                CommandParameter = participant.PlayerId,
                MinimumHeightRequest = 68,
            };
            selectionButton.Clicked += OnParticipantClicked;
            content.Add(selectionButton);
            ParticipantButtons.Add(new Border
            {
                Padding = new Thickness(12, 8),
                Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Colors.Gray : Colors.DarkGoldenrod,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
                {
                    CornerRadius = 10,
                },
                BackgroundColor = Color.FromArgb("#26FFD54F"),
                Content = content,
            });
        }
    }

    public async Task<int?> ShowAsync(INavigation navigation)
    {
        await navigation.PushModalAsync(this);
        return await completion.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    private async void OnParticipantClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: int playerId })
            await CloseAsync(playerId);
    }

    private async Task CloseAsync(int? playerId)
    {
        if (isClosing) return;
        isClosing = true;
        await Navigation.PopModalAsync();
        completion.TrySetResult(playerId);
    }
}
