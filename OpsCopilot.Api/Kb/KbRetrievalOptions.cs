namespace OpsCopilot.Api.Kb;

public sealed class KbRetrievalOptions
{
    public const string SectionName = "KbRetrieval";

    /// <summary>
    /// Retrieval mode: vector, keyword, or hybrid.
    /// </summary>
    public string Mode { get; set; } = "vector";

    /// <summary>
    /// Number of chunks to retrieve from Azure AI Search for each question.
    /// </summary>
    public int TopK { get; set; } = 3;

    /// <summary>
    /// Weight applied to normalized keyword scores during hybrid ranking.
    /// </summary>
    public double KeywordWeight { get; set; } = 0.5;

    /// <summary>
    /// Weight applied to normalized vector scores during hybrid ranking.
    /// </summary>
    public double VectorWeight { get; set; } = 0.5;

    /// <summary>
    /// Number of citations to include in the final response.
    /// </summary>
    public int CitationCount { get; set; } = 3;
}
