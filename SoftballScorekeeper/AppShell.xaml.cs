namespace SoftballScorekeeper
{
    public partial class AppShell : Shell
    {
        public const string CreateTeamRoute = "create-team";
        public const string EditTeamRoute = "edit-team";
        public const string InactiveTeamsRoute = "inactive-teams";
        public const string PlayerListRoute = "players";
        public const string CreatePlayerRoute = "create-player";
        public const string EditPlayerRoute = "edit-player";
        public const string InactivePlayersRoute = "inactive-players";
        public const string TeamWorkspaceRoute = "team-workspace";
        public const string TeamIdQueryParameter = "teamId";
        public const string PlayerIdQueryParameter = "playerId";

        public AppShell(Views.MainPage mainPage)
        {
            InitializeComponent();
            Routing.RegisterRoute(CreateTeamRoute, typeof(Views.CreateTeamPage));
            Routing.RegisterRoute(EditTeamRoute, typeof(Views.EditTeamPage));
            Routing.RegisterRoute(InactiveTeamsRoute, typeof(Views.InactiveTeamsPage));
            Routing.RegisterRoute(PlayerListRoute, typeof(Views.PlayerListPage));
            Routing.RegisterRoute(CreatePlayerRoute, typeof(Views.CreatePlayerPage));
            Routing.RegisterRoute(EditPlayerRoute, typeof(Views.EditPlayerPage));
            Routing.RegisterRoute(InactivePlayersRoute, typeof(Views.InactivePlayersPage));
            Routing.RegisterRoute(TeamWorkspaceRoute, typeof(Views.TeamWorkspacePage));
            MainShellContent.Content = mainPage;
        }
    }
}
