using SoftballScorekeeper.Core.Models;

namespace SoftballScorekeeper.Tests;

public sealed class BaseRunnerResolverTests
{
    public static IEnumerable<object[]> DefaultStates =>
    [
        ["Batter only single", State(P(Home, First))],
        ["First and batter single", State(P(FirstStart, Second), P(Home, First))],
        ["Second and batter single", State(P(SecondStart, Third), P(Home, First))],
        ["Third and batter single", State(P(ThirdStart, Scores), P(Home, First))],
        ["First and second single", State(P(SecondStart, Third), P(FirstStart, Second), P(Home, First))],
        ["First and third single", State(P(ThirdStart, Scores), P(FirstStart, Second), P(Home, First))],
        ["Second and third single", State(P(ThirdStart, Scores), P(SecondStart, Third), P(Home, First))],
        ["Bases loaded single", LoadedSingle()],
        ["Bases loaded double", State(P(ThirdStart, Scores), P(SecondStart, Scores), P(FirstStart, Third), P(Home, Second))],
        ["Bases loaded triple", LoadedTriple()],
        ["Multiple outs", State(P(ThirdStart, Scores), P(SecondStart, Out), P(FirstStart, Out), P(Home, First))],
    ];

    [Fact]
    public void State_canonicalizes_immutable_pre_play_lead_trail_order()
    {
        var state = State(P(Home, First), P(FirstStart, Second),
            P(ThirdStart, Scores), P(SecondStart, Third));

        Assert.Equal(
            [ThirdStart, SecondStart, FirstStart, Home],
            state.Participants.Select(item => item.StartingLocation));
    }

    [Fact]
    public void State_rejects_invalid_participant_identity_or_pre_play_context()
    {
        Assert.Throws<ArgumentException>(() => State());
        Assert.Throws<ArgumentException>(() => State(
            new(1, Home, First), new(1, FirstStart, Second)));
        Assert.Throws<ArgumentException>(() => State(
            new(1, Home, First), new(2, Home, Second)));
        Assert.Throws<ArgumentException>(() => State(P(FirstStart, Second)));
        Assert.Throws<ArgumentException>(() => State(
            P(ThirdStart, Scores), P(SecondStart, Third), P(FirstStart, Second),
            P(Home, First), new(999, (RunnerStartingLocation)99, Scores)));
    }

    [Theory]
    [MemberData(nameof(DefaultStates))]
    public void Single_double_and_triple_defaults_resolve_without_change(
        string _, BaseRunnerResolutionState state)
    {
        foreach (var participant in state.Participants)
        {
            var resolution = BaseRunnerResolver.ResolveCorrection(
                state, participant.PlayerId, participant.Destination);

            Assert.True(resolution.IsSuccess);
            Assert.Equal(state.Participants, resolution.Participants);
        }
    }

    [Fact]
    public void One_runner_and_batter_support_occupied_destination_displacement()
    {
        var state = State(P(FirstStart, Second), P(Home, First));

        var resolution = BaseRunnerResolver.ResolveCorrection(
            state, Player(Home), Second);

        Assert.True(resolution.IsSuccess);
        AssertDestinations(resolution, (FirstStart, Third), (Home, Second));
    }

    [Fact]
    public void Batter_moved_to_occupied_destination_uses_complete_forward_chain()
    {
        var resolution = BaseRunnerResolver.ResolveCorrection(
            LoadedSingle(), Player(Home), Second);

        Assert.True(resolution.IsSuccess);
        AssertDestinations(resolution,
            (ThirdStart, Scores), (SecondStart, Scores),
            (FirstStart, Third), (Home, Second));
        Assert.DoesNotContain(resolution.Participants,
            item => item.Destination == Out);
    }

