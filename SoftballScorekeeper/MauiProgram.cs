using Microsoft.Extensions.Logging;

using SoftballScorekeeper.Configuration;
using SoftballScorekeeper.Data;
using SoftballScorekeeper.Data.Repositories;
using SoftballScorekeeper.ViewModels;
using SoftballScorekeeper.Views;

namespace SoftballScorekeeper
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            builder.Services.AddSingleton<AppShell>();
            builder.Services.AddSingleton<ApplicationSettings>();
            builder.Services.AddSingleton<AppDatabase>();
            builder.Services.AddSingleton<TeamRepository>();
            builder.Services.AddSingleton<PlayerRepository>();
            builder.Services.AddSingleton<LandingPageViewModel>();
            builder.Services.AddTransient<CreateTeamViewModel>();
            builder.Services.AddTransient<EditTeamViewModel>();
            builder.Services.AddTransient<InactiveTeamsViewModel>();
            builder.Services.AddTransient<PlayerListViewModel>();
            builder.Services.AddTransient<CreatePlayerViewModel>();
            builder.Services.AddTransient<EditPlayerViewModel>();
            builder.Services.AddTransient<InactivePlayersViewModel>();
            builder.Services.AddTransient<CreateTeamPage>();
            builder.Services.AddTransient<EditTeamPage>();
            builder.Services.AddTransient<InactiveTeamsPage>();
            builder.Services.AddTransient<PlayerListPage>();
            builder.Services.AddTransient<CreatePlayerPage>();
            builder.Services.AddTransient<EditPlayerPage>();
            builder.Services.AddTransient<InactivePlayersPage>();
            builder.Services.AddSingleton<MainPage>();

            return builder.Build();
        }
    }
}
