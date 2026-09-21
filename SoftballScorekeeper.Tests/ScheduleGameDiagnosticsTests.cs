using System.Xml.Linq;

namespace SoftballScorekeeper.Tests;

public sealed class ScheduleGameDiagnosticsTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")
    );

    [Fact]
    public void Add_game_maps_each_repository_failure_to_a_specific_resource()
    {
        var source = ReadText("SoftballScorekeeper", "ViewModels", "AddGameViewModel.cs");

        Assert.Contains(
            "CreateGamesFailure.DuplicateSchedule => AppResources.DuplicateGameSchedule",
            source
        );
        Assert.Contains(
            "CreateGamesFailure.InvalidTeamOrSeason =>",
            source
        );
        Assert.Contains("AppResources.GameCreationInvalidTeamOrSeason", source);
        Assert.Contains(
            "CreateGamesFailure.TeamYearMismatch => AppResources.GameCreationTeamYearMismatch",
            source
        );
        Assert.Contains(
            "CreateGamesFailure.TeamYearFinalized => AppResources.GameCreationTeamYearFinalized",
            source
        );
        Assert.Contains(
            "CreateGamesFailure.InvalidRequest => AppResources.GameCreationInvalidRequest",
            source
        );
        Assert.Contains(
            "CreateGamesFailure.Persistence => AppResources.GameCreationPersistenceFailed",
            source
        );
        Assert.Contains("_ => AppResources.GameCreationUnexpectedError", source);
    }

    [Fact]
    public void Add_game_logs_failure_context_and_handles_unexpected_exceptions_safely()
    {
        var source = ReadText("SoftballScorekeeper", "ViewModels", "AddGameViewModel.cs");

        Assert.Contains("ILogger<AddGameViewModel>", source);
        Assert.Contains("logger.LogWarning(", source);
        Assert.Contains("{FailureReason}", source);
        Assert.Contains("{TeamId}", source);
        Assert.Contains("{SeasonId}", source);
        Assert.Contains("{TeamYear}", source);
        Assert.Contains("{ScheduledDate}", source);
        Assert.Contains("{ScheduledTime}", source);
        Assert.Contains("{AddSecondGame}", source);
        Assert.Contains("{SecondGameTime}", source);
        Assert.Contains("{ExceptionType}", source);
        Assert.Contains("{ExceptionMessage}", source);
        Assert.Contains("result.ExceptionType", source);
        Assert.Contains("result.ExceptionMessage", source);
        Assert.Contains("catch (Exception exception)", source);
        Assert.Contains("logger.LogError(", source);
        Assert.Contains("exception,", source);
        Assert.Contains("ValidationMessage = AppResources.GameCreationUnexpectedError;", source);
    }

    [Fact]
    public void Schedule_failure_messages_are_resource_backed_and_do_not_expose_diagnostics()
    {
        var resources = XDocument.Load(
            ReadPath("SoftballScorekeeper", "Resources", "Strings", "AppResources.resx")
        );
        var values = resources.Root!.Elements("data").ToDictionary(
            item => item.Attribute("name")!.Value,
            item => item.Element("value")!.Value
        );

        Assert.Equal(
            "A Game is already scheduled for this Team, Season, date, and time. Change the date or time and try again.",
            values["DuplicateGameSchedule"]
        );
        Assert.Equal(
            "The selected Season is no longer valid for this Team. Reopen the page and select the Season again.",
            values["GameCreationInvalidTeamOrSeason"]
        );
        Assert.Equal(
            "The selected Season does not match this Team Year. Reopen the page and select a current Season.",
            values["GameCreationTeamYearMismatch"]
        );
        Assert.Equal(
            "This Team Year is finalized and Games can no longer be added.",
            values["GameCreationTeamYearFinalized"]
        );
        Assert.Equal(
            "The Game schedule contains invalid information. Review the date and times and try again.",
            values["GameCreationInvalidRequest"]
        );
        Assert.Equal(
            "The Game schedule could not be saved because of a database error. Try again.",
            values["GameCreationPersistenceFailed"]
        );
        Assert.Equal(
            "The Game schedule could not be saved because of an unexpected error.",
            values["GameCreationUnexpectedError"]
        );
        Assert.DoesNotContain(
            "stack",
            values["GameCreationUnexpectedError"],
            StringComparison.OrdinalIgnoreCase
        );
        Assert.DoesNotContain(
            "path",
            values["GameCreationUnexpectedError"],
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static string ReadText(params string[] segments) =>
        File.ReadAllText(ReadPath(segments));

    private static string ReadPath(params string[] segments) =>
        Path.Combine([RepositoryRoot, .. segments]);
}
