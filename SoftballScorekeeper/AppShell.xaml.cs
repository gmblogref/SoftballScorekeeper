namespace SoftballScorekeeper
{
    public partial class AppShell : Shell
    {
        public const string CreateTeamRoute = "create-team";

        public AppShell(Views.MainPage mainPage)
        {
            InitializeComponent();
            Routing.RegisterRoute(CreateTeamRoute, typeof(Views.CreateTeamPage));
            MainShellContent.Content = mainPage;
        }
    }
}
