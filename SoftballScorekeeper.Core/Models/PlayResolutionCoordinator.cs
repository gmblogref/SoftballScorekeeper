namespace SoftballScorekeeper.Core.Models;

public sealed class PlayResolutionCoordinator
{
    public PlayResolutionResult Evaluate(PlayContext? context)
    {
        if (context is null
            || !PlateAppearanceResultDefinitions.TryGet(
                context.Result, out var definition)
            || definition is null)
            return Rejected(context, PlayResolutionFailure.InvalidContext);

        if (context.Participants.Count < definition.MinimumParticipants
            || definition.RequiresExistingRunner
                && context.OccupiedStartingLocations.Count == 0
            || definition.RequiredPlayOuts > 3 - context.CurrentOuts)
            return Rejected(context, PlayResolutionFailure.ResultUnavailable);

        if (context.Participants.Any(item =>
                !new RunnerOutcomeChoice(item.Destination,
                    item.SafeDestination, item.AttemptedDestination)
                    .IsValidFor(item.StartingLocation)))
            return Rejected(context, PlayResolutionFailure.InvalidContext);

        if (definition.CompletionBehavior == PlateAppearanceCompletionBehavior.Immediate)
            return Accepted(context, PlayResolutionStatus.CommitImmediately);

        if (context.Result == BatterResult.Out)
        {
            var commitsImmediately = context.OccupiedStartingLocations.Count == 0
                || context.CurrentOuts == 2;
            return Accepted(context, commitsImmediately
                ? PlayResolutionStatus.CommitImmediately
                : PlayResolutionStatus.ReadyForNextBatter);
        }

        if (definition.RequiredPlayOuts > 0
            && context.CurrentOuts + definition.RequiredPlayOuts == 3)
        {
            var ruling = ThirdOutRunRulingEvaluator.Evaluate(
                context.CurrentOuts,
                context.BatterPlayerId,
                context.Participants,
                context.Result);
            return ruling == ThirdOutRunRulingRequirement.AutomaticNoRun
                ? Accepted(context, PlayResolutionStatus.CommitImmediately,
                    thirdOutRulingRequirement: ruling)
                : Rejected(context, PlayResolutionFailure.InvalidContext);
        }

        if (definition.RequiredPlayOuts > 0
            && context.Participants.Count == definition.RequiredPlayOuts
            && context.Participants.Any(item => item.RequiresOutcomeConfirmation))
            return ApplyRetirementSelection(
                context, context.Participants.Select(item => item.PlayerId).ToHashSet());

        if (definition.RequiredPlayOuts > 0
            && context.Participants.All(item =>
                item.RequiresOutcomeConfirmation && item.OutOrder is null))
            return Accepted(
                context,
                PlayResolutionStatus.AwaitingDecision,
                PlayDecisionRequirement.RetirementSelection,
                context.Participants.Select(item => item.PlayerId).ToArray(),
                definition.RequiredPlayOuts);

        if (context.Result == BatterResult.FielderChoice
            && context.CurrentOuts == 2)
        {
            var completed = context.Participants.Any(item =>
                    item.RequiresOutcomeConfirmation)
                ? ResolveRemainingFielderChoiceParticipants(context, definition)
                : context;
            if (completed is null
                || !TryCreateResolverState(completed, out var completedState)
                || completedState is null
                || !BaseRunnerResolver.IsLegalArrangement(completedState)
                || !IsCompatibleWithDefinition(
                    completed, definition, completedState)
                || completed.Participants.Count(item =>
                    item.Destination == RunnerDestination.Out) != 1)
                return Rejected(context, PlayResolutionFailure.IllegalArrangement);

            return Accepted(
                completed,
                PlayResolutionStatus.AutoComplete,
                thirdOutRulingRequirement:
                    ThirdOutRunRulingRequirement.AutomaticNoRun);
        }

        if (context.Result == BatterResult.FielderChoice
            && context.Participants.Any(item => item.RequiresOutcomeConfirmation)
            && context.Participants.Count(item =>
                !item.RequiresOutcomeConfirmation
                && item.Destination == RunnerDestination.Out)
                >= definition.MinimumPlayOuts)
        {
            var defaulted = ResolveRemainingFielderChoiceParticipants(
                context, definition);
            return defaulted is null
                ? Rejected(context, PlayResolutionFailure.IllegalArrangement)
                : Evaluate(defaulted);
        }

        var unresolved = context.Participants
            .Where(item => item.RequiresOutcomeConfirmation)
            .Select(item => item.PlayerId)
            .ToArray();
        if (unresolved.Length > 0)
            return Accepted(
                context,
                PlayResolutionStatus.AwaitingDecision,
                PlayDecisionRequirement.ParticipantOutcome,
                unresolved);

        if (!TryCreateResolverState(context, out var resolverState)
            || resolverState is null
            || !BaseRunnerResolver.IsLegalArrangement(resolverState)
            || !IsCompatibleWithDefinition(context, definition, resolverState))
            return Rejected(context, PlayResolutionFailure.IllegalArrangement);

        var outsAfterPlay = context.CurrentOuts + context.Participants.Count(item =>
            item.Destination == RunnerDestination.Out);
        if (outsAfterPlay > 3)
            return Rejected(context, PlayResolutionFailure.IllegalArrangement);

        var thirdOutRuling = ThirdOutRunRulingRequirement.NotApplicable;
        if (outsAfterPlay == 3)
        {
            thirdOutRuling = ThirdOutRunRulingEvaluator.Evaluate(
                context.CurrentOuts,
                context.BatterPlayerId,
                context.Participants,
                context.Result);
            var scoringPlayerIds = context.Participants
                .Where(item => item.Destination == RunnerDestination.Scores)
                .OrderByDescending(item => item.StartingLocation)
                .Select(item => item.PlayerId)
                .ToArray();
            if (thirdOutRuling == ThirdOutRunRulingRequirement.ScorerDecisionRequired
                && scoringPlayerIds.Length > 0)
                return Accepted(
                    context,
                    PlayResolutionStatus.AwaitingDecision,
                    PlayDecisionRequirement.ThirdOutRuling,
                    scoringPlayerIds,
                    thirdOutRulingRequirement: thirdOutRuling);
        }

        if (PendingFielderChoiceCompletion.CanAutoComplete(
                context.Result, context.Participants, context.CurrentOuts)
            || context.Result == BatterResult.FielderChoice
                && outsAfterPlay == 3
            || definition.RequiredPlayOuts > 0
                && context.Participants.All(item =>
                    item.Destination == RunnerDestination.Out))
            return Accepted(
                context,
                PlayResolutionStatus.AutoComplete,
                thirdOutRulingRequirement: thirdOutRuling);

        return Accepted(
            context,
            PlayResolutionStatus.ReadyForNextBatter,
            thirdOutRulingRequirement: thirdOutRuling);
    }

