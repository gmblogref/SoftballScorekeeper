namespace SoftballScorekeeper
{
    public partial class AppShell : Shell
    {
        public const string CreateTeamRoute = "create-team";
        public const string EditTeamRoute = "edit-team";
        public const string InactiveTeamsRoute = "inactive-teams";
        public const string CreatePlayerRoute = "create-player";
        public const string PlayerWorkspaceRoute = "player-workspace";
        public const string InactivePlayersRoute = "inactive-players";
        public const string TeamWorkspaceRoute = "team-workspace";
        public const string AddPlayersToTeamRoute = "add-players-to-team";
        public const string AddGameRoute = "add-game";
        public const string EditGameRoute = "edit-game";
        public const string GameWorkspaceRoute = "game-workspace";
        public const string TeamIdQueryParameter = "teamId";
        public const string PlayerIdQueryParameter = "playerId";
        public const string GameIdQueryParameter = "gameId";
        public const string TeamsSectionRoute = "//teams/team-manager";
        public const string PlayersSectionRoute = "//players/player-manager";

        public AppShell(Views.MainPage mainPage)
        {
            InitializeComponent();
            Routing.RegisterRoute(CreateTeamRoute, typeof(Views.CreateTeamPage));
            Routing.RegisterRoute(EditTeamRoute, typeof(Views.EditTeamPage));
            Routing.RegisterRoute(InactiveTeamsRoute, typeof(Views.InactiveTeamsPage));
            Routing.RegisterRoute(CreatePlayerRoute, typeof(Views.CreatePlayerPage));
            Routing.RegisterRoute(PlayerWorkspaceRoute, typeof(Views.PlayerWorkspacePage));
            Routing.RegisterRoute(InactivePlayersRoute, typeof(Views.InactivePlayersPage));
            Routing.RegisterRoute(TeamWorkspaceRoute, typeof(Views.TeamWorkspacePage));
            Routing.RegisterRoute(AddPlayersToTeamRoute, typeof(Views.AddPlayersToTeamPage));
            Routing.RegisterRoute(AddGameRoute, typeof(Views.AddGamePage));
            Routing.RegisterRoute(EditGameRoute, typeof(Views.EditGamePage));
            Routing.RegisterRoute(GameWorkspaceRoute, typeof(Views.GameWorkspacePage));
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
    }
}
