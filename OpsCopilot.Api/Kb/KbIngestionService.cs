using System.Security.Cryptography;
using System.Text;

namespace OpsCopilot.Api.Kb;

public sealed class KbIngestionService
{
    private readonly MarkdownChunker _chunker;
    private readonly AzureOpenAiEmbeddingService _embeddingService;
    private readonly AzureSearchIndexingService _searchIndexingService;
    private readonly KbIngestionPipelineOptions _pipelineOptions;

    public KbIngestionService(
        MarkdownChunker chunker,
        AzureOpenAiEmbeddingService embeddingService,
        AzureSearchIndexingService searchIndexingService,
        Microsoft.Extensions.Options.IOptions<KbIngestionPipelineOptions> pipelineOptions)
    {
        _chunker = chunker;
        _embeddingService = embeddingService;
        _searchIndexingService = searchIndexingService;
        _pipelineOptions = pipelineOptions.Value;
    }

    public async Task<KbIngestionResult> IngestDirectoryAsync(
        string sourceDirectory,
        KbChunkingOptions chunkingOptions,
        CancellationToken cancellationToken)
    {
        var markdownFiles = Directory
            .EnumerateFiles(sourceDirectory, "*.md", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var failures = new List<KbIngestionFailure>();
        var chunkInputs = new List<ChunkInput>();
        var documentsIngested = 0;

        foreach (var filePath in markdownFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var markdown = await File.ReadAllTextAsync(filePath, cancellationToken);
                var chunkPreviews = _chunker.Chunk(markdown, chunkingOptions);

                var fileInfo = new FileInfo(filePath);
                var title = ExtractTitle(markdown, fileInfo.Name);
                var docId = ComputeSha256(NormalizePath(filePath));

                for (var index = 0; index < chunkPreviews.Count; index++)
                {
                    var preview = chunkPreviews[index];
                    chunkInputs.Add(new ChunkInput(
                        DocId: docId,
                        Title: title,
                        SourcePath: filePath,
                        ChunkId: $"{docId}_{index:D4}",
                        ChunkIndex: preview.ChunkIndex,
                        Section: preview.Section,
                        Content: preview.Content,
                        ContentHash: ComputeSha256(preview.Content),
                        Tags: Array.Empty<string>(),
                        UpdatedAt: fileInfo.LastWriteTimeUtc));
                }

                documentsIngested++;
            }
            catch (Exception ex)
            {
                failures.Add(new KbIngestionFailure(filePath, ex.Message));
            }
        }

        var chunks = await BuildChunksWithEmbeddingsAsync(chunkInputs, failures, cancellationToken);

        var indexedDocuments = 0;
        if (chunks.Count > 0)
        {
            try
            {
                indexedDocuments = await ExecuteWithRetryAsync(
                    () => _searchIndexingService.UploadChunksAsync(chunks, cancellationToken),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                failures.Add(new KbIngestionFailure(sourceDirectory, $"Azure Search indexing failed: {ex.Message}"));
            }
        }

        return new KbIngestionResult(
            DocumentsIngested: documentsIngested,
            ChunksCreated: chunks.Count,
            EmbeddingsCreated: chunks.Count,
            IndexedDocuments: indexedDocuments,
            Failures: failures,
            Chunks: chunks);
    }

    private async Task<List<KbChunkDocument>> BuildChunksWithEmbeddingsAsync(
        IReadOnlyList<ChunkInput> chunkInputs,
        List<KbIngestionFailure> failures,
        CancellationToken cancellationToken)
    {
        if (chunkInputs.Count == 0)
        {
            return new List<KbChunkDocument>();
        }

        var degreeOfParallelism = Math.Clamp(_pipelineOptions.MaxConcurrentEmbeddings, 1, 4);
        using var semaphore = new SemaphoreSlim(degreeOfParallelism, degreeOfParallelism);

        var results = new KbChunkDocument?[chunkInputs.Count];
        var tasks = chunkInputs.Select((input, index) => ProcessChunkAsync(input, index, results, failures, semaphore, cancellationToken));

        await Task.WhenAll(tasks);

        return results.Where(static chunk => chunk is not null).Select(static chunk => chunk!).ToList();
    }

    private async Task ProcessChunkAsync(
        ChunkInput input,
        int index,
        KbChunkDocument?[] results,
        List<KbIngestionFailure> failures,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var embedding = await ExecuteWithRetryAsync(
                () => _embeddingService.GenerateEmbeddingAsync(input.Content, cancellationToken),
                cancellationToken);

            results[index] = new KbChunkDocument(
                DocId: input.DocId,
                Title: input.Title,
                SourcePath: input.SourcePath,
                ChunkId: input.ChunkId,
                ChunkIndex: input.ChunkIndex,
                Section: input.Section,
                Content: input.Content,
                ContentHash: input.ContentHash,
                Tags: input.Tags,
                UpdatedAt: input.UpdatedAt,
                Embedding: embedding);
        }
        catch (Exception ex)
        {
            lock (failures)
            {
                failures.Add(new KbIngestionFailure(input.SourcePath, $"Chunk {input.ChunkId} embedding failed: {ex.Message}"));
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        var attempts = Math.Max(_pipelineOptions.RetryCount, 0) + 1;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await action();
            }
            catch (Exception ex) when (attempt < attempts)
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
            }
            catch (Exception ex)
            {
                lastException = ex;
                break;
            }
        }

        throw lastException ?? new InvalidOperationException("The operation failed without an exception.");
    }

    private static string ExtractTitle(string markdown, string fallbackFileName)
    {
        foreach (var rawLine in markdown.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                return line[2..].Trim();
            }
        }

        return Path.GetFileNameWithoutExtension(fallbackFileName);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record ChunkInput(
        string DocId,
        string Title,
        string SourcePath,
        string ChunkId,
        int ChunkIndex,
        string? Section,
        string Content,
        string ContentHash,
        IReadOnlyList<string> Tags,
        DateTimeOffset UpdatedAt);
}