    [Fact]
    public void Scored_runner_can_return_backward_to_third_or_second()
    {
        var toThird = BaseRunnerResolver.ResolveCorrection(
            State(P(ThirdStart, Scores), P(Home, First)), Player(ThirdStart), Third);
        var toSecond = BaseRunnerResolver.ResolveCorrection(
            State(P(SecondStart, Scores), P(Home, First)), Player(SecondStart), Second);

        Assert.True(toThird.IsSuccess);
        AssertDestinations(toThird, (ThirdStart, Third), (Home, First));
        Assert.True(toSecond.IsSuccess);
        AssertDestinations(toSecond, (SecondStart, Second), (Home, First));
    }

    [Fact]
    public void Scored_runner_can_return_to_first_when_trailing_batter_is_already_out()
    {
        var state = State(P(FirstStart, Scores), P(Home, Out));

        var resolution = BaseRunnerResolver.ResolveCorrection(
            state, Player(FirstStart), First);

        Assert.True(resolution.IsSuccess);
        AssertDestinations(resolution, (FirstStart, First), (Home, Out));
    }

    [Fact]
    public void Bases_loaded_triple_resolves_batter_and_scored_runner_corrections()
    {
        var state = LoadedTriple();

        var firstRunnerToSecond = BaseRunnerResolver.ResolveCorrection(
            state, Player(FirstStart), Second);
        var batterToSecond = BaseRunnerResolver.ResolveCorrection(
            state, Player(Home), Second);
        var batterToFirst = BaseRunnerResolver.ResolveCorrection(
            state, Player(Home), First);

        Assert.True(firstRunnerToSecond.IsSuccess);
        AssertDestinations(firstRunnerToSecond,
            (ThirdStart, Scores), (SecondStart, Scores),
            (FirstStart, Second), (Home, First));
        Assert.True(batterToSecond.IsSuccess);
        AssertDestinations(batterToSecond,
            (ThirdStart, Scores), (SecondStart, Scores),
            (FirstStart, Scores), (Home, Second));
        Assert.True(batterToFirst.IsSuccess);
        AssertDestinations(batterToFirst,
            (ThirdStart, Scores), (SecondStart, Scores),
            (FirstStart, Scores), (Home, First));
    }

    [Fact]
    public void Bases_loaded_triple_supports_sequential_scored_runner_returns_when_legal()
    {
        var firstReturn = BaseRunnerResolver.ResolveCorrection(
            LoadedTriple(), Player(FirstStart), Second);
        var stateAfterFirstReturn = State(firstReturn.Participants.ToArray());

        var secondReturn = BaseRunnerResolver.ResolveCorrection(
            stateAfterFirstReturn, Player(SecondStart), Third);

        Assert.True(firstReturn.IsSuccess);
        Assert.True(secondReturn.IsSuccess);
        AssertDestinations(secondReturn,
            (ThirdStart, Scores), (SecondStart, Third),
            (FirstStart, Second), (Home, First));
    }

    [Theory]
    [InlineData(RunnerStartingLocation.Third, RunnerDestination.Third)]
    [InlineData(RunnerStartingLocation.Second, RunnerDestination.Second)]
    [InlineData(RunnerStartingLocation.First, RunnerDestination.First)]
    public void Bases_loaded_triple_rejects_returns_with_insufficient_trailing_capacity(
        RunnerStartingLocation participant, RunnerDestination destination)
    {
        var resolution = BaseRunnerResolver.ResolveCorrection(
            LoadedTriple(), Player(participant), destination);

        Assert.False(resolution.IsSuccess);
        Assert.Empty(resolution.Participants);
    }

    [Fact]
    public void No_passing_rejects_old_backward_cascade_arrangement()
    {
        var state = LoadedSingle();

        var resolution = BaseRunnerResolver.ResolveCorrection(
            state, Player(ThirdStart), Third);

        Assert.False(resolution.IsSuccess);
        Assert.DoesNotContain(Third,
            BaseRunnerResolver.GetAvailableDestinations(state, Player(ThirdStart)));
    }

