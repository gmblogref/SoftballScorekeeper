using Microsoft.Extensions.DependencyInjection;
using SoftballScorekeeper.Infrastructure.Data;
using SoftballScorekeeper.Services;
#if DEBUG
using SoftballScorekeeper.Testing;
#endif

namespace SoftballScorekeeper;

public partial class App : Application
{
    public static DatabaseModeState CurrentDatabaseModeState { get; private set; } = null!;

    private readonly AppShell appShell;
    private readonly AppDatabase appDatabase;
#if DEBUG
    private readonly AndroidUiTestDataInitializer androidUiTestDataInitializer;
#endif

    public App(
        IServiceProvider serviceProvider,
        AppDatabase appDatabase,
        DatabaseModeState databaseModeState
#if DEBUG
        ,
        AndroidUiTestDataInitializer androidUiTestDataInitializer
#endif
    )
    {
        CurrentDatabaseModeState = databaseModeState;
        InitializeComponent();
        Resources["DatabaseModeState"] = databaseModeState;

        appShell = serviceProvider.GetRequiredService<AppShell>();
        this.appDatabase = appDatabase;
#if DEBUG
        this.androidUiTestDataInitializer = androidUiTestDataInitializer;
#endif
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(appShell);
        window.SizeChanged += OnWindowSizeChanged;
        window.Created += OnWindowCreated;

        return window;
    }

    private static void OnWindowSizeChanged(object? sender, EventArgs e)
    {
        if (sender is Window window)
            ResponsiveLayoutState.Current.Update(window.Width, window.Height);
    }

    private async void OnWindowCreated(object? sender, EventArgs e)
    {
        await appDatabase.InitializeAsync();
#if DEBUG && ANDROID
        var context = await androidUiTestDataInitializer.InitializeIfRequestedAsync();
        if (context is not null)
        {
            if (context.OpenTeamManager)
            {
                await Shell.Current.GoToAsync(AppShell.PlayersSectionRoute);
                await Shell.Current.GoToAsync(AppShell.TeamsSectionRoute);
            }
            else
            {
                await Shell.Current.GoToAsync(
                    AppShell.GameWorkspaceRoute,
                    new ShellNavigationQueryParameters
                    {
                        [AppShell.TeamIdQueryParameter] = context.TeamId,
                        [AppShell.GameIdQueryParameter] = context.GameId,
                    }
                );
            }
        }
#endif
    }
}
