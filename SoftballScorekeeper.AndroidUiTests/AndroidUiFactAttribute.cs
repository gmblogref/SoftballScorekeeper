namespace SoftballScorekeeper.AndroidUiTests;

public sealed class AndroidUiFactAttribute : FactAttribute
{
    public const string OptInEnvironmentVariable = "SOFTBALL_RUN_ANDROID_UI_TESTS";

    public AndroidUiFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(OptInEnvironmentVariable),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Android UI acceptance tests are opt-in. Run scripts/run-android-ui-tests.ps1.";
        }
    }
}
