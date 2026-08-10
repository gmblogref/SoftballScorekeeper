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
        public const string TeamIdQueryParameter = "teamId";
        public const string PlayerIdQueryParameter = "playerId";
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
            MainShellContent.Content = mainPage;
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