    private static PlayContext? ResolveRemainingFielderChoiceParticipants(
        PlayContext context,
        PlateAppearanceResultDefinition definition)
    {
        if (!TryCreateResolverState(context, out var seedState)
            || seedState is null)
            return null;

        var fixedOutcomes = context.Participants
            .Where(item => !item.RequiresOutcomeConfirmation)
            .ToDictionary(item => item.PlayerId, item => item.Destination);
        var arrangement = GetCompatibleArrangements(
                context, seedState, definition, fixedOutcomes)
            .OrderBy(item => item.Participants.Count(participant =>
                participant.Destination == RunnerDestination.Out))
            .ThenBy(item => item.Participants.Count(participant =>
                participant.Destination != context.Participants.Single(original =>
                    original.PlayerId == participant.PlayerId).Destination))
            .FirstOrDefault();
        if (arrangement is null) return null;

        return context.WithParticipants(context.Participants.Select(item =>
            item.RequiresOutcomeConfirmation
                ? item with
                {
                    Destination = arrangement.Participants.Single(participant =>
                        participant.PlayerId == item.PlayerId).Destination,
                    SafeDestination = null,
                    AttemptedDestination = null,
                    RequiresOutcomeConfirmation = false,
                }
                : item));
    }

    public PlayResolutionResult ApplyParticipantOutcome(
        PlayContext? context,
        int playerId,
        RunnerOutcomeChoice? outcome)
    {
        if (context is null || playerId <= 0 || outcome is null)
            return Rejected(context, PlayResolutionFailure.InvalidDecision);
        var selected = context.Participants.FirstOrDefault(item => item.PlayerId == playerId);
        var definition = PlateAppearanceResultDefinitions.Get(context.Result);
        if (selected is null
            || selected.IsUnorderedMultiOutRetirement
            || !outcome.IsValidFor(selected.StartingLocation)
            || !TryCreateResolverState(context, out var currentState)
            || currentState is null)
            return Rejected(context, PlayResolutionFailure.InvalidDecision);

        if (definition.RequiresExplicitOutcomeConfirmation
            && definition.RequiredPlayOuts == 0)
            return ApplyGuidedParticipantOutcome(
                context, currentState, playerId, outcome, definition);

        if (!BaseRunnerResolver.IsLegalArrangement(currentState))
            return Rejected(context, PlayResolutionFailure.InvalidDecision);

        if (outcome.IsOutAttemptingAdvance
            && (!BaseRunnerResolver.TryGetOutAttemptingAdvance(
                    currentState, playerId, out var availableOutcome)
                || availableOutcome != outcome))
            return Rejected(context, PlayResolutionFailure.InvalidDecision);

        var resolution = BaseRunnerResolver.ResolveCorrection(
            currentState, playerId, outcome.Destination);
        if (!resolution.IsSuccess
            || context.CurrentOuts + resolution.Participants.Count(item =>
                item.Destination == RunnerDestination.Out) > 3
            || !IsCompatibleWithDefinition(
                context, definition, new(resolution.Participants)))
            return Rejected(context, PlayResolutionFailure.IllegalArrangement);

        var nextOutOrder = definition.RequiredPlayOuts > 0
            && outcome.Destination == RunnerDestination.Out
            && selected.OutOrder is null
                ? context.Participants.Where(item => item.OutOrder is not null)
                    .Select(item => item.OutOrder!.Value)
                    .DefaultIfEmpty(0).Max() + 1
                : (int?)null;
        var updated = context.Participants.Select(item =>
        {
            var destination = resolution.Participants.Single(participant =>
                participant.PlayerId == item.PlayerId).Destination;
            if (item.PlayerId == playerId)
                return item with
                {
                    Destination = destination,
                    SafeDestination = outcome.SafeDestination,
                    AttemptedDestination = outcome.AttemptedDestination,
                    OutOrder = nextOutOrder ?? item.OutOrder,
                    RequiresOutcomeConfirmation = false,
                };
            return item with
            {
                Destination = destination,
                SafeDestination = destination == RunnerDestination.Out
                    ? item.SafeDestination : null,
                AttemptedDestination = destination == RunnerDestination.Out
                    ? item.AttemptedDestination : null,
            };
        }).ToArray();
        return Evaluate(context.WithParticipants(updated));
    }

