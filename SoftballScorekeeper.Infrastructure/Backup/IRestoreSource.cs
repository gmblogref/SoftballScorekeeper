namespace SoftballScorekeeper.Infrastructure.Backup;

public interface IRestoreSource
{
    Task<RestoreSourceArtifact?> SelectAsync(CancellationToken cancellationToken = default);
}
