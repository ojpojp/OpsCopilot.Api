namespace OpsCopilot.Api.Kb;

public sealed class KbIngestionOptions
{
    public const string SectionName = "KbIngestion";

    /// <summary>
    /// Local directory containing Markdown runbooks to ingest.
    /// </summary>
    public string? SourceDirectory { get; set; }
}

