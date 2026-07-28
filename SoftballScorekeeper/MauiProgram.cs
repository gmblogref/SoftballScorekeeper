using Microsoft.Extensions.Logging;

using SoftballScorekeeper.Configuration;
using SoftballScorekeeper.Data;
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
            builder.Services.AddSingleton<MainPageViewModel>();
            builder.Services.AddSingleton<MainPage>();

            return builder.Build();
        }
    }
}
