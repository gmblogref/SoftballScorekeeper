using Microsoft.Extensions.DependencyInjection;
using SoftballScorekeeper.Infrastructure.Data;
#if DEBUG
using SoftballScorekeeper.Testing;
#endif

namespace SoftballScorekeeper;

public partial class App : Application
{
	private readonly AppShell appShell;
	private readonly AppDatabase appDatabase;
#if DEBUG
	private readonly AndroidUiTestDataInitializer androidUiTestDataInitializer;
#endif

	public App(IServiceProvider serviceProvider, AppDatabase appDatabase
#if DEBUG
		, AndroidUiTestDataInitializer androidUiTestDataInitializer
#endif
		)
	{
		InitializeComponent();

		appShell = serviceProvider.GetRequiredService<AppShell>();
		this.appDatabase = appDatabase;
#if DEBUG
		this.androidUiTestDataInitializer = androidUiTestDataInitializer;
#endif
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(appShell);
		window.Created += OnWindowCreated;

		return window;
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
					});
			}
		}
#endif
	}
}
