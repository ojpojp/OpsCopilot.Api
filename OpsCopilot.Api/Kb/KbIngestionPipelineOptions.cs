namespace OpsCopilot.Api.Kb;

public sealed class KbIngestionPipelineOptions
{
    public const string SectionName = "KbIngestionPipeline";

    /// <summary>
    /// Maximum number of chunks processed in parallel for embedding generation.
    /// Keep this small to avoid rate-limit spikes during initial ingestion.
    /// </summary>
    public int MaxConcurrentEmbeddings { get; set; } = 3;

    /// <summary>
    /// Maximum number of retry attempts after the initial embeddings/search request.
    /// Value 1 means "retry once".
    /// </summary>
    public int RetryCount { get; set; } = 1;
}

