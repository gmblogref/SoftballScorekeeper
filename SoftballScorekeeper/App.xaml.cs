using Microsoft.Extensions.DependencyInjection;
using SoftballScorekeeper.Data;

namespace SoftballScorekeeper;

public partial class App : Application
{
	private readonly AppShell appShell;
	private readonly AppDatabase appDatabase;

	public App(IServiceProvider serviceProvider, AppDatabase appDatabase)
	{
		InitializeComponent();

		appShell = serviceProvider.GetRequiredService<AppShell>();
		this.appDatabase = appDatabase;
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
	}
}