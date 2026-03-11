namespace OpsCopilot.Api.Kb;

public sealed class KbRetrievalOptions
{
    public const string SectionName = "KbRetrieval";

    /// <summary>
    /// Number of chunks to retrieve from Azure AI Search for each question.
    /// </summary>
    public int TopK { get; set; } = 3;

    /// <summary>
    /// Number of citations to include in the final response.
    /// </summary>
    public int CitationCount { get; set; } = 3;
}

