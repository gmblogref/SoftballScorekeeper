namespace SoftballScorekeeper.Core.Models;

public static class PendingFielderChoiceCompletion
{
    public static bool CanAutoComplete(
        GameScoringPendingPlay play,
        IReadOnlyList<GameScoringPendingRunnerMovement> movements,
        int currentOuts)
    {
        if (play is null || movements is null
            || play.Result != BatterResult.FielderChoice
            || currentOuts is < 0 or > 2
            || movements.Any(item => item.RequiresOutcomeConfirmation)
            || !PendingBaseRunnerResolutionAdapter.TryCreateState(
                play, movements, out var state)
            || state is null
            || !BaseRunnerResolver.IsLegalArrangement(state))
            return false;

        var definition = PlateAppearanceResultDefinitions.Get(BatterResult.FielderChoice);
        if (!definition.IsCompatibleWithArrangement(state)) return false;

        var outsAfterPlay = currentOuts + state.Participants.Count(item =>
            item.Destination == RunnerDestination.Out);
        if (outsAfterPlay >= 3) return false;

        return state.Participants.All(item =>
            item.Destination == RunnerDestination.Out);
    }
}
