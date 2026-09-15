using Android.App;
using Android.Content.PM;
using Android.OS;

namespace SoftballScorekeeper
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        public static event EventHandler<ActivityResultEventArgs>? ActivityResultReceived;

        protected override void OnActivityResult(int requestCode, Result resultCode, Android.Content.Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            ActivityResultReceived?.Invoke(
                this,
                new ActivityResultEventArgs(requestCode, resultCode, data)
            );
        }
    }

    public sealed record ActivityResultEventArgs(
        int RequestCode,
        Result ResultCode,
        Android.Content.Intent? Data
    );
}
