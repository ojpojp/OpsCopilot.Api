namespace OpsCopilot.Api.Kb;

public sealed class KbChunkingOptions
{
    /// <summary>
    /// Configuration section name for chunking options.
    /// </summary>
    public const string SectionName = "KbChunking";

    /// <summary>
    /// Target maximum chunk size in characters. Chunks are packed up to (but not exceeding) this size.
    /// </summary>
    public int ChunkSizeChars { get; set; } = 2000;

    /// <summary>
    /// Number of trailing characters from the previous chunk to prepend to the next chunk (simple overlap).
    /// </summary>
    public int ChunkOverlapChars { get; set; } = 200;
}
