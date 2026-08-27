using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Infrastructure.Data.Repositories;

public static class PlayContextFactory
{
    public static bool TryCreateForNewResult(
        int currentOuts,
        int batterPlayerId,
        BatterResult result,
        IReadOnlyList<GameScoringBaseRunner> committedBases,
        out PlayContext? context)
    {
        context = null;
        if (currentOuts is < 0 or > 2
            || batterPlayerId <= 0
            || committedBases is null
            || !PlateAppearanceResultDefinitions.TryGet(result, out var definition)
            || definition is null
            || !HasValidCommittedBases(committedBases))
            return false;

        var occupiedStartingLocations = committedBases
            .Select(item => (RunnerStartingLocation)item.Base)
            .ToHashSet();
        var participants = committedBases.Select(item =>
            new ResolvedPlayParticipantOutcome(
                item.PlayerId,
                (RunnerStartingLocation)item.Base,
                definition.GetExistingRunnerDefaultDestination(
                    (RunnerStartingLocation)item.Base, occupiedStartingLocations),
                null,
                null,
                null,
                RequiresOutcomeConfirmation: definition.RequiresOutcomeConfirmation(
                    (RunnerStartingLocation)item.Base))).ToList();
        participants.Add(new(
            batterPlayerId,
            RunnerStartingLocation.Home,
            definition.IncludesBatterMovement
                ? definition.BatterDefaultDestination
                : RunnerDestination.Out,
            null,
            null,
            definition.UsesOrderedOuts
                && definition.BatterDefaultDestination == RunnerDestination.Out
                    ? 1
                    : null,
            RequiresOutcomeConfirmation: definition.RequiresOutcomeConfirmation(
                RunnerStartingLocation.Home)));

        return TryConstruct(
            batterPlayerId, result, currentOuts, participants, out context);
    }

    public static bool TryRehydrate(
        int currentOuts,
        GameScoringPendingPlay pendingPlay,
        IReadOnlyList<GameScoringPendingRunnerMovement> pendingMovements,
        IReadOnlyList<GameScoringBaseRunner> committedBases,
        out PlayContext? context)
    {
        context = null;
        if (currentOuts is < 0 or > 2
            || pendingPlay is null
            || pendingMovements is null
            || committedBases is null
            || pendingPlay.BatterPlayerId <= 0
            || !PlateAppearanceResultDefinitions.TryGet(
                pendingPlay.Result, out var definition)
            || definition is null
            || !HasValidCommittedBases(committedBases)
            || !HasExpectedPrePlayParticipants(
                pendingPlay, pendingMovements, committedBases, definition)
            || !PendingBaseRunnerResolutionAdapter.TryCreateState(
                pendingPlay, pendingMovements, out var resolverState)
            || resolverState is null)
            return false;

        var movementsByPlayerId = pendingMovements.ToDictionary(item => item.PlayerId);
        var participants = new List<ResolvedPlayParticipantOutcome>(
            resolverState.Participants.Count);
        foreach (var participant in resolverState.Participants)
        {
            if (!movementsByPlayerId.TryGetValue(
                    participant.PlayerId, out var movement))
            {
                if (participant.PlayerId != pendingPlay.BatterPlayerId
                    || participant.StartingLocation != RunnerStartingLocation.Home
                    || pendingPlay.Result != BatterResult.Out)
                    return false;
                participants.Add(new(
                    participant.PlayerId,
                    participant.StartingLocation,
                    participant.Destination,
                    null,
                    null,
                    null));
                continue;
            }

            participants.Add(new(
                movement.PlayerId,
                movement.StartingLocation,
                movement.Destination,
                movement.SafeDestination,
                movement.AttemptedDestination,
                movement.OutOrder,
                definition.RequiredPlayOuts > 0
                    && movement.OutOrder is not null
                    && movement.OutOrder <= definition.RequiredPlayOuts,
                movement.RequiresOutcomeConfirmation));
        }

        if (!HasValidOutOrderState(participants, definition)) return false;
        return TryConstruct(
            pendingPlay.BatterPlayerId,
            pendingPlay.Result,
            currentOuts,
            participants,
            out context);
    }

    public static bool TryApplyToPendingMovements(
        PlayContext context,
        IReadOnlyList<GameScoringPendingRunnerMovement> pendingMovements)
    {
        if (context is null || pendingMovements is null) return false;
        foreach (var movement in pendingMovements)
        {
            var participant = context.Participants.FirstOrDefault(item =>
                item.PlayerId == movement.PlayerId);
            if (participant is null
                || participant.StartingLocation != movement.StartingLocation)
                return false;
            movement.Destination = participant.Destination;
            movement.SafeDestination = participant.SafeDestination;
            movement.AttemptedDestination = participant.AttemptedDestination;
            movement.OutOrder = participant.OutOrder;
            movement.RequiresOutcomeConfirmation =
                participant.RequiresOutcomeConfirmation;
        }
        return true;
    }

    private static bool HasExpectedPrePlayParticipants(
        GameScoringPendingPlay pendingPlay,
        IReadOnlyList<GameScoringPendingRunnerMovement> movements,
        IReadOnlyList<GameScoringBaseRunner> bases,
        PlateAppearanceResultDefinition definition)
    {
        var expectedStarts = bases.Select(item =>
            (item.PlayerId, (RunnerStartingLocation)item.Base)).ToHashSet();
        if (definition.IncludesBatterMovement)
            expectedStarts.Add((pendingPlay.BatterPlayerId, RunnerStartingLocation.Home));
        return movements.Count == expectedStarts.Count
            && movements.Select(item => (item.PlayerId, item.StartingLocation))
                .ToHashSet().SetEquals(expectedStarts)
            && movements.All(item => !item.RequiresOutcomeConfirmation
                || item.SafeDestination is null && item.AttemptedDestination is null);
    }

    private static bool HasValidOutOrderState(
        IReadOnlyList<ResolvedPlayParticipantOutcome> participants,
        PlateAppearanceResultDefinition definition)
    {
        if (!definition.UsesOrderedOuts)
            return participants.All(item => item.OutOrder is null);
        var ordered = participants
            .Where(item => item.OutOrder is not null)
            .OrderBy(item => item.OutOrder)
            .ToArray();
        return participants.All(item =>
                item.Destination == RunnerDestination.Out
                    ? item.OutOrder is not null
                    : item.OutOrder is null)
            && ordered.All(item => item.Destination == RunnerDestination.Out)
            && ordered.Select(item => item.OutOrder)
                .SequenceEqual(Enumerable.Range(1, ordered.Length)
                    .Select(value => (int?)value));
    }

    private static bool HasValidCommittedBases(
        IReadOnlyList<GameScoringBaseRunner> bases) =>
        bases.All(item => item.PlayerId > 0 && Enum.IsDefined(item.Base))
        && bases.Select(item => item.PlayerId).Distinct().Count() == bases.Count
        && bases.Select(item => item.Base).Distinct().Count() == bases.Count;

    private static bool TryConstruct(
        int batterPlayerId,
        BatterResult result,
        int currentOuts,
        IEnumerable<ResolvedPlayParticipantOutcome> participants,
        out PlayContext? context)
    {
        try
        {
            context = new(batterPlayerId, result, currentOuts, participants);
            return true;
        }
        catch (ArgumentException)
        {
            context = null;
            return false;
        }
    }
}