    [Fact]
    public void Multiple_scores_and_multiple_explicit_outs_are_preserved()
    {
        var state = State(P(ThirdStart, Scores), P(SecondStart, Out),
            P(FirstStart, Out), P(Home, First));

        var resolution = BaseRunnerResolver.ResolveCorrection(
            state, Player(ThirdStart), Third);

        Assert.True(resolution.IsSuccess);
        AssertDestinations(resolution,
            (ThirdStart, Third), (SecondStart, Out),
            (FirstStart, Out), (Home, First));
    }

    [Fact]
    public void Explicit_batter_out_is_supported_without_changing_other_outcomes()
    {
        var state = State(P(SecondStart, Third), P(Home, First));

        var resolution = BaseRunnerResolver.ResolveCorrection(
            state, Player(Home), Out);

        Assert.True(resolution.IsSuccess);
        AssertDestinations(resolution, (SecondStart, Third), (Home, Out));
    }

    [Fact]
    public void Explicit_runner_out_and_multiple_outs_preserve_every_other_outcome()
    {
        var firstOut = BaseRunnerResolver.ResolveCorrection(
            State(P(SecondStart, Third), P(FirstStart, Second), P(Home, First)),
            Player(SecondStart), Out);
        var stateWithFirstOut = State(firstOut.Participants.ToArray());

        var secondOut = BaseRunnerResolver.ResolveCorrection(
            stateWithFirstOut, Player(FirstStart), Out);

        Assert.True(firstOut.IsSuccess);
        Assert.True(secondOut.IsSuccess);
        AssertDestinations(secondOut,
            (SecondStart, Out), (FirstStart, Out), (Home, First));
    }

    [Fact]
    public void Resolver_rejects_malformed_current_arrangement_instead_of_repairing_it()
    {
        var duplicateBaseState = State(P(FirstStart, First), P(Home, First));

        var resolution = BaseRunnerResolver.ResolveCorrection(
            duplicateBaseState, Player(Home), Second);

        Assert.False(resolution.IsSuccess);
        Assert.Empty(BaseRunnerResolver.GetAvailableDestinations(
            duplicateBaseState, Player(Home)));
    }

    [Fact]
    public void Runner_correction_does_not_own_or_rewrite_batter_result()
    {
        const BatterResult selectedResult = BatterResult.Single;
        var state = State(P(Home, First));

        var resolution = BaseRunnerResolver.ResolveCorrection(
            state, Player(Home), Second);

        Assert.True(resolution.IsSuccess);
        Assert.Equal(Second, Assert.Single(resolution.Participants).Destination);
        Assert.Equal(BatterResult.Single, selectedResult);
    }

    [Fact]
    public void Ranking_occupied_destination_prefers_least_physical_displacement()
    {
        var resolution = BaseRunnerResolver.ResolveCorrection(
            State(P(FirstStart, Second), P(Home, First)), Player(Home), Second);

        Assert.True(resolution.IsSuccess);
        AssertDestinations(resolution, (FirstStart, Third), (Home, Second));
    }

    [Fact]
    public void Ranking_multiple_scored_runners_chooses_nearest_trailing_base()
    {
        var resolution = BaseRunnerResolver.ResolveCorrection(
            State(P(ThirdStart, Scores), P(SecondStart, Scores), P(Home, Out)),
            Player(ThirdStart), Third);

        Assert.True(resolution.IsSuccess);
        AssertDestinations(resolution,
            (ThirdStart, Third), (SecondStart, Second), (Home, Out));
    }

    [Fact]
    public void Ranking_bases_loaded_chain_preserves_exact_identity_order()
    {
        var resolution = BaseRunnerResolver.ResolveCorrection(
            LoadedSingle(), Player(Home), Second);

        Assert.True(resolution.IsSuccess);
        AssertDestinations(resolution,
            (ThirdStart, Scores), (SecondStart, Scores),
            (FirstStart, Third), (Home, Second));
    }