    private PlayResolutionResult ApplyGuidedParticipantOutcome(
        PlayContext context,
        BaseRunnerResolutionState seedState,
        int playerId,
        RunnerOutcomeChoice outcome,
        PlateAppearanceResultDefinition definition)
    {
        if (context.Result == BatterResult.FielderChoice && context.CurrentOuts == 2
            || outcome.IsOutAttemptingAdvance
                && !GetGuidedOutcomeChoices(
                    context, seedState, playerId, definition).Contains(outcome))
            return Rejected(context, PlayResolutionFailure.InvalidDecision);

        var fixedOutcomes = context.Participants
            .Where(item => !item.RequiresOutcomeConfirmation
                && item.PlayerId != playerId)
            .ToDictionary(item => item.PlayerId, item => item.Destination);
        fixedOutcomes[playerId] = outcome.Destination;
        var arrangements = GetCompatibleArrangements(
            context, seedState, definition, fixedOutcomes);
        if (arrangements.Count == 0)
            return Rejected(context, PlayResolutionFailure.IllegalArrangement);

        var resolvedOutcomes = new Dictionary<int, RunnerDestination>(fixedOutcomes);
        var establishesThirdOut = context.CurrentOuts
            + fixedOutcomes.Count(item => item.Value == RunnerDestination.Out) == 3;
        if (establishesThirdOut)
        {
            var thirdOutArrangement = arrangements
                .OrderByDescending(arrangement => arrangement.Participants.Count(
                    participant => participant.Destination == RunnerDestination.Scores))
                .First();
            foreach (var participant in thirdOutArrangement.Participants)
                resolvedOutcomes[participant.PlayerId] = participant.Destination;
        }
        else
        {
            foreach (var unresolved in context.Participants.Where(item =>
                         !resolvedOutcomes.ContainsKey(item.PlayerId)))
            {
                var outcomes = arrangements.Select(arrangement => arrangement.Participants
                        .Single(participant => participant.PlayerId == unresolved.PlayerId)
                        .Destination)
                    .Distinct()
                    .ToArray();
                if (outcomes.Length == 1)
                    resolvedOutcomes[unresolved.PlayerId] = outcomes[0];
            }
        }

        var representative = arrangements[0];
        var updated = context.Participants.Select(item =>
        {
            var destination = resolvedOutcomes.TryGetValue(
                item.PlayerId, out var resolvedDestination)
                    ? resolvedDestination
                    : representative.Participants.Single(participant =>
                        participant.PlayerId == item.PlayerId).Destination;
            if (item.PlayerId == playerId)
                return item with
                {
                    Destination = destination,
                    SafeDestination = outcome.SafeDestination,
                    AttemptedDestination = outcome.AttemptedDestination,
                    RequiresOutcomeConfirmation = false,
                };
            return item with
            {
                Destination = destination,
                SafeDestination = destination == RunnerDestination.Out
                    ? item.SafeDestination : null,
                AttemptedDestination = destination == RunnerDestination.Out
                    ? item.AttemptedDestination : null,
                RequiresOutcomeConfirmation =
                    !resolvedOutcomes.ContainsKey(item.PlayerId),
            };
        }).ToArray();
        return Evaluate(context.WithParticipants(updated));
    }

