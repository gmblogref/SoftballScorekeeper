namespace SoftballScorekeeper.Core.Models;

public static class PendingBaseRunnerResolutionAdapter
{
    public static bool TryCreateState(
        GameScoringPendingPlay play,
        IReadOnlyList<GameScoringPendingRunnerMovement> movements,
        out BaseRunnerResolutionState? state)
    {
        state = null;
        if (play is null || movements is null || play.BatterPlayerId <= 0
            || !Enum.IsDefined(play.Result))
            return false;

        var participants = new List<BaseRunnerParticipant>(movements.Count + 1);
        foreach (var movement in movements)
        {
            var isBatter = movement.PlayerId == play.BatterPlayerId;
            if (movement.PlayerId <= 0
                || !Enum.IsDefined(movement.StartingLocation)
                || !Enum.IsDefined(movement.Destination)
                || (movement.StartingLocation == RunnerStartingLocation.Home) != isBatter)
                return false;
            participants.Add(new(movement.PlayerId,
                movement.StartingLocation, movement.Destination));
        }

        var batterMovements = participants.Count(item =>
            item.PlayerId == play.BatterPlayerId);
        if (play.Result == BatterResult.Out)
        {
            if (batterMovements == 0)
                participants.Add(new(play.BatterPlayerId,
                    RunnerStartingLocation.Home, RunnerDestination.Out));
            else if (batterMovements != 1
                || participants.Single(item => item.PlayerId == play.BatterPlayerId)
                    .Destination != RunnerDestination.Out)
                return false;
        }
        else if (batterMovements != 1)
        {
            return false;
        }

        try
        {
            state = new BaseRunnerResolutionState(participants);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