    [Fact]
    public void Generated_legal_state_matrix_preserves_equivalence_invariants_and_determinism()
    {
        var participantSets = new[]
        {
            new[] { Home },
            new[] { FirstStart, Home },
            new[] { SecondStart, Home },
            new[] { ThirdStart, Home },
            new[] { SecondStart, FirstStart, Home },
            new[] { ThirdStart, FirstStart, Home },
            new[] { ThirdStart, SecondStart, Home },
            new[] { ThirdStart, SecondStart, FirstStart, Home },
        };
        var stateCount = 0;
        var requestCount = 0;

        foreach (var participantSet in participantSets)
        foreach (var state in GenerateLegalStates(participantSet))
        {
            stateCount++;
            foreach (var selected in state.Participants)
            {
                var available = BaseRunnerResolver.GetAvailableDestinations(
                    state, selected.PlayerId);
                foreach (var destination in Enum.GetValues<RunnerDestination>())
                {
                    requestCount++;
                    var first = BaseRunnerResolver.ResolveCorrection(
                        state, selected.PlayerId, destination);
                    var repeated = BaseRunnerResolver.ResolveCorrection(
                        state, selected.PlayerId, destination);

                    Assert.Equal(first.IsSuccess, available.Contains(destination));
                    Assert.Equal(first.IsSuccess, repeated.IsSuccess);
                    if (!first.IsSuccess) continue;
                    Assert.Equal(OrderedOutcomes(first), OrderedOutcomes(repeated));
                    AssertResolvedInvariants(
                        state, selected.PlayerId, destination, first);
                }
            }
        }

        Assert.True(stateCount > 100);
        Assert.True(requestCount > 1_000);
    }

    [Theory]
    [MemberData(nameof(DefaultStates))]
    public void Availability_is_equivalent_to_resolution_and_every_success_is_complete(
        string _, BaseRunnerResolutionState state)
    {
        foreach (var selected in state.Participants)
        {
            var available = BaseRunnerResolver.GetAvailableDestinations(
                state, selected.PlayerId);
            foreach (var destination in Enum.GetValues<RunnerDestination>())
            {
                var first = BaseRunnerResolver.ResolveCorrection(
                    state, selected.PlayerId, destination);
                var repeated = BaseRunnerResolver.ResolveCorrection(
                    state, selected.PlayerId, destination);

                Assert.Equal(first.IsSuccess, available.Contains(destination));
                Assert.Equal(first.IsSuccess, repeated.IsSuccess);
                if (!first.IsSuccess) continue;
                Assert.Equal(first.Participants, repeated.Participants);
                AssertResolvedInvariants(state, selected.PlayerId, destination, first);
            }
        }
    }

    private static void AssertResolvedInvariants(
        BaseRunnerResolutionState state,
        int selectedPlayerId,
        RunnerDestination requestedDestination,
        BaseRunnerResolutionResult resolution)
    {
        Assert.Equal(state.Participants.Count, resolution.Participants.Count);
        Assert.Equal(state.Participants.Select(item => item.PlayerId),
            resolution.Participants.Select(item => item.PlayerId));
        Assert.Equal(state.Participants.Select(item => item.StartingLocation),
            resolution.Participants.Select(item => item.StartingLocation));
        Assert.Equal(requestedDestination, resolution.Participants
            .Single(item => item.PlayerId == selectedPlayerId).Destination);
        Assert.Equal(resolution.Participants.Count,
            resolution.Participants.Select(item => item.PlayerId).Distinct().Count());
        Assert.Equal(resolution.Participants.Count(item => IsSafeBase(item.Destination)),
            resolution.Participants.Where(item => IsSafeBase(item.Destination))
                .Select(item => item.Destination).Distinct().Count());

        foreach (var participant in state.Participants
            .Where(item => item.PlayerId != selectedPlayerId))
            Assert.Equal(participant.Destination == Out,
                resolution.Participants.Single(item => item.PlayerId == participant.PlayerId)
                    .Destination == Out);

        for (var lead = 0; lead < resolution.Participants.Count; lead++)
        for (var trail = lead + 1; trail < resolution.Participants.Count; trail++)
        {
            var leadDestination = resolution.Participants[lead].Destination;
            var trailDestination = resolution.Participants[trail].Destination;
            if (leadDestination == Out || trailDestination == Out) continue;
            Assert.True((int)leadDestination >= (int)trailDestination);
        }
    }

