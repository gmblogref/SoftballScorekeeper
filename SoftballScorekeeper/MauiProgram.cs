using Microsoft.Extensions.Logging;

using SoftballScorekeeper.Configuration;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.ViewModels;
using SoftballScorekeeper.Views;
#if DEBUG
using SoftballScorekeeper.Testing;
#endif

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
                var useAndroidUiTestDatabase = false;
#if DEBUG && ANDROID
                useAndroidUiTestDatabase = AndroidUiTestDataInitializer.IsInitializationRequested(
                    FileSystem.AppDataDirectory);
#endif
                var normalDatabasePath = AppDatabaseFileIsolation.ResolveDatabasePath(
                    FileSystem.AppDataDirectory,
                    settings.DatabaseFileName,
                    settings.AndroidUiTestDatabaseFileName,
                    useAndroidUiTestDatabase: false);
                var databasePath = AppDatabaseFileIsolation.ResolveDatabasePath(
                    FileSystem.AppDataDirectory,
                    settings.DatabaseFileName,
                    settings.AndroidUiTestDatabaseFileName,
                    useAndroidUiTestDatabase);
                if (useAndroidUiTestDatabase)
                    AppDatabaseFileIsolation.ResetAndroidUiTestDatabase(
                        normalDatabasePath, databasePath);
                return new AppDatabase(databasePath);
            });
            builder.Services.AddSingleton<TeamRepository>();
            builder.Services.AddSingleton<PlayerRepository>();
            builder.Services.AddSingleton<TeamPlayerRepository>();
            builder.Services.AddSingleton<SeasonRepository>();
            builder.Services.AddSingleton<GameRepository>();
            builder.Services.AddSingleton<GameLineupRepository>();
            builder.Services.AddSingleton<Core.Models.PlayResolutionCoordinator>();
            builder.Services.AddSingleton<GameScoringSessionRepository>();
            builder.Services.AddSingleton<BeerRuleConfigurationRepository>();
            builder.Services.AddSingleton<BeerAccountingRepository>();
            builder.Services.AddSingleton<TeamRecordRepository>();
#if DEBUG
            builder.Services.AddSingleton<AndroidUiTestDataInitializer>();
#endif
            builder.Services.AddSingleton<LandingPageViewModel>();
            builder.Services.AddTransient<CreateTeamViewModel>();
            builder.Services.AddTransient<EditTeamViewModel>();
            builder.Services.AddTransient<InactiveTeamsViewModel>();
            builder.Services.AddTransient<PlayerListViewModel>();
            builder.Services.AddTransient<CreatePlayerViewModel>();
            builder.Services.AddTransient<PlayerWorkspaceViewModel>();
            builder.Services.AddTransient<InactivePlayersViewModel>();
            builder.Services.AddTransient<TeamWorkspaceViewModel>();
            builder.Services.AddTransient<TeamBeerTotalsViewModel>();
            builder.Services.AddTransient<AddPlayersToTeamViewModel>();
            builder.Services.AddTransient<AddGameViewModel>();
            builder.Services.AddTransient<EditGameViewModel>();
            builder.Services.AddTransient<GameWorkspaceViewModel>();
            builder.Services.AddTransient<SetLineupViewModel>();
            builder.Services.AddTransient<ScoringViewModel>();
            builder.Services.AddTransient<CreateTeamPage>();
            builder.Services.AddTransient<EditTeamPage>();
            builder.Services.AddTransient<InactiveTeamsPage>();
            builder.Services.AddTransient<PlayerListPage>();
            builder.Services.AddTransient<CreatePlayerPage>();
            builder.Services.AddTransient<PlayerWorkspacePage>();
            builder.Services.AddTransient<InactivePlayersPage>();
            builder.Services.AddTransient<TeamWorkspacePage>();
            builder.Services.AddTransient<TeamBeerTotalsPage>();
            builder.Services.AddTransient<AddPlayersToTeamPage>();
            builder.Services.AddTransient<AddGamePage>();
            builder.Services.AddTransient<EditGamePage>();
            builder.Services.AddTransient<GameWorkspacePage>();
            builder.Services.AddTransient<SetLineupPage>();
            builder.Services.AddTransient<ScoringPage>();
            builder.Services.AddSingleton<MainPage>();

            return builder.Build();
        }
    }
}
