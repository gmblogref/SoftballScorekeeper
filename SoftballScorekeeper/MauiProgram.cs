using Microsoft.Extensions.Logging;
using SoftballScorekeeper.Configuration;
using SoftballScorekeeper.Infrastructure.Backup;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Services;
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
            builder.Services.AddSingleton<IDatabaseModePreferenceStore>(_ =>
            {
                var preferenceStore = new MauiDatabaseModePreferenceStore();
#if DEBUG && ANDROID
                if (
                    AndroidUiTestDataInitializer.IsInitializationRequested(
                        FileSystem.AppDataDirectory
                    )
                )
                    preferenceStore.Save(DatabaseMode.Production);
#endif
                return preferenceStore;
            });
            builder.Services.AddSingleton<ApplicationWorkflowCoordinator>();
            builder.Services.AddSingleton(serviceProvider =>
            {
                var preferenceStore =
                    serviceProvider.GetRequiredService<IDatabaseModePreferenceStore>();
                return new DatabaseModeState(preferenceStore.Load());
            });
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<IBackupPlatform, MauiBackupPlatform>();
            builder.Services.AddSingleton(serviceProvider =>
            {
                var settings = serviceProvider.GetRequiredService<ApplicationSettings>();
                var databaseModeState = serviceProvider.GetRequiredService<DatabaseModeState>();
                var useAndroidUiTestDatabase = false;
                var resetAndroidUiTestDatabase = false;
#if DEBUG && ANDROID
                useAndroidUiTestDatabase = AndroidUiTestDataInitializer.IsUiTestModeRequested(
                    FileSystem.AppDataDirectory
                );
                resetAndroidUiTestDatabase =
                    AndroidUiTestDataInitializer.IsInitializationRequested(
                        FileSystem.AppDataDirectory
                    );
#endif
                var paths = new AppDatabasePaths(
                    FileSystem.AppDataDirectory,
                    settings.DatabaseFileName,
                    settings.TestDatabaseFileName,
                    settings.AndroidUiTestDatabaseFileName,
                    useAndroidUiTestDatabase
                );
                if (resetAndroidUiTestDatabase)
                    AppDatabaseFileIsolation.ResetAndroidUiTestDatabase(
                        paths.ProductionDatabasePath,
                        paths.AndroidUiTestDatabasePath
                    );
                return new AppDatabase(paths, databaseModeState.CurrentMode);
            });
            builder.Services.AddSingleton<DatabaseModeService>();
            builder.Services.AddSingleton<TeamRepository>();
            builder.Services.AddSingleton<TeamYearCopyRepository>();
            builder.Services.AddSingleton<PlayerRepository>();
            builder.Services.AddSingleton<TeamPlayerRepository>();
            builder.Services.AddSingleton<SeasonRepository>();
            builder.Services.AddSingleton<GameRepository>();
            builder.Services.AddSingleton<GameLineupRepository>();
            builder.Services.AddSingleton<Core.Models.PlayResolutionCoordinator>();
            builder.Services.AddSingleton<GameScoringSessionRepository>();
            builder.Services.AddSingleton<BeerRuleConfigurationRepository>();
            builder.Services.AddSingleton<BeerAccountingRepository>();
            builder.Services.AddSingleton<LowManCalculationRepository>();
            builder.Services.AddSingleton<TeamRecordRepository>();
            builder.Services.AddSingleton<TeamBattingStatisticsRepository>();
            builder.Services.AddSingleton<TeamYearSummaryRepository>();
            builder.Services.AddSingleton<BackupPackageService>();
            builder.Services.AddSingleton<IBackupDestination, MauiBackupDestination>();
            builder.Services.AddSingleton<ManualBackupService>();
            builder.Services.AddSingleton<IRestoreSource, MauiRestoreSource>();
            builder.Services.AddSingleton<DatabaseRestoreService>();
            builder.Services.AddSingleton<ManualRestoreService>();
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
            builder.Services.AddTransient<TeamStatsViewModel>();
            builder.Services.AddTransient<TeamYearSummaryViewModel>();
            builder.Services.AddTransient<AddPlayersToTeamViewModel>();
            builder.Services.AddTransient<AddGameViewModel>();
            builder.Services.AddTransient<EditGameViewModel>();
            builder.Services.AddTransient<GameWorkspaceViewModel>();
            builder.Services.AddTransient<SetLineupViewModel>();
            builder.Services.AddTransient<ScoringViewModel>();
            builder.Services.AddTransient<DataManagementViewModel>();
            builder.Services.AddTransient<CreateTeamPage>();
            builder.Services.AddTransient<EditTeamPage>();
            builder.Services.AddTransient<InactiveTeamsPage>();
            builder.Services.AddTransient<PlayerListPage>();
            builder.Services.AddTransient<CreatePlayerPage>();
            builder.Services.AddTransient<PlayerWorkspacePage>();
            builder.Services.AddTransient<InactivePlayersPage>();
            builder.Services.AddTransient<TeamWorkspacePage>();
            builder.Services.AddTransient<TeamBeerTotalsPage>();
            builder.Services.AddTransient<TeamStatsPage>();
            builder.Services.AddTransient<TeamYearSummaryPage>();
            builder.Services.AddTransient<AddPlayersToTeamPage>();
            builder.Services.AddTransient<AddGamePage>();
            builder.Services.AddTransient<EditGamePage>();
            builder.Services.AddTransient<GameWorkspacePage>();
            builder.Services.AddTransient<SetLineupPage>();
            builder.Services.AddTransient<ScoringPage>();
            builder.Services.AddTransient<DataManagementPage>();
            builder.Services.AddSingleton<MainPage>();

            return builder.Build();
        }
    }
}
