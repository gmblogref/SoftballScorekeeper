namespace SoftballScorekeeper.Core.Models;

public sealed class PlayResolutionCoordinator
{
    public PlayResolutionResult InitializeResult(PlayContext? context)
    {
        if (context?.Result != BatterResult.SacrificeFly)
            return Evaluate(context);

        var thirdBaseRunner = context.Participants.SingleOrDefault(item =>
            item.StartingLocation == RunnerStartingLocation.Third);
        if (thirdBaseRunner is null)
            return Rejected(context, PlayResolutionFailure.ResultUnavailable);

        var initialized = context.WithParticipants(context.Participants.Select(item =>
            item.PlayerId == thirdBaseRunner.PlayerId
                ? item with
                {
                    Destination = RunnerDestination.Scores,
                    SafeDestination = null,
                    AttemptedDestination = null,
                    OutOrder = null,
                    RequiresOutcomeConfirmation = false,
                }
                : item));
        return Evaluate(initialized);
    }

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
            || definition.RequiredOccupiedStartingLocation is not null
                && !context.OccupiedStartingLocations.Contains(
                    definition.RequiredOccupiedStartingLocation.Value)
            || context.CurrentOuts > definition.MaximumCurrentOuts
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
            return EvaluateOrdinaryOut(context);

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
            && context.Participants.All(item =>
                item.Destination != RunnerDestination.Out))
        {
            var retirementCandidates = context.Participants
                .Select(item => item.PlayerId)
                .ToArray();
            return Accepted(
                context,
                PlayResolutionStatus.AwaitingDecision,
                PlayDecisionRequirement.RetirementSelection,
                retirementCandidates,
                requiredRetirementCount: 1);
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
        {
            if (TryCreateResolverState(context, out var unresolvedState)
                && unresolvedState is not null)
            {
                var outcomes = GetParticipantOutcomeChoices(
                    context, unresolvedState, unresolved[0], definition);
                if (outcomes.Count == 1)
                    return ApplyParticipantOutcome(
                        context, unresolved[0], outcomes[0]);
            }
            return Accepted(
                context,
                PlayResolutionStatus.AwaitingDecision,
                PlayDecisionRequirement.ParticipantOutcome,
                unresolved);
        }

        if (!TryCreateResolverState(context, out var resolverState)
            || resolverState is null
            || !BaseRunnerResolver.IsLegalArrangement(resolverState)
            || !IsCompatibleWithDefinition(context, definition, resolverState))
            return Rejected(context, PlayResolutionFailure.IllegalArrangement);

        var outsAfterPlay = context.CurrentOuts + context.Participants.Count(item =>
            item.Destination == RunnerDestination.Out);
        if (outsAfterPlay > 3)
            return Rejected(context, PlayResolutionFailure.IllegalArrangement);

        if (context.Result == BatterResult.SacrificeFly
            && !SacrificeFlyCompletionEvaluator.IsSatisfied(context.Participants))
            return Accepted(context, PlayResolutionStatus.AwaitingDecision);

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

        if ((context.Result == BatterResult.FielderChoice
                && outsAfterPlay == 3)
            || (definition.RequiredPlayOuts > 0
                && context.Participants.All(item =>
                    item.Destination == RunnerDestination.Out)))
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
        var defaultedParticipants = context.Participants.ToArray();
        ApplyFielderChoiceDisplacementDefaults(defaultedParticipants);
        var defaultedContext = context.WithParticipants(defaultedParticipants);
        if (!TryCreateResolverState(defaultedContext, out var seedState)
            || seedState is null)
            return null;

        var fixedOutcomes = defaultedContext.Participants
            .Where(item => !item.RequiresOutcomeConfirmation)
            .ToDictionary(item => item.PlayerId, item => item.Destination);
        var arrangement = GetCompatibleArrangements(
                defaultedContext, seedState, definition, fixedOutcomes)
            .OrderBy(item => item.Participants.Count(participant =>
                participant.Destination == RunnerDestination.Out))
            .ThenBy(item => item.Participants.Count(participant =>
                participant.Destination != defaultedContext.Participants.Single(original =>
                    original.PlayerId == participant.PlayerId).Destination))
            .FirstOrDefault();
        if (arrangement is null) return null;

        return defaultedContext.WithParticipants(defaultedContext.Participants.Select(item =>
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
            || context.Result == BatterResult.FielderChoice
                && selected.StartingLocation == RunnerStartingLocation.Home
                && outcome.Destination == RunnerDestination.Out
            || context.Result == BatterResult.FielderChoice
                && outcome == RunnerOutcomeChoice.ForDestination(
                    RunnerDestination.Out)
                && context.Participants.Any(item =>
                    item.Destination == RunnerDestination.Out)
            || !outcome.IsValidFor(selected.StartingLocation)
            || !TryCreateResolverState(context, out var currentState)
            || currentState is null)
            return Rejected(context, PlayResolutionFailure.InvalidDecision);

        if (definition.RequiresExplicitOutcomeConfirmation
            && definition.RequiredPlayOuts == 0
            && selected.RequiresOutcomeConfirmation)
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

        var nextOutOrder = definition.UsesOrderedOuts
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
                    OutOrder = destination == RunnerDestination.Out
                        ? nextOutOrder ?? item.OutOrder
                        : null,
                    RequiresOutcomeConfirmation = false,
                };
            return item with
            {
                Destination = destination,
                SafeDestination = destination == RunnerDestination.Out
                    ? item.SafeDestination : null,
                AttemptedDestination = destination == RunnerDestination.Out
                    ? item.AttemptedDestination : null,
                OutOrder = destination == RunnerDestination.Out
                    ? item.OutOrder : null,
            };
        }).ToArray();
        var updatedContext = context.WithParticipants(updated);
        if (context.Result == BatterResult.SacrificeFly
            && updatedContext.Participants.Single(item =>
                item.StartingLocation == RunnerStartingLocation.Third).Destination
                != RunnerDestination.Scores)
            return ReconcileToOrdinaryOut(updatedContext);

        return Evaluate(updatedContext);
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
            || definition is null)
            return Rejected(context, PlayResolutionFailure.InvalidDecision);

        if (context.Result == BatterResult.FielderChoice)
        {
            if (retiredPlayerIds.Count != 1
                || context.Participants.SingleOrDefault(item =>
                    retiredPlayerIds.Contains(item.PlayerId)) is not { } selected)
                return Rejected(context, PlayResolutionFailure.InvalidDecision);
            if (selected.StartingLocation == RunnerStartingLocation.Home)
            {
                if (!TryCreateResolverState(context, out var state)
                    || state is null)
                    return Rejected(context, PlayResolutionFailure.InvalidDecision);
                return ReconcileFielderChoiceBatterOut(context, state);
            }
            return ApplyFielderChoiceRetirementSelection(
                context, selected.PlayerId, definition);
        }

        if (definition.RequiredPlayOuts == 0
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

    private PlayResolutionResult ApplyFielderChoiceRetirementSelection(
        PlayContext context,
        int retiredPlayerId,
        PlateAppearanceResultDefinition definition)
    {
        var selected = context.Participants.SingleOrDefault(item =>
            item.PlayerId == retiredPlayerId);
        if (selected is null
            || selected.StartingLocation == RunnerStartingLocation.Home)
            return Rejected(context, PlayResolutionFailure.InvalidDecision);

        var selectedContext = context.WithParticipants(context.Participants.Select(item =>
            item.PlayerId == retiredPlayerId
                ? item with
                {
                    Destination = RunnerDestination.Out,
                    SafeDestination = null,
                    AttemptedDestination = null,
                    RequiresOutcomeConfirmation = false,
                }
                : item));
        var completed = ResolveRemainingFielderChoiceParticipants(
            selectedContext, definition);
        return completed is null
            ? Rejected(context, PlayResolutionFailure.IllegalArrangement)
            : Evaluate(completed);
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

        return GetParticipantOutcomeChoices(context, state, playerId, definition);
    }

    private static IReadOnlyList<RunnerOutcomeChoice> GetParticipantOutcomeChoices(
        PlayContext context,
        BaseRunnerResolutionState state,
        int playerId,
        PlateAppearanceResultDefinition definition)
    {
        if (definition.RequiresExplicitOutcomeConfirmation
            && definition.RequiredPlayOuts == 0
            && context.Participants.Single(item =>
                item.PlayerId == playerId).RequiresOutcomeConfirmation)
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
        var canBecomeOut = choices.Any(item =>
            item.Destination == RunnerDestination.Out);
        if (context.Result == BatterResult.FielderChoice
            && context.Participants.Any(item =>
                item.Destination == RunnerDestination.Out))
            choices.RemoveAll(item =>
                item == RunnerOutcomeChoice.ForDestination(RunnerDestination.Out));
        if (BaseRunnerResolver.TryGetOutAttemptingAdvance(
                state, playerId, out var attempted)
            && attempted is not null
            && canBecomeOut)
            choices.Add(attempted);
        return choices.Distinct().ToArray();
    }

    private PlayResolutionResult ReconcileFielderChoiceBatterOut(
        PlayContext context,
        BaseRunnerResolutionState seedState)
    {
        var fixedOutcomes = context.Participants
            .Where(item => item.StartingLocation != RunnerStartingLocation.Home
                && !item.RequiresOutcomeConfirmation)
            .ToDictionary(item => item.PlayerId, item => item.Destination);
        var arrangement = GetOrdinaryOutArrangements(context, seedState, fixedOutcomes)
            .OrderBy(item => item.Participants.Count(participant =>
                participant.Destination != context.Participants.Single(original =>
                    original.PlayerId == participant.PlayerId).Destination))
            .ThenBy(item => item.Participants.Count(participant =>
                participant.Destination == RunnerDestination.Out))
            .FirstOrDefault();
        return arrangement is null
            ? Rejected(context, PlayResolutionFailure.IllegalArrangement)
            : ReconcileToOrdinaryOut(context.WithParticipants(
                ApplyResolvedDestinations(context, arrangement)));
    }

    private PlayResolutionResult ReconcileToOrdinaryOut(PlayContext context)
    {
        var participants = context.Participants.Select(item => item with
        {
            SafeDestination = item.Destination == RunnerDestination.Out
                && item.StartingLocation != RunnerStartingLocation.Home
                    ? item.SafeDestination : null,
            AttemptedDestination = item.Destination == RunnerDestination.Out
                && item.StartingLocation != RunnerStartingLocation.Home
                    ? item.AttemptedDestination : null,
            OutOrder = null,
            IsUnorderedMultiOutRetirement = false,
            RequiresOutcomeConfirmation = false,
        }).ToArray();
        return Evaluate(context.WithResultAndParticipants(
            BatterResult.Out, participants));
    }

    private static IReadOnlyList<ResolvedPlayParticipantOutcome> ApplyResolvedDestinations(
        PlayContext context,
        BaseRunnerResolutionState arrangement) => context.Participants.Select(item =>
    {
        var destination = arrangement.Participants.Single(participant =>
            participant.PlayerId == item.PlayerId).Destination;
        return item with
        {
            Destination = destination,
            SafeDestination = destination == RunnerDestination.Out
                ? item.SafeDestination : null,
            AttemptedDestination = destination == RunnerDestination.Out
                ? item.AttemptedDestination : null,
        };
    }).ToArray();

    private static IReadOnlyList<BaseRunnerResolutionState> GetOrdinaryOutArrangements(
        PlayContext context,
        BaseRunnerResolutionState seedState,
        IReadOnlyDictionary<int, RunnerDestination> fixedExistingOutcomes) =>
        BaseRunnerResolver.GetLegalArrangements(seedState)
            .Where(arrangement => arrangement.Participants.Single(item =>
                item.StartingLocation == RunnerStartingLocation.Home).Destination
                == RunnerDestination.Out)
            .Where(arrangement => context.CurrentOuts
                + arrangement.Participants.Count(item =>
                    item.Destination == RunnerDestination.Out) <= 3)
            .Where(arrangement => fixedExistingOutcomes.All(outcome =>
                arrangement.Participants.Single(item =>
                    item.PlayerId == outcome.Key).Destination == outcome.Value))
            .ToArray();

    private static PlayResolutionResult EvaluateOrdinaryOut(PlayContext context)
    {
        if (!TryCreateResolverState(context, out var state)
            || state is null
            || !BaseRunnerResolver.IsLegalArrangement(state))
            return Rejected(context, PlayResolutionFailure.IllegalArrangement);
        var outsAfterPlay = context.CurrentOuts + context.Participants.Count(item =>
            item.Destination == RunnerDestination.Out);
        if (outsAfterPlay > 3)
            return Rejected(context, PlayResolutionFailure.IllegalArrangement);
        var ruling = ThirdOutRunRulingRequirement.NotApplicable;
        if (outsAfterPlay == 3)
        {
            ruling = ThirdOutRunRulingEvaluator.Evaluate(
                context.CurrentOuts, context.BatterPlayerId,
                context.Participants, context.Result);
            var scoringIds = context.Participants
                .Where(item => item.Destination == RunnerDestination.Scores)
                .OrderByDescending(item => item.StartingLocation)
                .Select(item => item.PlayerId).ToArray();
            if (ruling == ThirdOutRunRulingRequirement.ScorerDecisionRequired
                && scoringIds.Length > 0)
                return Accepted(context, PlayResolutionStatus.AwaitingDecision,
                    PlayDecisionRequirement.ThirdOutRuling, scoringIds,
                    thirdOutRulingRequirement: ruling);
        }
        var immediate = context.OccupiedStartingLocations.Count == 0
            || context.CurrentOuts == 2;
        return Accepted(context, immediate
                ? PlayResolutionStatus.CommitImmediately
                : PlayResolutionStatus.ReadyForNextBatter,
            thirdOutRulingRequirement: ruling);
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

    private static void ApplyFielderChoiceDisplacementDefaults(
        ResolvedPlayParticipantOutcome[] participants)
    {
        var primaryRetirement = participants.SingleOrDefault(item =>
            item.StartingLocation != RunnerStartingLocation.Home
            && item.Destination == RunnerDestination.Out
            && item.SafeDestination is null
            && item.AttemptedDestination is null
            && !item.RequiresOutcomeConfirmation);
        if (primaryRetirement is null) return;

        var destination = RunnerOutcomeChoice.GetNextDestination(
            (RunnerDestination)primaryRetirement.StartingLocation);
        while (destination is RunnerDestination.First
            or RunnerDestination.Second or RunnerDestination.Third)
        {
            var startingLocation = (RunnerStartingLocation)destination.Value;
            var index = Array.FindIndex(participants, item =>
                item.StartingLocation == startingLocation);
            if (index < 0 || !participants[index].RequiresOutcomeConfirmation)
                return;

            var nextDestination = RunnerOutcomeChoice.GetNextDestination(
                destination.Value);
            if (nextDestination is null) return;
            participants[index] = participants[index] with
            {
                Destination = nextDestination.Value,
                SafeDestination = null,
                AttemptedDestination = null,
            };
            destination = nextDestination;
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
