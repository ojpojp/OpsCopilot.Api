namespace OpsCopilot.Api.Kb;

public sealed record KbChunkDocument(
    string DocId,
    string Title,
    string SourcePath,
    string ChunkId,
    int ChunkIndex,
    string? Section,
    string Content,
    string ContentHash,
    IReadOnlyList<string> Tags,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<float>? Embedding = null);

public sealed record KbIngestionFailure(string Path, string Reason);

public sealed record KbIngestionResult(
    int DocumentsIngested,
    int ChunksCreated,
    int EmbeddingsCreated,
    int IndexedDocuments,
    IReadOnlyList<KbIngestionFailure> Failures,
    IReadOnlyList<KbChunkDocument> Chunks);
