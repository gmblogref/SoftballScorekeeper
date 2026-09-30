using System.Globalization;
using SoftballScorekeeper.Core.Models;
using SoftballScorekeeper.Infrastructure.Data.Repositories;
using SoftballScorekeeper.Resources.Strings;

namespace SoftballScorekeeper.ViewModels;

public sealed class CompletedGameHalfInningViewModel
{
    public CompletedGameHalfInningViewModel(
        CompletedGameHalfInningGroup group, string teamName, string opponentName)
    {
        Heading = string.Format(CultureInfo.CurrentCulture,
            AppResources.GamePlaysHalfHeading,
            group.Half == CompletedGameHalf.Top
                ? AppResources.GamePlaysTopLabel : AppResources.GamePlaysBottomLabel,
            group.Inning,
            group.IsTeamOffense ? teamName : opponentName);
        Plays = group.Plays.Select(play => new CompletedGamePlayViewModel(
            play, teamName, opponentName)).ToArray();
        if (group.OpponentSummary is { } summary)
        {
            OpponentRuns = string.Format(CultureInfo.CurrentCulture,
                AppResources.GamePlaysOpponentRuns,
                FormatNullable(summary.Runs));
            OpponentHomeRuns = string.Format(CultureInfo.CurrentCulture,
                AppResources.GamePlaysOpponentHomeRuns,
                FormatNullable(summary.HomeRuns));
            OpponentTotalHomeRuns = string.Format(CultureInfo.CurrentCulture,
                AppResources.GamePlaysOpponentTotalHomeRuns, summary.TotalHomeRuns);
            OpponentScore = Score(teamName, summary.TeamScore,
                opponentName, summary.OpponentScore);
        }
        IsOpponentSummary = group.OpponentSummary is not null;
    }

    public string Heading { get; }
    public IReadOnlyList<CompletedGamePlayViewModel> Plays { get; }
    public bool IsOpponentSummary { get; }
    public string OpponentRuns { get; } = string.Empty;
    public string OpponentHomeRuns { get; } = string.Empty;
    public string OpponentTotalHomeRuns { get; } = string.Empty;
    public string OpponentScore { get; } = string.Empty;

    private static string FormatNullable(int? value) =>
        value?.ToString(CultureInfo.CurrentCulture) ?? AppResources.UndefinedBattingRate;

    internal static string Score(
        string teamName, int teamScore, string opponentName, int opponentScore) =>
        string.Format(CultureInfo.CurrentCulture, AppResources.GamePlaysScore,
            teamName, teamScore, opponentName, opponentScore);
}

public sealed class CompletedGamePlayViewModel
{
    public CompletedGamePlayViewModel(
        CompletedGamePlayRow play, string teamName, string opponentName)
    {
        BatterResultDisplay = string.Format(CultureInfo.CurrentCulture,
            AppResources.GamePlaysBatterResult, play.BatterName,
            FormatResult(play.Result));
        RunnerMovements = play.RunnerMovements
            .Where(IsRelevant)
            .Select(FormatMovement)
            .ToArray();
        Score = CompletedGameHalfInningViewModel.Score(
            teamName, play.TeamScore, opponentName, play.OpponentScore);
    }

    public string BatterResultDisplay { get; }
    public IReadOnlyList<string> RunnerMovements { get; }
    public string Score { get; }

    private static bool IsRelevant(CompletedRunnerMovementRow movement) =>
        movement.Destination == RunnerDestination.Out
        || movement.Destination == RunnerDestination.Scores
        || (int)movement.Destination != (int)movement.StartingLocation;

    private static string FormatMovement(CompletedRunnerMovementRow movement)
    {
        if (movement.Destination == RunnerDestination.Scores)
            return movement.RunCounts == false
                ? string.Format(CultureInfo.CurrentCulture,
                    AppResources.GamePlaysRunDidNotCount, movement.PlayerName)
                : string.Format(CultureInfo.CurrentCulture,
                    AppResources.GamePlaysScored, movement.PlayerName);
        if (movement.Destination == RunnerDestination.Out)
        {
            if (movement.AttemptedDestination is { } attempted)
                return string.Format(CultureInfo.CurrentCulture,
                    AppResources.GamePlaysOutAttempting, movement.PlayerName,
                    FormatDestination(attempted));
            var outAt = movement.StartingLocation switch
            {
                RunnerStartingLocation.Home => RunnerDestination.First,
                RunnerStartingLocation.First => RunnerDestination.Second,
                RunnerStartingLocation.Second => RunnerDestination.Third,
                RunnerStartingLocation.Third => RunnerDestination.Scores,
                _ => RunnerDestination.First,
            };
            return string.Format(CultureInfo.CurrentCulture,
                AppResources.GamePlaysOutAt, movement.PlayerName,
                FormatDestination(outAt));
        }
        return string.Format(CultureInfo.CurrentCulture,
            movement.StartingLocation == RunnerStartingLocation.Home
                ? AppResources.GamePlaysSafeAt : AppResources.GamePlaysAdvancedTo,
            movement.PlayerName, FormatDestination(movement.Destination));
    }

    private static string FormatDestination(RunnerDestination destination) => destination switch
    {
        RunnerDestination.First => AppResources.GamePlaysFirstBase,
        RunnerDestination.Second => AppResources.GamePlaysSecondBase,
        RunnerDestination.Third => AppResources.GamePlaysThirdBase,
        RunnerDestination.Scores => AppResources.GamePlaysHome,
        _ => string.Empty,
    };

    private static string FormatResult(BatterResult? result) => result switch
    {
        BatterResult.Single => AppResources.SingleResultLabel,
        BatterResult.Double => AppResources.DoubleResultLabel,
        BatterResult.Triple => AppResources.TripleResultLabel,
        BatterResult.HomeRun => AppResources.HomeRunResultLabel,
        BatterResult.InTheParkHomeRun => AppResources.InTheParkHomeRunResultLabel,
        BatterResult.Out => AppResources.OutResultLabel,
        BatterResult.Walk => AppResources.WalkResultLabel,
        BatterResult.FielderChoice => AppResources.FielderChoiceResultLabel,
        BatterResult.DoublePlay => AppResources.DoublePlayResultLabel,
        BatterResult.TriplePlay => AppResources.TriplePlayResultLabel,
        BatterResult.SacrificeFly => AppResources.SacrificeFlyResultLabel,
        BatterResult.Strikeout => AppResources.StrikeoutResultLabel,
        BatterResult.DeadBallOut => AppResources.DeadBallOutResultLabel,
        _ => AppResources.UndefinedBattingRate,
    };
}
