namespace SoftballScorekeeper.Infrastructure.Data;

public interface IDatabaseModePreferenceStore
{
    DatabaseMode Load();

    void Save(DatabaseMode mode);
}