    public PlayResolutionResult ApplyRetirementSelection(
        PlayContext? context,
        IReadOnlySet<int>? retiredPlayerIds)
    {
        if (context is null || retiredPlayerIds is null
            || !PlateAppearanceResultDefinitions.TryGet(
                context.Result, out var definition)
            || definition is null
            || definition.RequiredPlayOuts == 0
            || retiredPlayerIds.Count != definition.RequiredPlayOuts
            || retiredPlayerIds.Any(playerId => context.Participants.All(item =>
                item.PlayerId != playerId)))
            return Rejected(context, PlayResolutionFailure.InvalidDecision);

        var outOrders = context.Participants
            .Where(item => retiredPlayerIds.Contains(item.PlayerId))
            .Select((item, index) => (item.PlayerId, Order: index + 1))
            .ToDictionary(item => item.PlayerId, item => item.Order);
        var updated = context.Participants.Select(item =>
            outOrders.TryGetValue(item.PlayerId, out var outOrder)
                ? item with
                {
                    Destination = RunnerDestination.Out,
                    SafeDestination = null,
                    AttemptedDestination = null,
                    OutOrder = outOrder,
                    IsUnorderedMultiOutRetirement = true,
                    RequiresOutcomeConfirmation = false,
                }
                : item with
                {
                    OutOrder = null,
                    IsUnorderedMultiOutRetirement = false,
                    RequiresOutcomeConfirmation = false,
                }).ToArray();

        ApplyContinuousForceDefaults(updated);
        var proposed = context.WithParticipants(updated);
        if (!TryCreateResolverState(proposed, out var proposedState)
            || proposedState is null)
            return Rejected(context, PlayResolutionFailure.IllegalArrangement);

        if (!BaseRunnerResolver.IsLegalArrangement(proposedState)
            || !IsCompatibleWithDefinition(proposed, definition, proposedState))
        {
            var fallback = BaseRunnerResolver.GetLegalArrangements(proposedState)
                .Where(arrangement => IsCompatibleWithDefinition(
                    proposed, definition, arrangement))
                .Where(arrangement => retiredPlayerIds.All(playerId =>
                    arrangement.Participants.Single(item =>
                        item.PlayerId == playerId).Destination == RunnerDestination.Out))
                .OrderBy(arrangement => arrangement.Participants.Count(participant =>
                    participant.Destination != proposedState.Participants.Single(item =>
                        item.PlayerId == participant.PlayerId).Destination))
                .FirstOrDefault();
            if (fallback is null)
                return Rejected(context, PlayResolutionFailure.IllegalArrangement);
            updated = updated.Select(item => retiredPlayerIds.Contains(item.PlayerId)
                ? item
                : item with
                {
                    Destination = fallback.Participants.Single(participant =>
                        participant.PlayerId == item.PlayerId).Destination,
                    SafeDestination = null,
                    AttemptedDestination = null,
                }).ToArray();
            proposed = context.WithParticipants(updated);
        }

        return Evaluate(proposed);
    }

