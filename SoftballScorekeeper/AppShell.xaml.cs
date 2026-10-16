namespace SoftballScorekeeper
{
    public partial class AppShell : Shell
    {
        private readonly Infrastructure.Data.DatabaseModeService databaseModeService;
        private readonly Infrastructure.Data.DatabaseModeState databaseModeState;
        private readonly Views.MainPage mainPage;

        public const string CreateTeamRoute = "create-team";
        public const string EditTeamRoute = "edit-team";
        public const string InactiveTeamsRoute = "inactive-teams";
        public const string CreatePlayerRoute = "create-player";
        public const string PlayerWorkspaceRoute = "player-workspace";
        public const string InactivePlayersRoute = "inactive-players";
        public const string TeamWorkspaceRoute = "team-workspace";
        public const string TeamBeerTotalsRoute = "team-beer-totals";
        public const string TeamStatsRoute = "team-stats";
        public const string TeamYearSummaryRoute = "team-year-summary";
        public const string AddPlayersToTeamRoute = "add-players-to-team";
        public const string AddGameRoute = "add-game";
        public const string EditGameRoute = "edit-game";
        public const string GameWorkspaceRoute = "game-workspace";
        public const string SetLineupRoute = "set-lineup";
        public const string ScoringRoute = "scoring";
        public const string DataManagementRoute = "data-management";
        public const string LicensesRoute = "licenses";
        public const string TeamIdQueryParameter = "teamId";
        public const string PlayerIdQueryParameter = "playerId";
        public const string GameIdQueryParameter = "gameId";
        public const string LiveLineupPlayerIdQueryParameter = "liveLineupPlayerId";
        public const string TeamsSectionRoute = "//teams/team-manager";
        public const string PlayersSectionRoute = "//players/player-manager";

        public AppShell(
            Views.MainPage mainPage,
            Infrastructure.Data.DatabaseModeState databaseModeState,
            Infrastructure.Data.DatabaseModeService databaseModeService
        )
        {
            InitializeComponent();
            BindingContext = databaseModeState;
            this.databaseModeState = databaseModeState;
            this.mainPage = mainPage;
            this.databaseModeService = databaseModeService;
            UpdateModeMenuVisibility();
            Routing.RegisterRoute(CreateTeamRoute, typeof(Views.CreateTeamPage));
            Routing.RegisterRoute(EditTeamRoute, typeof(Views.EditTeamPage));
            Routing.RegisterRoute(InactiveTeamsRoute, typeof(Views.InactiveTeamsPage));
            Routing.RegisterRoute(CreatePlayerRoute, typeof(Views.CreatePlayerPage));
            Routing.RegisterRoute(PlayerWorkspaceRoute, typeof(Views.PlayerWorkspacePage));
            Routing.RegisterRoute(InactivePlayersRoute, typeof(Views.InactivePlayersPage));
            Routing.RegisterRoute(TeamWorkspaceRoute, typeof(Views.TeamWorkspacePage));
            Routing.RegisterRoute(TeamBeerTotalsRoute, typeof(Views.TeamBeerTotalsPage));
            Routing.RegisterRoute(TeamStatsRoute, typeof(Views.TeamStatsPage));
            Routing.RegisterRoute(TeamYearSummaryRoute, typeof(Views.TeamYearSummaryPage));
            Routing.RegisterRoute(AddPlayersToTeamRoute, typeof(Views.AddPlayersToTeamPage));
            Routing.RegisterRoute(AddGameRoute, typeof(Views.AddGamePage));
            Routing.RegisterRoute(EditGameRoute, typeof(Views.EditGamePage));
            Routing.RegisterRoute(GameWorkspaceRoute, typeof(Views.GameWorkspacePage));
            Routing.RegisterRoute(SetLineupRoute, typeof(Views.SetLineupPage));
            Routing.RegisterRoute(ScoringRoute, typeof(Views.ScoringPage));
            Routing.RegisterRoute(DataManagementRoute, typeof(Views.DataManagementPage));
            Routing.RegisterRoute(LicensesRoute, typeof(Views.LicensesPage));
            Shell.SetFlyoutItemIsVisible(
                LicensesMenuItem,
                DeviceInfo.Platform == DevicePlatform.Android
            );
            MainShellContent.Content = mainPage;
        }

        private async void OnTeamsClicked(object sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await GoToAsync(TeamsSectionRoute);
        }

        private async void OnPlayersClicked(object sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await GoToAsync(PlayersSectionRoute);
        }

        private async void OnManageInactiveTeamsClicked(object sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await GoToAsync(InactiveTeamsRoute);
        }

        private async void OnManageInactivePlayersClicked(object sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await GoToAsync(InactivePlayersRoute);
        }

        private async void OnDataManagementClicked(object sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await GoToAsync(DataManagementRoute);
        }

        private async void OnLicensesClicked(object sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            await GoToAsync(LicensesRoute);
        }

        private async void OnEnterTestModeClicked(object sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            var result = await databaseModeService.EnterTestModeAsync(() =>
                DisplayAlert(
                    global::SoftballScorekeeper
                        .Resources
                        .Strings
                        .AppResources
                        .EnterTestModeConfirmationTitle,
                    global::SoftballScorekeeper
                        .Resources
                        .Strings
                        .AppResources
                        .EnterTestModeConfirmationMessage,
                    global::SoftballScorekeeper
                        .Resources
                        .Strings
                        .AppResources
                        .EnterTestModeConfirmButton,
                    global::SoftballScorekeeper.Resources.Strings.AppResources.CancelButton
                )
            );
            await CompleteModeChangeAsync(result);
        }

        private async void OnExitTestModeClicked(object sender, EventArgs e)
        {
            FlyoutIsPresented = false;
            var result = await databaseModeService.ExitTestModeAsync(async () =>
            {
                var selection = await DisplayActionSheet(
                    global::SoftballScorekeeper
                        .Resources
                        .Strings
                        .AppResources
                        .ExitTestModeChoiceTitle,
                    global::SoftballScorekeeper.Resources.Strings.AppResources.CancelButton,
                    null,
                    global::SoftballScorekeeper.Resources.Strings.AppResources.DeleteTestDataChoice,
                    global::SoftballScorekeeper.Resources.Strings.AppResources.KeepTestDataChoice
                );
                if (
                    selection
                    == global::SoftballScorekeeper
                        .Resources
                        .Strings
                        .AppResources
                        .DeleteTestDataChoice
                )
                    return Infrastructure.Data.TestModeExitChoice.DeleteTestData;
                if (
                    selection
                    == global::SoftballScorekeeper.Resources.Strings.AppResources.KeepTestDataChoice
                )
                    return Infrastructure.Data.TestModeExitChoice.KeepTestData;
                return Infrastructure.Data.TestModeExitChoice.Cancel;
            });
            await CompleteModeChangeAsync(result);
        }

        private async Task CompleteModeChangeAsync(
            Infrastructure.Data.DatabaseModeChangeResult result
        )
        {
            if (result == Infrastructure.Data.DatabaseModeChangeResult.Changed)
            {
                UpdateModeMenuVisibility();
                await mainPage.ReloadAsync();
                await GoToAsync(TeamsSectionRoute);
                return;
            }

            if (result == Infrastructure.Data.DatabaseModeChangeResult.RecoveryFailed)
                await DisplayAlert(
                    global::SoftballScorekeeper
                        .Resources
                        .Strings
                        .AppResources
                        .TestModeRecoveryFailedTitle,
                    global::SoftballScorekeeper
                        .Resources
                        .Strings
                        .AppResources
                        .TestModeRecoveryFailedMessage,
                    global::SoftballScorekeeper.Resources.Strings.AppResources.OkButton
                );
            else if (result == Infrastructure.Data.DatabaseModeChangeResult.Failed)
                await DisplayAlert(
                    global::SoftballScorekeeper
                        .Resources
                        .Strings
                        .AppResources
                        .TestModeChangeFailedTitle,
                    global::SoftballScorekeeper
                        .Resources
                        .Strings
                        .AppResources
                        .TestModeChangeFailedMessage,
                    global::SoftballScorekeeper.Resources.Strings.AppResources.OkButton
                );
            else if (result == Infrastructure.Data.DatabaseModeChangeResult.AlreadyInProgress)
                await DisplayAlert(
                    global::SoftballScorekeeper
                        .Resources
                        .Strings
                        .AppResources
                        .ApplicationWorkflowBusyTitle,
                    global::SoftballScorekeeper
                        .Resources
                        .Strings
                        .AppResources
                        .ApplicationWorkflowBusyMessage,
                    global::SoftballScorekeeper.Resources.Strings.AppResources.OkButton
                );
        }

        private void UpdateModeMenuVisibility()
        {
            Shell.SetFlyoutItemIsVisible(EnterTestModeMenuItem, databaseModeState.IsProductionMode);
            Shell.SetFlyoutItemIsVisible(ExitTestModeMenuItem, databaseModeState.IsTestMode);
        }
    }
}
