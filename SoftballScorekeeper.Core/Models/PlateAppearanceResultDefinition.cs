namespace SoftballScorekeeper.Core.Models;

public enum PlateAppearanceCompletionBehavior
{
    Pending,
    Immediate,
    ContextualOut,
}

public enum ExistingRunnerMovementBehavior
{
    Advance,
    ForcedOnly,
    Hold,
}

public sealed record PlateAppearanceResultDefinition(
    BatterResult Result,
    RunnerDestination BatterDefaultDestination,
    ExistingRunnerMovementBehavior ExistingRunnerMovement,
    int BasesAdvanced,
    PlateAppearanceCompletionBehavior CompletionBehavior,
    bool IncludesBatterMovement = true,
    bool RequiresExistingRunner = false,
    bool RequiresExplicitOutcomeConfirmation = false,
    int MinimumPlayOuts = 0,
    int RequiredPlayOuts = 0,
    int MinimumParticipants = 1,
    int MaximumCurrentOuts = 2,
    RunnerStartingLocation? RequiredOccupiedStartingLocation = null,
    IReadOnlyList<RunnerDestination>? AllowedBatterDestinations = null)
{
    public bool UsesOrderedOuts => RequiredPlayOuts > 0
        || Result == BatterResult.SacrificeFly;

    public bool RequiresOutcomeConfirmation(
        RunnerStartingLocation startingLocation) =>
        RequiresExplicitOutcomeConfirmation
        && !(Result == BatterResult.FielderChoice
            && startingLocation == RunnerStartingLocation.Home);

    public RunnerDestination GetExistingRunnerDefaultDestination(
        RunnerStartingLocation startingLocation,
        IReadOnlySet<RunnerStartingLocation> occupiedStartingLocations)
    {
        if (startingLocation == RunnerStartingLocation.Home)
            throw new ArgumentOutOfRangeException(nameof(startingLocation));

        return ExistingRunnerMovement switch
        {
            ExistingRunnerMovementBehavior.Advance =>
                (int)startingLocation + BasesAdvanced > 3
                    ? RunnerDestination.Scores
                    : (RunnerDestination)((int)startingLocation + BasesAdvanced),
            ExistingRunnerMovementBehavior.ForcedOnly => startingLocation switch
            {
                RunnerStartingLocation.First => RunnerDestination.Second,
                RunnerStartingLocation.Second
                    when occupiedStartingLocations.Contains(RunnerStartingLocation.First) =>
                    RunnerDestination.Third,
                RunnerStartingLocation.Third
                    when occupiedStartingLocations.Contains(RunnerStartingLocation.First)
                        && occupiedStartingLocations.Contains(RunnerStartingLocation.Second) =>
                    RunnerDestination.Scores,
                _ => (RunnerDestination)startingLocation,
            },
            ExistingRunnerMovementBehavior.Hold => (RunnerDestination)startingLocation,
            _ => throw new ArgumentOutOfRangeException(nameof(ExistingRunnerMovement)),
        };
    }

    public bool IsCompatibleWithArrangement(
        BaseRunnerResolutionState arrangement, int currentOuts = 0)
    {
        var batter = arrangement.Participants.Single(item =>
            item.StartingLocation == RunnerStartingLocation.Home);
        var outCount = arrangement.Participants.Count(item =>
            item.Destination == RunnerDestination.Out);
        var hasEnoughOutsRemaining = RequiredPlayOuts == 0
            || RequiredPlayOuts <= 3 - currentOuts;
        return arrangement.Participants.Count >= MinimumParticipants
            && currentOuts <= MaximumCurrentOuts
            && (RequiredOccupiedStartingLocation is null
                || arrangement.Participants.Any(item =>
                    item.StartingLocation == RequiredOccupiedStartingLocation))
            && outCount >= MinimumPlayOuts
            && hasEnoughOutsRemaining
            && (RequiredPlayOuts == 0 || outCount == RequiredPlayOuts)
            && (AllowedBatterDestinations is null
                || AllowedBatterDestinations.Contains(batter.Destination));
    }
}

