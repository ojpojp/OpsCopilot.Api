using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace OpsCopilot.Api.Kb;

public sealed class AzureSearchIndexingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AzureSearchOptions _options;
    private readonly ILogger<AzureSearchIndexingService> _logger;

    public AzureSearchIndexingService(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureSearchOptions> options,
        ILogger<AzureSearchIndexingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlyList<string> GetMissingConfiguration()
    {
        var missing = new List<string>(capacity: 3);

        if (string.IsNullOrWhiteSpace(_options.Endpoint)) missing.Add("AZURE_SEARCH_ENDPOINT");
        if (string.IsNullOrWhiteSpace(_options.ApiKey)) missing.Add("AZURE_SEARCH_API_KEY");
        if (string.IsNullOrWhiteSpace(_options.IndexName)) missing.Add("AZURE_SEARCH_INDEX");

        return missing;
    }

    public async Task<int> UploadChunksAsync(
        IReadOnlyList<KbChunkDocument> chunks,
        CancellationToken cancellationToken)
    {
        var missing = GetMissingConfiguration();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Missing Azure Search configuration: {string.Join(", ", missing)}");
        }

        if (chunks.Count == 0)
        {
            return 0;
        }

        var requestUri = new Uri(
            $"{_options.Endpoint!.TrimEnd('/')}/indexes/{Uri.EscapeDataString(_options.IndexName!)}/docs/index?api-version={Uri.EscapeDataString(_options.ApiVersion)}",
            UriKind.Absolute);

        var actions = chunks.Select(chunk => new SearchIndexAction(
            "upload",
            chunk.ChunkId,
            chunk.DocId,
            chunk.Title,
            chunk.SourcePath,
            chunk.ChunkIndex,
            chunk.Section,
            chunk.Content,
            chunk.ContentHash,
            chunk.Tags,
            chunk.UpdatedAt,
            chunk.Embedding)).ToArray();

        var payload = new SearchIndexRequest(actions);

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Add("api-key", _options.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Azure Search indexing failed status={StatusCode} index={IndexName}",
                (int)response.StatusCode,
                _options.IndexName);

            throw new InvalidOperationException(TryExtractErrorMessage(responseBody)
                ?? $"Azure Search indexing failed with status {(int)response.StatusCode}");
        }

        return ParseSuccessfulCount(responseBody);
    }

    private static int ParseSuccessfulCount(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Azure Search response did not contain a value array.");
        }

        var successCount = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (item.TryGetProperty("status", out var status)
                && (status.ValueKind == JsonValueKind.True || status.ValueKind == JsonValueKind.False)
                && status.GetBoolean())
            {
                successCount++;
            }
        }

        return successCount;
    }

    private static string? TryExtractErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!error.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return message.GetString();
        }
        catch
        {
            return null;
        }
    }

    private sealed record SearchIndexRequest(
        [property: JsonPropertyName("value")] IReadOnlyList<SearchIndexAction> Value);

    private sealed record SearchIndexAction(
        [property: JsonPropertyName("@search.action")] string SearchAction,
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("docId")] string DocId,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("sourcePath")] string SourcePath,
        [property: JsonPropertyName("chunkIndex")] int ChunkIndex,
        [property: JsonPropertyName("section")] string? Section,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("contentHash")] string ContentHash,
        [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
        [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt,
        [property: JsonPropertyName("contentVector")] IReadOnlyList<float>? ContentVector);
}
