using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public sealed class BeerLedgerRepository(AppDatabase database)
{
    public async Task<IReadOnlyList<BeerLedgerEntry>> GetForGameAsync(int gameId)
    {
        if (gameId <= 0)
            return [];
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        return await connection
            .Table<BeerLedgerEntry>()
            .Where(item => item.GameId == gameId)
            .OrderBy(item => item.Id)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<BeerLedgerEntry>> GetForTeamAsync(int teamId)
    {
        if (teamId <= 0)
            return [];
        await using var operation = await database.BeginOperationAsync();
        var connection = operation.Connection;
        return await connection
            .Table<BeerLedgerEntry>()
            .Where(item => item.TeamId == teamId)
            .OrderBy(item => item.Id)
            .ToListAsync();
    }
}
