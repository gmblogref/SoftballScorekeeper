using Microsoft.Extensions.Logging;

using SoftballScorekeeper.Configuration;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
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
            builder.Services.AddSingleton(serviceProvider =>
            {
                var settings = serviceProvider.GetRequiredService<ApplicationSettings>();
                var databasePath = Path.Combine(FileSystem.AppDataDirectory, settings.DatabaseFileName);
                return new AppDatabase(databasePath);
            });
            builder.Services.AddSingleton<TeamRepository>();
            builder.Services.AddSingleton<PlayerRepository>();
            builder.Services.AddSingleton<TeamPlayerRepository>();
            builder.Services.AddSingleton<LandingPageViewModel>();
            builder.Services.AddTransient<CreateTeamViewModel>();
            builder.Services.AddTransient<EditTeamViewModel>();
            builder.Services.AddTransient<InactiveTeamsViewModel>();
            builder.Services.AddTransient<PlayerListViewModel>();
            builder.Services.AddTransient<CreatePlayerViewModel>();
            builder.Services.AddTransient<PlayerWorkspaceViewModel>();
            builder.Services.AddTransient<InactivePlayersViewModel>();
            builder.Services.AddTransient<TeamWorkspaceViewModel>();
            builder.Services.AddTransient<AddPlayersToTeamViewModel>();
            builder.Services.AddTransient<CreateTeamPage>();
            builder.Services.AddTransient<EditTeamPage>();
            builder.Services.AddTransient<InactiveTeamsPage>();
            builder.Services.AddTransient<PlayerListPage>();
            builder.Services.AddTransient<CreatePlayerPage>();
            builder.Services.AddTransient<PlayerWorkspacePage>();
            builder.Services.AddTransient<InactivePlayersPage>();
            builder.Services.AddTransient<TeamWorkspacePage>();
            builder.Services.AddTransient<AddPlayersToTeamPage>();
            builder.Services.AddSingleton<MainPage>();

            return builder.Build();
        }
    }
}
