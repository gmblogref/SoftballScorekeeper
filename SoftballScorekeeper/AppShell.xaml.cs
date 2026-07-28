namespace SoftballScorekeeper
{
    public partial class AppShell : Shell
    {
        public AppShell(Views.MainPage mainPage)
        {
            InitializeComponent();
            MainShellContent.Content = mainPage;
        }
    }
}
