namespace SoftballScorekeeper.Core.Models;

public static class HomeRunAvailabilityEvaluator
{
    public static bool IsHomeRunLegal(
        int homeRunLimit,
        bool isOneUpEnabled,
        int battingTeamHomeRuns,
        int opponentHomeRuns)
    {
        if (homeRunLimit < 0 || battingTeamHomeRuns < 0 || opponentHomeRuns < 0)
            throw new ArgumentOutOfRangeException(
                nameof(homeRunLimit),
                "Home Run availability values cannot be negative."
            );

        return battingTeamHomeRuns < homeRunLimit
            || isOneUpEnabled
                && opponentHomeRuns >= homeRunLimit
                && battingTeamHomeRuns <= opponentHomeRuns;
    }
}
