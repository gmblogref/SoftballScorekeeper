using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;

namespace SoftballScorekeeper.Tests;

public sealed class GameScoringSessionRepositoryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task Initialize_creates_zeroed_session_and_ordered_live_sequence_without_changing_frozen_lineup(
        int playerCount)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, playerCount);
        var frozenBefore = (await db.GetGameLineupPlayersAsync()).ToArray();

        var result = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var state = await db.CreateGameScoringSessionRepository().GetStateForGameAsync(context.Game.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.WasCreated);
        Assert.Equal(0, state!.Session.HomeScore);
        Assert.Equal(0, state.Session.VisitorScore);
        Assert.Equal(1, state.Session.CurrentBatterPosition);
        Assert.Equal(
            frozenBefore.Select(item => (state.Session.Id, item.PlayerId, item.BattingOrder)),
            state.BattingSequence.Select(item =>
                (item.GameScoringSessionId, item.PlayerId, item.BattingPosition)));
        Assert.Equal(LineupValues(frozenBefore), LineupValues(await db.GetGameLineupPlayersAsync()));
        Assert.Equal(frozenBefore.Select(item => item.PlayerId).Order(), state.PlayersById.Keys.Order());
    }

    [Fact]
    public async Task Initialize_uses_frozen_historical_players_without_current_eligibility()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var frozen = await db.GetGameLineupPlayersAsync();
        await db.CreatePlayerRepository().DeactivateAsync([frozen[0].PlayerId]);
        await db.CreateTeamPlayerRepository()
            .RemovePlayersFromTeamAsync(context.Team.Id, [frozen[1].PlayerId]);

        var result = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);
        var state = await db.CreateGameScoringSessionRepository().GetStateForGameAsync(context.Game.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(frozen.Select(item => item.PlayerId),
            state!.BattingSequence.Select(item => item.PlayerId));
        Assert.Equal(frozen.Select(item => item.PlayerId).Order(), state.PlayersById.Keys.Order());
    }

    [Fact]
    public async Task Load_and_repeated_initialization_preserve_scores_current_batter_and_sequence()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 3);
        var repository = db.CreateGameScoringSessionRepository();
        var created = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Away);
        var originalSequence = await db.GetGameScoringLineupPlayersAsync();
        await db.UpdateScoringStateAsync(created.Session!.Id, 7, 5, 3);

        var repeated = await repository.InitializeAsync(
            context.Team.Id, context.Game.Id, TeamGameSide.Home);
        var loaded = await repository.GetStateForGameAsync(context.Game.Id);

        Assert.True(repeated.IsSuccess);
        Assert.False(repeated.WasCreated);
        Assert.Equal(created.Session.Id, repeated.Session!.Id);
        Assert.Equal(TeamGameSide.Away, loaded!.Session.TeamSide);
        Assert.Equal(7, loaded.Session.HomeScore);
        Assert.Equal(5, loaded.Session.VisitorScore);
        Assert.Equal(3, loaded.Session.CurrentBatterPosition);
        Assert.Equal(SequenceValues(originalSequence), SequenceValues(loaded.BattingSequence));
        Assert.Equal(SequenceValues(originalSequence),
            SequenceValues(await db.GetGameScoringLineupPlayersAsync()));
    }

    [Fact]
    public async Task Initialization_rolls_back_session_and_sequence_when_sequence_insert_fails()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        await db.ExecuteAsync(
            "CREATE TRIGGER FailLiveSequence BEFORE INSERT ON GameScoringLineupPlayer BEGIN SELECT RAISE(ABORT, 'test failure'); END");

        var result = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.Equal(InitializeScoringSessionFailure.Persistence, result.FailureReason);
        Assert.Empty(await db.GetGameScoringSessionsAsync());
        Assert.Empty(await db.GetGameScoringLineupPlayersAsync());
    }

    [Fact]
    public async Task Story60_database_upgrade_preserves_history_and_backfills_live_state_once()
    {
        await using var db = await TemporaryTestDatabase.CreateUninitializedAsync();
        var legacy = await SeedLegacyStory60DatabaseAsync(db, validLineup: true);

        await db.Database.InitializeAsync();
        await db.Database.InitializeAsync();

        var session = Assert.Single(await db.GetGameScoringSessionsAsync());
        var sequence = await db.GetGameScoringLineupPlayersAsync();
        var lineup = await db.GetGameLineupPlayersAsync();
        Assert.Equal(legacy.SessionId, session.Id);
        Assert.Equal(legacy.GameId, session.GameId);
        Assert.Equal(TeamGameSide.Away, session.TeamSide);
        Assert.Equal(GameScoringStatus.InProgress, session.Status);
        Assert.Equal((0, 0, 1),
            (session.HomeScore, session.VisitorScore, session.CurrentBatterPosition));
        Assert.Equal(legacy.LineupValues, LineupValues(lineup));
        Assert.Equal(
            legacy.LineupValues.Select(item => (legacy.SessionId, item.PlayerId, item.BattingOrder)),
            sequence.Select(item =>
                (item.GameScoringSessionId, item.PlayerId, item.BattingPosition)));
    }

    [Fact]
    public async Task Story60_database_upgrade_rejects_malformed_lineup_without_partial_live_state()
    {
        await using var db = await TemporaryTestDatabase.CreateUninitializedAsync();
        var legacy = await SeedLegacyStory60DatabaseAsync(db, validLineup: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.Database.InitializeAsync());

        var sessions = await db.GetGameScoringSessionsAsync();
        Assert.Equal(legacy.SessionId, Assert.Single(sessions).Id);
        Assert.Equal(0, sessions[0].CurrentBatterPosition);
        Assert.Empty(await db.GetGameScoringLineupPlayersAsync());
        Assert.Equal(legacy.LineupValues, LineupValues(await db.GetGameLineupPlayersAsync()));
    }

    [Fact]
    public async Task Initialize_rejects_game_without_lineup_and_leaves_no_session()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateGameAsync(db, DateTime.Today, "Opponent", "Field");

        var result = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);

        Assert.False(result.IsSuccess);
        Assert.Equal(InitializeScoringSessionFailure.InvalidLineup, result.FailureReason);
        Assert.Empty(await db.GetGameScoringSessionsAsync());
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(0)]
    [InlineData(10)]
    public async Task Initialize_accepts_past_today_and_future_games(int dayOffset)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today.AddDays(dayOffset), "Opponent", "Field", 1);

        var result = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);

        Assert.True(result.IsSuccess);
        Assert.True(result.WasCreated);
    }

    [Theory]
    [InlineData(null, "Field")]
    [InlineData("Opponent", null)]
    public async Task Initialize_does_not_require_opponent_or_field(string? opponent, string? field)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, opponent, field, 1);

        var result = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(12)]
    public async Task Initialize_accepts_any_positive_contiguous_lineup_size(int playerCount)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, playerCount);

        var result = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Initialize_rejects_invalid_team_game_ownership_and_invalid_ids()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var otherTeam = new Team { Name = "Other", IsActive = true };
        await db.CreateTeamRepository().CreateAsync(otherTeam);
        var repository = db.CreateGameScoringSessionRepository();

        Assert.Equal(InitializeScoringSessionFailure.InvalidRequest,
            (await repository.InitializeAsync(0, context.Game.Id, TeamGameSide.Home)).FailureReason);
        Assert.Equal(InitializeScoringSessionFailure.InvalidTeamOrGame,
            (await repository.InitializeAsync(otherTeam.Id, context.Game.Id, TeamGameSide.Home)).FailureReason);
        Assert.Equal(InitializeScoringSessionFailure.InvalidTeamOrGame,
            (await repository.InitializeAsync(context.Team.Id, 999999, TeamGameSide.Home)).FailureReason);
        Assert.Empty(await db.GetGameScoringSessionsAsync());
    }

    [Theory]
    [InlineData(TeamGameSide.Home)]
    [InlineData(TeamGameSide.Away)]
    public async Task Initialize_persists_selected_side_and_in_progress_status(TeamGameSide side)
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);

        var result = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, side);
        var loaded = await db.CreateGameScoringSessionRepository().GetForGameAsync(context.Game.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(side, loaded!.TeamSide);
        Assert.Equal(GameScoringStatus.InProgress, loaded.Status);
    }

    [Fact]
    public async Task Repeated_and_concurrent_initialization_resolves_to_exactly_one_original_session()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        var repository = db.CreateGameScoringSessionRepository();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            repository.InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away)));
        var sessions = await db.GetGameScoringSessionsAsync();

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Single(results, result => result.WasCreated);
        Assert.Single(sessions);
        Assert.All(results, result => Assert.Equal(sessions[0].Id, result.Session!.Id));
        Assert.Equal(TeamGameSide.Away, sessions[0].TeamSide);
        var sequence = await db.GetGameScoringLineupPlayersAsync();
        Assert.Single(sequence);
        Assert.Equal(sessions[0].Id, sequence[0].GameScoringSessionId);
    }

    [Fact]
    public async Task Session_and_frozen_lineup_are_independent_between_games()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var first = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var secondGame = new Game { TeamId = first.Team.Id, SeasonId = first.Game.SeasonId, ScheduledDate = DateTime.Today.AddDays(1), ScheduledTime = TimeSpan.FromHours(18) };
        await db.SeedGameAsync(secondGame);
        var secondPlayer = new Player { FirstName = "Second", LastName = "Game", IsActive = true };
        await db.CreatePlayerRepository().CreateAsync(secondPlayer);
        await db.SeedGameLineupPlayerAsync(new() { GameId = secondGame.Id, PlayerId = secondPlayer.Id, BattingOrder = 1 });
        var before = await db.GetGameLineupPlayersAsync();

        await db.CreateGameScoringSessionRepository().InitializeAsync(first.Team.Id, first.Game.Id, TeamGameSide.Home);

        Assert.Equal(LineupValues(before), LineupValues(await db.GetGameLineupPlayersAsync()));
        Assert.Null(await db.CreateGameScoringSessionRepository().GetForGameAsync(secondGame.Id));
        await db.CreateGameScoringSessionRepository().InitializeAsync(
            first.Team.Id, secondGame.Id, TeamGameSide.Away);
        var states = new[]
        {
            await db.CreateGameScoringSessionRepository().GetStateForGameAsync(first.Game.Id),
            await db.CreateGameScoringSessionRepository().GetStateForGameAsync(secondGame.Id),
        };
        Assert.NotEqual(states[0]!.Session.Id, states[1]!.Session.Id);
        Assert.Equal(2, states[0]!.BattingSequence.Count);
        Assert.Single(states[1]!.BattingSequence);
        Assert.NotEqual(states[0]!.BattingSequence[0].PlayerId,
            states[1]!.BattingSequence[0].PlayerId);
    }

    [Fact]
    public async Task Session_blocks_every_pre_game_lineup_mutation()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 2);
        var lineupRepository = db.CreateGameLineupRepository();
        await db.CreateGameScoringSessionRepository().InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Away);
        var before = await db.GetGameLineupPlayersAsync();
        var extra = await CreateEligiblePlayerAsync(db, context.Team.Id, "Extra");

        var append = await lineupRepository.AppendAsync(context.Team.Id, context.Game.Id, [extra.Id]);
        var remove = await lineupRepository.RemovePlayersAsync(context.Game.Id, [before[0].PlayerId]);
        var reorder = await lineupRepository.ReorderAsync(context.Game.Id, before.Select(row => row.PlayerId).Reverse().ToArray());
        var previous = await lineupRepository.UsePreviousAsync(context.Team.Id, context.Game.Id);
        var created = new Player { FirstName = "New", LastName = "Player" };
        var create = await db.CreatePlayerRepository().CreateForGameLineupAsync(created, context.Team.Id, context.Game.Id);

        Assert.Equal(SaveGameLineupFailure.ScoringStarted, append.FailureReason);
        Assert.Empty(remove.RemovedPlayerIds);
        Assert.Equal(ReorderGameLineupFailure.ScoringStarted, reorder.FailureReason);
        Assert.Equal(UsePreviousLineupFailure.ScoringStarted, previous.FailureReason);
        Assert.Equal(CreatePlayerFailure.ScoringStarted, create.Failure);
        Assert.Equal(LineupValues(before), LineupValues(await db.GetGameLineupPlayersAsync()));
        Assert.DoesNotContain(await db.GetPlayersAsync(), player => player.FirstName == "New");
    }

    [Fact]
    public async Task Session_blocks_game_deletion_even_if_lineup_rows_are_missing()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateReadyGameAsync(db, DateTime.Today, null, null, 1);
        await db.CreateGameScoringSessionRepository().InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);
        await db.DeleteGameLineupAsync(context.Game.Id);

        var result = await db.CreateGameRepository().RemoveGamesAsync(context.Team.Id, [context.Game.Id]);

        Assert.Contains(context.Game.Id, result.BlockedGameIds);
        Assert.Contains(await db.GetGamesAsync(), game => game.Id == context.Game.Id);
    }

    [Fact]
    public async Task Invalid_noncontiguous_lineup_fails_without_partial_session()
    {
        await using var db = await TemporaryTestDatabase.CreateAsync();
        var context = await CreateGameAsync(db, DateTime.Today, null, null);
        var player = new Player { FirstName = "Invalid", LastName = "Order", IsActive = true };
        await db.CreatePlayerRepository().CreateAsync(player);
        await db.SeedGameLineupPlayerAsync(new() { GameId = context.Game.Id, PlayerId = player.Id, BattingOrder = 2 });

        var result = await db.CreateGameScoringSessionRepository()
            .InitializeAsync(context.Team.Id, context.Game.Id, TeamGameSide.Home);

        Assert.Equal(InitializeScoringSessionFailure.InvalidLineup, result.FailureReason);
        Assert.Empty(await db.GetGameScoringSessionsAsync());
    }

    private static async Task<(Team Team, Game Game)> CreateReadyGameAsync(
        TemporaryTestDatabase db, DateTime date, string? opponent, string? field, int playerCount)
    {
        var context = await CreateGameAsync(db, date, opponent, field);
        for (var index = 0; index < playerCount; index++)
        {
            var player = await CreateEligiblePlayerAsync(db, context.Team.Id, $"Player{index}");
            await db.SeedGameLineupPlayerAsync(new() { GameId = context.Game.Id, PlayerId = player.Id, BattingOrder = index + 1 });
        }
        return context;
    }

    private static async Task<(Team Team, Game Game)> CreateGameAsync(
        TemporaryTestDatabase db, DateTime date, string? opponent, string? field)
    {
        var team = new Team { Name = $"Team-{Guid.NewGuid():N}", IsActive = true };
        await db.CreateTeamRepository().CreateAsync(team);
        var season = new Season { TeamId = team.Id, Name = "Season", Year = 2026 };
        await db.SeedSeasonAsync(season);
        var game = new Game { TeamId = team.Id, SeasonId = season.Id, ScheduledDate = date.Date, ScheduledTime = TimeSpan.FromHours(18), Opponent = opponent, Field = field };
        await db.SeedGameAsync(game);
        return (team, game);
    }

    private static async Task<Player> CreateEligiblePlayerAsync(TemporaryTestDatabase db, int teamId, string firstName)
    {
        var player = new Player { FirstName = firstName, LastName = "Player", IsActive = true };
        await db.CreatePlayerRepository().CreateAsync(player);
        await db.SeedTeamPlayerAsync(new() { TeamId = teamId, PlayerId = player.Id, IsActive = true });
        return player;
    }

    private static IEnumerable<(int Id, int GameId, int PlayerId, int BattingOrder)> LineupValues(
        IEnumerable<GameLineupPlayer> rows) =>
        rows.Select(row => (row.Id, row.GameId, row.PlayerId, row.BattingOrder));

    private static IEnumerable<(int Id, int SessionId, int PlayerId, int Position)> SequenceValues(
        IEnumerable<GameScoringLineupPlayer> rows) =>
        rows.Select(row =>
            (row.Id, row.GameScoringSessionId, row.PlayerId, row.BattingPosition));

    private static async Task<LegacyContext> SeedLegacyStory60DatabaseAsync(
        TemporaryTestDatabase db,
        bool validLineup)
    {
        return await db.UseConnectionAsync<LegacyContext>(async connection =>
        {
            await connection.CreateTableAsync<Team>();
            await connection.CreateTableAsync<Player>();
            await connection.CreateTableAsync<TeamPlayer>();
            await connection.CreateTableAsync<Season>();
            await connection.CreateTableAsync<Game>();
            await connection.CreateTableAsync<GameLineupPlayer>();
            await connection.CreateTableAsync<LegacyGameScoringSession>();

            var team = new Team { Name = "Legacy Team", IsActive = true };
            await connection.InsertAsync(team);
            var season = new Season { TeamId = team.Id, Name = "Legacy", Year = 2026 };
            await connection.InsertAsync(season);
            var game = new Game
            {
                TeamId = team.Id,
                SeasonId = season.Id,
                ScheduledDate = DateTime.Today,
                ScheduledTime = TimeSpan.FromHours(18),
            };
            await connection.InsertAsync(game);

            var lineup = new List<GameLineupPlayer>();
            for (var index = 0; index < 2; index++)
            {
                var player = new Player
                {
                    FirstName = $"Legacy{index}",
                    LastName = "Player",
                    IsActive = index != 0,
                };
                await connection.InsertAsync(player);
                var lineupPlayer = new GameLineupPlayer
                {
                    GameId = game.Id,
                    PlayerId = player.Id,
                    BattingOrder = validLineup ? index + 1 : index + 2,
                };
                await connection.InsertAsync(lineupPlayer);
                lineup.Add(lineupPlayer);
            }

            var session = new LegacyGameScoringSession
            {
                GameId = game.Id,
                Status = GameScoringStatus.InProgress,
                TeamSide = TeamGameSide.Away,
            };
            await connection.InsertAsync(session);
            return new LegacyContext(
                session.Id,
                game.Id,
                LineupValues(lineup).ToArray());
        });
    }

    private sealed record LegacyContext(
        int SessionId,
        int GameId,
        IReadOnlyList<(int Id, int GameId, int PlayerId, int BattingOrder)> LineupValues);

    [SQLite.Table("GameScoringSession")]
    private sealed class LegacyGameScoringSession
    {
        [SQLite.PrimaryKey, SQLite.AutoIncrement]
        public int Id { get; set; }

        [SQLite.Indexed("UX_GameScoringSession_Game", 1, Unique = true)]
        public int GameId { get; set; }

        public GameScoringStatus Status { get; set; }

        public TeamGameSide TeamSide { get; set; }
    }
}
