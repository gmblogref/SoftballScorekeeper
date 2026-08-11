namespace SoftballScorekeeper.ViewModels;

public sealed class GameScheduleItem
{
    public required int GameId { get; init; }

    public required DateTime ScheduledDate { get; init; }

    public required string NextDisplay { get; init; }

    public required string DateDisplay { get; init; }

    public required string OpponentDisplay { get; init; }

    public required string TimeDisplay { get; init; }

    public required string FieldDisplay { get; init; }

    public required string SeasonDisplay { get; init; }

    public required bool IsNextPlayingDate { get; init; }
}
