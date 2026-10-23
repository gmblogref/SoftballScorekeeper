using SoftballScorekeeper.ViewModels;
using SoftballScorekeeper.Resources.Strings;
using SoftballScorekeeper.Services;

namespace SoftballScorekeeper.Views;

public partial class GameWorkspacePage : ContentPage
{
    private const string PortraitCompletedWorkspaceState = "PortraitCompletedWorkspace";
    private const string LandscapeCompletedWorkspaceState = "LandscapeCompletedWorkspace";
    private readonly GameWorkspaceViewModel viewModel;
    private string? appliedCompletedLayoutState;
    private string? lastCompletedLayoutLog;
    private double allocatedWidth;
    private double allocatedHeight;

    public GameWorkspacePage(GameWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        viewModel.RevertManualOverridesConfirmationRequested = () => DisplayAlertAsync(
            AppResources.ManualOverrideRevertTitle,
            AppResources.ManualOverrideRevertMessage,
            AppResources.ManualOverrideRevertConfirmButton,
            AppResources.CancelButton);
        BindingContext = viewModel;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0)
            return;

        allocatedWidth = width;
        allocatedHeight = height;
        ResponsiveLayoutState.Current.Update(width, height);
        var layoutState = SelectCompletedWorkspaceState(width, height);
        if (layoutState != appliedCompletedLayoutState)
        {
            VisualStateManager.GoToState(RootGrid, layoutState);
            appliedCompletedLayoutState = layoutState;
        }

        QueueCompletedLayoutLog();
    }

    internal static string SelectCompletedWorkspaceState(double width, double height) =>
        ResponsiveLayoutState.Classify(width, height)
            is ResponsiveLayoutProfile.CompactLandscape
                or ResponsiveLayoutProfile.RegularLandscape
            ? LandscapeCompletedWorkspaceState
            : PortraitCompletedWorkspaceState;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadAsync();
        QueueCompletedLayoutLog();
    }

    private async void OnScoreGameClicked(object sender, EventArgs e)
    {
        var dialog = new GameRulesDialog(true,
            Core.Models.GameScoringSession.DefaultHomeRunLimit, false);
        var setup = await dialog.ShowAsync(Navigation);
        if (setup is not null)
            await viewModel.InitializeScoringAsync(setup.TeamSide,
                setup.HomeRunLimit, setup.IsOneUpEnabled);
    }

    private async void OnResetGameClicked(object sender, EventArgs e)
    {
        var firstConfirmation = await DisplayAlertAsync(
            AppResources.ResetGameTitle,
            AppResources.ResetGameFirstConfirmation,
            AppResources.ContinueButton,
            AppResources.CancelButton);
        if (!firstConfirmation) return;
        var finalConfirmation = await DisplayAlertAsync(
            AppResources.ResetGameTitle,
            AppResources.ResetGameFinalConfirmation,
            AppResources.ResetGameButton,
            AppResources.CancelButton);
        if (finalConfirmation)
            await viewModel.ResetScoringAsync();
    }

    private void QueueCompletedLayoutLog()
    {
        if (App.CurrentDatabaseModeState?.IsTestMode == true && viewModel.ShowCompletedGame)
            Dispatcher.Dispatch(LogCompletedLayoutDimensions);
    }

    private void LogCompletedLayoutDimensions()
    {
        var message =
            $"[Story13CompletedLayout] PAGE={allocatedWidth:0.#}x{allocatedHeight:0.#} "
            + $"WORKSPACE={WorkspaceContentGrid.Width:0.#}x{WorkspaceContentGrid.Height:0.#} "
            + $"STATE={appliedCompletedLayoutState}";
        if (message == lastCompletedLayoutLog)
            return;

        lastCompletedLayoutLog = message;
        Console.WriteLine(message);
    }
}
