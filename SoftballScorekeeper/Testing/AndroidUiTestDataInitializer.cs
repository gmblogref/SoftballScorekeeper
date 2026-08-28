#if DEBUG
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Testing;

public sealed class AndroidUiTestDataInitializer
{
    internal const string RequestFileName = "android-ui-test-seed";

    private readonly TeamRepository teamRepository;
    private readonly PlayerRepository playerRepository;
    private readonly GameRepository gameRepository;
    private readonly GameLineupRepository lineupRepository;

    public AndroidUiTestDataInitializer(
        TeamRepository teamRepository,
        PlayerRepository playerRepository,
        GameRepository gameRepository,
        GameLineupRepository lineupRepository)
    {
        this.teamRepository = teamRepository;
        this.playerRepository = playerRepository;
        this.gameRepository = gameRepository;
        this.lineupRepository = lineupRepository;
    }

    internal async Task<AndroidUiTestContext?> InitializeIfRequestedAsync()
    {
        var requestPath = Path.Combine(FileSystem.AppDataDirectory, RequestFileName);
        if (!File.Exists(requestPath)) return null;

        File.Delete(requestPath);

        var team = new Team { Name = "Automation Team", IsActive = true };
        EnsureSingleRow(await teamRepository.CreateAsync(team), "team");

        var playerIds = new List<int>();
        foreach (var firstName in new[] { "Alex", "Blake", "Casey" })
        {
            var result = await playerRepository.CreateForTeamAsync(
                new Player
                {
                    FirstName = firstName,
                    LastName = "Automation",
                    IsActive = true,
                },
                team.Id);
            if (!result.IsSuccess)
                throw new InvalidOperationException($"Could not seed UI-test Player {firstName}.");
            playerIds.Add(result.PlayerId);
        }

        var gameResult = await gameRepository.CreateForTeamAsync(
            team.Id,
            new Season { Name = "Automation", Year = 2026 },
            [new Game
            {
                ScheduledDate = new DateTime(2026, 8, 27),
                ScheduledTime = new TimeSpan(18, 30, 0),
                Opponent = "Automation Opponent",
                Field = "Automation Field",
            }]);
        if (!gameResult.IsCompleteSuccess)
            throw new InvalidOperationException("Could not seed the Android UI-test Game.");

        var gameId = gameResult.CreatedGameIds.Single();
        var lineupResult = await lineupRepository.SaveInitialAsync(team.Id, gameId, playerIds);
        if (!lineupResult.IsSuccess)
            throw new InvalidOperationException("Could not seed the Android UI-test lineup.");

        return new AndroidUiTestContext(team.Id, gameId);
    }

    private static void EnsureSingleRow(int affectedRows, string entityName)
    {
        if (affectedRows != 1)
            throw new InvalidOperationException($"Could not seed the Android UI-test {entityName}.");
    }
}

internal sealed record AndroidUiTestContext(int TeamId, int GameId);
#endif
