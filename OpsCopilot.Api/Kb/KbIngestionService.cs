using System.Security.Cryptography;
using System.Text;

namespace OpsCopilot.Api.Kb;

public sealed class KbIngestionService
{
    private readonly MarkdownChunker _chunker;

    public KbIngestionService(MarkdownChunker chunker)
    {
        _chunker = chunker;
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
        var chunks = new List<KbChunkDocument>();
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
                    chunks.Add(new KbChunkDocument(
                        DocId: docId,
                        Title: title,
                        SourcePath: filePath,
                        ChunkId: $"{docId}:{index:D4}",
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

        return new KbIngestionResult(
            DocumentsIngested: documentsIngested,
            ChunksCreated: chunks.Count,
            Failures: failures,
            Chunks: chunks);
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
}
