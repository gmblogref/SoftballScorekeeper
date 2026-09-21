using SoftballScorekeeper.Infrastructure.Data;

namespace SoftballScorekeeper.Services;

public sealed class MauiDatabaseModePreferenceStore : IDatabaseModePreferenceStore
{
    private const string PreferenceKey = "database-mode";

    public DatabaseMode Load()
    {
        var value = Preferences.Default.Get(PreferenceKey, nameof(DatabaseMode.Production));
        return
            Enum.TryParse<DatabaseMode>(value, ignoreCase: false, out var mode)
            && Enum.IsDefined(mode)
            ? mode
            : DatabaseMode.Production;
    }

    public void Save(DatabaseMode mode) => Preferences.Default.Set(PreferenceKey, mode.ToString());
}