    public IReadOnlyList<RunnerOutcomeChoice> GetAvailableOutcomes(
        PlayContext? context,
        int playerId)
    {
        if (context is null
            || context.Participants.FirstOrDefault(item => item.PlayerId == playerId)
                is not { IsUnorderedMultiOutRetirement: false }
            || !TryCreateResolverState(context, out var state)
            || state is null)
            return [];

        var definition = PlateAppearanceResultDefinitions.Get(context.Result);
        var resolution = Evaluate(context);
        if (!resolution.IsAccepted
            || resolution.DecisionRequirement == PlayDecisionRequirement.RetirementSelection)
            return [];

        if (definition.RequiresExplicitOutcomeConfirmation
            && definition.RequiredPlayOuts == 0)
            return GetGuidedOutcomeChoices(context, state, playerId, definition);

        if (!BaseRunnerResolver.IsLegalArrangement(state)) return [];
        var choices = BaseRunnerResolver.GetAvailableDestinations(state, playerId)
            .Where(destination =>
            {
                var resolution = BaseRunnerResolver.ResolveCorrection(
                    state, playerId, destination);
                return resolution.IsSuccess
                    && context.CurrentOuts + resolution.Participants.Count(item =>
                        item.Destination == RunnerDestination.Out) <= 3
                    && IsCompatibleWithDefinition(
                        context, definition, new(resolution.Participants));
            })
            .Select(RunnerOutcomeChoice.ForDestination)
            .ToList();
        if (BaseRunnerResolver.TryGetOutAttemptingAdvance(
                state, playerId, out var attempted)
            && attempted is not null
            && choices.Any(item => item.Destination == RunnerDestination.Out))
            choices.Add(attempted);
        return choices.AsReadOnly();
    }

    private static IReadOnlyList<RunnerOutcomeChoice> GetGuidedOutcomeChoices(
        PlayContext context,
        BaseRunnerResolutionState seedState,
        int playerId,
        PlateAppearanceResultDefinition definition)
    {
        var selected = context.Participants.Single(item => item.PlayerId == playerId);
        var fixedOutcomes = context.Participants
            .Where(item => !item.RequiresOutcomeConfirmation && item.PlayerId != playerId)
            .ToDictionary(item => item.PlayerId, item => item.Destination);
        var arrangements = GetCompatibleArrangements(
            context, seedState, definition, fixedOutcomes);
        var choices = arrangements
            .Select(arrangement => arrangement.Participants.Single(item =>
                item.PlayerId == playerId).Destination)
            .Distinct()
            .OrderBy(item => item)
            .Select(RunnerOutcomeChoice.ForDestination)
            .ToList();

        if (!selected.RequiresOutcomeConfirmation)
        {
            var attemptedDestination = RunnerOutcomeChoice.GetNextDestination(
                selected.Destination);
            var attempted = new RunnerOutcomeChoice(
                RunnerDestination.Out, selected.Destination, attemptedDestination);
            if (attempted.IsValidFor(selected.StartingLocation))
            {
                var fixedOut = new Dictionary<int, RunnerDestination>(fixedOutcomes)
                {
                    [playerId] = RunnerDestination.Out,
                };
                if (GetCompatibleArrangements(
                        context, seedState, definition, fixedOut).Count > 0)
                    choices.Add(attempted);
            }
        }

        return choices.AsReadOnly();
    }

