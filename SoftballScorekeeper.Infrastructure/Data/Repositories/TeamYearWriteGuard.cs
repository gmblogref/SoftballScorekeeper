using SoftballScorekeeper.Core.Models;
using SQLite;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

internal static class TeamYearWriteGuard
{
    public static bool IsOpen(SQLiteConnection connection, int teamId) =>
        connection.Find<Team>(teamId) is { IsYearOver: false };

    public static bool IsActiveAndOpen(SQLiteConnection connection, int teamId) =>
        connection.Find<Team>(teamId) is { IsActive: true, IsYearOver: false };

    public static bool IsGameFinalized(SQLiteConnection connection, int gameId)
    {
        var game = connection.Find<Game>(gameId);
        return game is not null
            && connection.Find<Team>(game.TeamId) is { IsYearOver: true };
    }
}