    private static void AssertDestinations(
        BaseRunnerResolutionResult resolution,
        params (RunnerStartingLocation Start, RunnerDestination Destination)[] expected)
    {
        Assert.Equal(expected.Length, resolution.Participants.Count);
        foreach (var item in expected)
            Assert.Equal(item.Destination, resolution.Participants
                .Single(participant => participant.StartingLocation == item.Start).Destination);
    }

    private static IReadOnlyList<(int PlayerId, RunnerStartingLocation Start,
        RunnerDestination Destination)> OrderedOutcomes(
        BaseRunnerResolutionResult resolution) => resolution.Participants
            .Select(item => (item.PlayerId, item.StartingLocation, item.Destination))
            .ToArray();

    private static IEnumerable<BaseRunnerResolutionState> GenerateLegalStates(
        IReadOnlyList<RunnerStartingLocation> participantSet)
    {
        var destinations = new RunnerDestination[participantSet.Count];
        foreach (var state in Generate(0)) yield return state;

        IEnumerable<BaseRunnerResolutionState> Generate(int index)
        {
            if (index == participantSet.Count)
            {
                if (IsLegal(destinations))
                    yield return State(participantSet.Select((start, participantIndex) =>
                        P(start, destinations[participantIndex])).ToArray());
                yield break;
            }

            foreach (var destination in Enum.GetValues<RunnerDestination>())
            {
                destinations[index] = destination;
                foreach (var state in Generate(index + 1)) yield return state;
            }
        }
    }

    private static bool IsLegal(IReadOnlyList<RunnerDestination> destinations)
    {
        var safeBases = destinations.Where(IsSafeBase).ToArray();
        if (safeBases.Distinct().Count() != safeBases.Length) return false;
        for (var lead = 0; lead < destinations.Count; lead++)
        for (var trail = lead + 1; trail < destinations.Count; trail++)
        {
            if (destinations[lead] == Out || destinations[trail] == Out) continue;
            if ((int)destinations[lead] < (int)destinations[trail]) return false;
        }
        return true;
    }

    private static bool IsSafeBase(RunnerDestination destination) => destination
        is First or Second or Third;

    private static BaseRunnerResolutionState LoadedSingle() => State(
        P(ThirdStart, Scores), P(SecondStart, Third),
        P(FirstStart, Second), P(Home, First));

    private static BaseRunnerResolutionState LoadedTriple() => State(
        P(ThirdStart, Scores), P(SecondStart, Scores),
        P(FirstStart, Scores), P(Home, Third));

    private static BaseRunnerResolutionState State(
        params BaseRunnerParticipant[] participants) => new(participants);

    private static BaseRunnerParticipant P(
        RunnerStartingLocation startingLocation, RunnerDestination destination) =>
        new(Player(startingLocation), startingLocation, destination);

    private static int Player(RunnerStartingLocation startingLocation) =>
        100 + (int)startingLocation;

    private const RunnerStartingLocation Home = RunnerStartingLocation.Home;
    private const RunnerStartingLocation FirstStart = RunnerStartingLocation.First;
    private const RunnerStartingLocation SecondStart = RunnerStartingLocation.Second;
    private const RunnerStartingLocation ThirdStart = RunnerStartingLocation.Third;
    private const RunnerDestination First = RunnerDestination.First;
    private const RunnerDestination Second = RunnerDestination.Second;
    private const RunnerDestination Third = RunnerDestination.Third;
    private const RunnerDestination Scores = RunnerDestination.Scores;
    private const RunnerDestination Out = RunnerDestination.Out;
}
