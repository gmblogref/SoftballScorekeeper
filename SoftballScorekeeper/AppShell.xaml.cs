namespace SoftballScorekeeper
{
    public partial class AppShell : Shell
    {
        public const string CreateTeamRoute = "create-team";
        public const string EditTeamRoute = "edit-team";
        public const string TeamIdQueryParameter = "teamId";

        public AppShell(Views.MainPage mainPage)
        {
            InitializeComponent();
            Routing.RegisterRoute(CreateTeamRoute, typeof(Views.CreateTeamPage));
            Routing.RegisterRoute(EditTeamRoute, typeof(Views.EditTeamPage));
            MainShellContent.Content = mainPage;
        }
    }
}
