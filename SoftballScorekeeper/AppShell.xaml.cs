namespace SoftballScorekeeper
{
    public partial class AppShell : Shell
    {
        public const string CreateTeamRoute = "create-team";
        public const string EditTeamRoute = "edit-team";
        public const string InactiveTeamsRoute = "inactive-teams";
        public const string PlayerListRoute = "players";
        public const string CreatePlayerRoute = "create-player";
        public const string TeamIdQueryParameter = "teamId";

        public AppShell(Views.MainPage mainPage)
        {
            InitializeComponent();
            Routing.RegisterRoute(CreateTeamRoute, typeof(Views.CreateTeamPage));
            Routing.RegisterRoute(EditTeamRoute, typeof(Views.EditTeamPage));
            Routing.RegisterRoute(InactiveTeamsRoute, typeof(Views.InactiveTeamsPage));
            Routing.RegisterRoute(PlayerListRoute, typeof(Views.PlayerListPage));
            Routing.RegisterRoute(CreatePlayerRoute, typeof(Views.CreatePlayerPage));
            MainShellContent.Content = mainPage;
        }
    }
}