    private static IReadOnlyList<BaseRunnerResolutionState> GetCompatibleArrangements(
        PlayContext context,
        BaseRunnerResolutionState seedState,
        PlateAppearanceResultDefinition definition,
        IReadOnlyDictionary<int, RunnerDestination> fixedOutcomes) =>
        BaseRunnerResolver.GetLegalArrangements(seedState)
            .Where(arrangement => definition.IsCompatibleWithArrangement(
                arrangement, context.CurrentOuts))
            .Where(arrangement => context.CurrentOuts
                + arrangement.Participants.Count(item =>
                    item.Destination == RunnerDestination.Out) <= 3)
            .Where(arrangement => fixedOutcomes.All(outcome =>
                arrangement.Participants.Single(item =>
                    item.PlayerId == outcome.Key).Destination == outcome.Value))
            .ToArray();

    private static void ApplyContinuousForceDefaults(
        ResolvedPlayParticipantOutcome[] participants)
    {
        var occupied = participants
            .Where(item => item.StartingLocation != RunnerStartingLocation.Home)
            .Select(item => item.StartingLocation)
            .ToHashSet();
        var firstForced = occupied.Contains(RunnerStartingLocation.First);
        var secondForced = firstForced
            && occupied.Contains(RunnerStartingLocation.Second);
        var thirdForced = secondForced
            && occupied.Contains(RunnerStartingLocation.Third);
        for (var index = 0; index < participants.Length; index++)
        {
            var participant = participants[index];
            if (participant.Destination == RunnerDestination.Out) continue;
            var isForced = participant.StartingLocation switch
            {
                RunnerStartingLocation.First => firstForced,
                RunnerStartingLocation.Second => secondForced,
                RunnerStartingLocation.Third => thirdForced,
                _ => false,
            };
            if (!isForced) continue;
            participants[index] = participant with
            {
                Destination = participant.StartingLocation == RunnerStartingLocation.Third
                    ? RunnerDestination.Scores
                    : (RunnerDestination)((int)participant.StartingLocation + 1),
                SafeDestination = null,
                AttemptedDestination = null,
            };
        }
    }

    private static bool TryCreateResolverState(
        PlayContext context,
        out BaseRunnerResolutionState? state)
    {
        try
        {
            state = new(context.Participants.Select(item => new BaseRunnerParticipant(
                item.PlayerId, item.StartingLocation, item.Destination)));
            return true;
        }
        catch (ArgumentException)
        {
            state = null;
            return false;
        }
    }

    private static bool IsCompatibleWithDefinition(
        PlayContext context,
        PlateAppearanceResultDefinition definition,
        BaseRunnerResolutionState arrangement)
    {
        if (definition.RequiredPlayOuts == 0)
            return definition.IsCompatibleWithArrangement(arrangement, context.CurrentOuts);
        var batter = arrangement.Participants.Single(item =>
            item.StartingLocation == RunnerStartingLocation.Home);
        var outCount = arrangement.Participants.Count(item =>
            item.Destination == RunnerDestination.Out);
        return arrangement.Participants.Count >= definition.MinimumParticipants
            && outCount >= definition.RequiredPlayOuts
            && outCount <= 3 - context.CurrentOuts
            && (definition.AllowedBatterDestinations is null
                || definition.AllowedBatterDestinations.Contains(batter.Destination));
    }

    private static PlayResolutionResult Accepted(
        PlayContext context,
        PlayResolutionStatus status,
        PlayDecisionRequirement decisionRequirement = PlayDecisionRequirement.None,
        IReadOnlyList<int>? decisionPlayerIds = null,
        int requiredRetirementCount = 0,
        ThirdOutRunRulingRequirement thirdOutRulingRequirement =
            ThirdOutRunRulingRequirement.NotApplicable) =>
        new(
            context,
            status,
            decisionRequirement,
            decisionPlayerIds ?? [],
            requiredRetirementCount,
            thirdOutRulingRequirement,
            PlayResolutionFailure.None);

    private static PlayResolutionResult Rejected(
        PlayContext? context,
        PlayResolutionFailure failure) =>
        new(
            context,
            PlayResolutionStatus.Rejected,
            PlayDecisionRequirement.None,
            [],
            0,
            ThirdOutRunRulingRequirement.NotApplicable,
            failure);
}
