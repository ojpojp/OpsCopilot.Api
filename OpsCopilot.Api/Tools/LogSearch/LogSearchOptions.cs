namespace OpsCopilot.Api.Tools.LogSearch;

public sealed class LogSearchOptions
{
    public const string SectionName = "LogSearch";

    /// <summary>
    /// Directory that contains mock log fixture JSON files.
    /// </summary>
    public string FixturesDirectory { get; set; } = "../fixtures/logs";

    /// <summary>
    /// Maximum allowed search time window in hours.
    /// </summary>
    public int MaxTimeRangeHours { get; set; } = 24;

    /// <summary>
    /// Maximum number of events a caller can request.
    /// </summary>
    public int MaxResults { get; set; } = 50;

    /// <summary>
    /// Maximum query length in characters.
    /// </summary>
    public int MaxQueryLength { get; set; } = 200;
}