public static class PlateAppearanceResultDefinitions
{
    private static readonly IReadOnlyList<PlateAppearanceResultDefinition> Definitions =
    [
        new(BatterResult.Single, RunnerDestination.First,
            ExistingRunnerMovementBehavior.Advance, 1,
            PlateAppearanceCompletionBehavior.Pending),
        new(BatterResult.Double, RunnerDestination.Second,
            ExistingRunnerMovementBehavior.Advance, 2,
            PlateAppearanceCompletionBehavior.Pending),
        new(BatterResult.Triple, RunnerDestination.Third,
            ExistingRunnerMovementBehavior.Advance, 3,
            PlateAppearanceCompletionBehavior.Pending),
        new(BatterResult.HomeRun, RunnerDestination.Scores,
            ExistingRunnerMovementBehavior.Advance, 4,
            PlateAppearanceCompletionBehavior.Immediate),
        new(BatterResult.InTheParkHomeRun, RunnerDestination.Scores,
            ExistingRunnerMovementBehavior.Advance, 4,
            PlateAppearanceCompletionBehavior.Immediate),
        new(BatterResult.DeadBallOut, RunnerDestination.Out,
            ExistingRunnerMovementBehavior.Hold, 0,
            PlateAppearanceCompletionBehavior.Immediate,
            IncludesBatterMovement: false),
        new(BatterResult.Walk, RunnerDestination.First,
            ExistingRunnerMovementBehavior.ForcedOnly, 0,
            PlateAppearanceCompletionBehavior.Immediate),
        new(BatterResult.FielderChoice, RunnerDestination.First,
            ExistingRunnerMovementBehavior.Hold, 0,
            PlateAppearanceCompletionBehavior.Pending,
            RequiresExistingRunner: true,
            RequiresExplicitOutcomeConfirmation: true,
            MinimumPlayOuts: 1,
            AllowedBatterDestinations:
            [
                RunnerDestination.First,
                RunnerDestination.Second,
                RunnerDestination.Third,
                RunnerDestination.Scores,
            ]),
        new(BatterResult.DoublePlay, RunnerDestination.First,
            ExistingRunnerMovementBehavior.Hold, 0,
            PlateAppearanceCompletionBehavior.Pending,
            RequiresExistingRunner: true,
            RequiresExplicitOutcomeConfirmation: true,
            RequiredPlayOuts: 2,
            MinimumParticipants: 2),
        new(BatterResult.TriplePlay, RunnerDestination.First,
            ExistingRunnerMovementBehavior.Hold, 0,
            PlateAppearanceCompletionBehavior.Pending,
            RequiresExistingRunner: true,
            RequiresExplicitOutcomeConfirmation: true,
            RequiredPlayOuts: 3,
            MinimumParticipants: 3),
        new(BatterResult.SacrificeFly, RunnerDestination.Out,
            ExistingRunnerMovementBehavior.Hold, 0,
            PlateAppearanceCompletionBehavior.Pending,
            RequiresExistingRunner: true,
            MaximumCurrentOuts: 1,
            RequiredOccupiedStartingLocation: RunnerStartingLocation.Third,
            AllowedBatterDestinations: [RunnerDestination.Out]),
        new(BatterResult.Out, RunnerDestination.Out,
            ExistingRunnerMovementBehavior.Hold, 0,
            PlateAppearanceCompletionBehavior.ContextualOut,
            IncludesBatterMovement: false),
        new(BatterResult.Strikeout, RunnerDestination.Out,
            ExistingRunnerMovementBehavior.Hold, 0,
            PlateAppearanceCompletionBehavior.Immediate,
            IncludesBatterMovement: false),
    ];

    public static PlateAppearanceResultDefinition Get(BatterResult result) =>
        Definitions.FirstOrDefault(item => item.Result == result)
        ?? throw new ArgumentOutOfRangeException(nameof(result));

    public static bool TryGet(
        BatterResult result,
        out PlateAppearanceResultDefinition? definition)
    {
        definition = Definitions.FirstOrDefault(item => item.Result == result);
        return definition is not null;
    }

    public static IReadOnlyList<BatterResult> GetSelectableResults(
        int existingRunnerCount,
        int currentOuts = 0,
        bool isHomeRunLegal = true) => Definitions
        .Where(item => !item.RequiresExistingRunner || existingRunnerCount > 0)
        .Where(item => existingRunnerCount + 1 >= item.MinimumParticipants)
        .Where(item => item.RequiredOccupiedStartingLocation is null)
        .Where(item => currentOuts <= item.MaximumCurrentOuts)
        .Where(item => item.RequiredPlayOuts == 0
            || item.RequiredPlayOuts <= 3 - currentOuts)
        .Where(item => isHomeRunLegal
            ? item.Result != BatterResult.DeadBallOut
            : item.Result != BatterResult.HomeRun)
        .Select(item => item.Result)
        .ToArray();

    public static IReadOnlyList<BatterResult> GetSelectableResults(
        IReadOnlySet<RunnerStartingLocation> occupiedStartingLocations,
        int currentOuts = 0,
        bool isHomeRunLegal = true) => Definitions
        .Where(item => !item.RequiresExistingRunner
            || occupiedStartingLocations.Count > 0)
        .Where(item => occupiedStartingLocations.Count + 1
            >= item.MinimumParticipants)
        .Where(item => item.RequiredOccupiedStartingLocation is null
            || occupiedStartingLocations.Contains(
                item.RequiredOccupiedStartingLocation.Value))
        .Where(item => currentOuts <= item.MaximumCurrentOuts)
        .Where(item => item.RequiredPlayOuts == 0
            || item.RequiredPlayOuts <= 3 - currentOuts)
        .Where(item => isHomeRunLegal
            ? item.Result != BatterResult.DeadBallOut
            : item.Result != BatterResult.HomeRun)
        .Select(item => item.Result)
        .ToArray();

    public static IReadOnlyList<BatterResult> GetSelectableResults(
        bool hasExistingRunners) => GetSelectableResults(hasExistingRunners ? 1 : 0, 0);
}
