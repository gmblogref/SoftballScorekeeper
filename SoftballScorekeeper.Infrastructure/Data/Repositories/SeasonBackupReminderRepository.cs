using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SoftballScorekeeper.Core.Models;
using SQLite;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed class SeasonBackupReminderRepository(
    AppDatabase database,
    DatabaseModeState modeState,
    TimeProvider timeProvider
)
{
    public async Task<IReadOnlyList<SeasonBackupReminderCandidate>> GetEligibleAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!modeState.IsProductionMode)
            return Array.Empty<SeasonBackupReminderCandidate>();

        await using var operation = await database.BeginOperationAsync(cancellationToken);
        IReadOnlyList<SeasonBackupReminderCandidate> eligible =
            Array.Empty<SeasonBackupReminderCandidate>();
        await operation.Connection.RunInTransactionAsync(transaction =>
        {
            var handledIds = transaction
                .Table<SeasonBackupReminderCheckpoint>()
                .Select(checkpoint => checkpoint.CheckpointId)
                .ToHashSet(StringComparer.Ordinal);
            eligible = FindCompletedCandidates(
                    transaction,
                    timeProvider.GetLocalNow().Date,
                    requirePriorPlayingDate: true
                )
                .Where(candidate => !handledIds.Contains(candidate.CheckpointId))
                .ToArray();
        });
        return eligible;
    }

    public async Task DismissAsync(
        IEnumerable<SeasonBackupReminderCandidate> candidates,
        CancellationToken cancellationToken = default
    )
    {
        if (!modeState.IsProductionMode)
            return;

        await SaveAsync(
            candidates,
            SeasonBackupReminderCheckpointDisposition.Dismissed,
            cancellationToken
        );
    }

    public async Task SatisfyEligibleAsync(CancellationToken cancellationToken = default)
    {
        if (!modeState.IsProductionMode)
            return;

        await using var operation = await database.BeginOperationAsync(cancellationToken);
        await operation.Connection.RunInTransactionAsync(transaction =>
        {
            var candidates = FindCompletedCandidates(
                transaction,
                timeProvider.GetLocalNow().Date,
                requirePriorPlayingDate: true
            );
            InsertHandled(
                transaction,
                candidates,
                SeasonBackupReminderCheckpointDisposition.BackupSatisfied
            );
        });
    }

    private async Task SaveAsync(
        IEnumerable<SeasonBackupReminderCandidate> candidates,
        SeasonBackupReminderCheckpointDisposition disposition,
        CancellationToken cancellationToken
    )
    {
        var representedCandidates = candidates
            .GroupBy(candidate => candidate.CheckpointId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        if (representedCandidates.Length == 0)
            return;

        await using var operation = await database.BeginOperationAsync(cancellationToken);
        await operation.Connection.RunInTransactionAsync(transaction =>
            InsertHandled(transaction, representedCandidates, disposition)
        );
    }

    internal static IReadOnlyList<SeasonBackupReminderCandidate> FindCompletedCandidates(
        SQLiteConnection connection,
        DateTime localDate,
        bool requirePriorPlayingDate
    )
    {
        var teams = connection.Table<Team>().ToDictionary(team => team.Id);
        var sessions = connection
            .Table<GameScoringSession>()
            .ToList()
            .ToDictionary(session => session.GameId);
        var gamesBySeason = connection
            .Table<Game>()
            .ToList()
            .GroupBy(game => game.SeasonId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var candidates = new List<SeasonBackupReminderCandidate>();

        foreach (var season in connection.Table<Season>().OrderBy(season => season.Id))
        {
            if (
                !teams.TryGetValue(season.TeamId, out var team)
                || season.Year != team.Year
                || !gamesBySeason.TryGetValue(season.Id, out var seasonGames)
            )
                continue;

            var ownedGames = seasonGames
                .Where(game => game.TeamId == season.TeamId)
                .OrderBy(game => game.Id)
                .ToArray();
            if (
                ownedGames.Length == 0
                || ownedGames.Length != seasonGames.Length
                || ownedGames.Any(game =>
                    !sessions.TryGetValue(game.Id, out var session)
                    || session.Status != GameScoringStatus.Completed
                )
            )
                continue;

            var latestPlayingDate = ownedGames.Max(game => game.ScheduledDate.Date);
            if (requirePriorPlayingDate && latestPlayingDate >= localDate.Date)
                continue;

            candidates.Add(
                new SeasonBackupReminderCandidate(
                    CreateCheckpointId(season.Id, ownedGames),
                    season.Id,
                    latestPlayingDate
                )
            );
        }

        return candidates;
    }

    internal static void InsertHandled(
        SQLiteConnection connection,
        IEnumerable<SeasonBackupReminderCandidate> candidates,
        SeasonBackupReminderCheckpointDisposition disposition
    )
    {
        foreach (var candidate in candidates)
        {
            connection.Insert(
                new SeasonBackupReminderCheckpoint
                {
                    CheckpointId = candidate.CheckpointId,
                    SeasonId = candidate.SeasonId,
                    Disposition = disposition,
                },
                "OR IGNORE"
            );
        }
    }

    private static string CreateCheckpointId(int seasonId, IReadOnlyList<Game> games)
    {
        var canonical = new StringBuilder()
            .Append(seasonId.ToString(CultureInfo.InvariantCulture));
        foreach (var game in games.OrderBy(game => game.Id))
        {
            canonical
                .Append('|')
                .Append(game.Id.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(game.ScheduledDate.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }
}

public sealed record SeasonBackupReminderCandidate(
    string CheckpointId,
    int SeasonId,
    DateTime LatestPlayingDate
);
