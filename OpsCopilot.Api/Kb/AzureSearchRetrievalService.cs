using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace OpsCopilot.Api.Kb;

public sealed class AzureSearchRetrievalService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AzureSearchOptions _options;
    private readonly ILogger<AzureSearchRetrievalService> _logger;

    public AzureSearchRetrievalService(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureSearchOptions> options,
        ILogger<AzureSearchRetrievalService> logger)
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

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        IReadOnlyList<float> questionEmbedding,
        int topK,
        CancellationToken cancellationToken)
    {
        var missing = GetMissingConfiguration();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Missing Azure Search configuration: {string.Join(", ", missing)}");
        }

        var requestUri = new Uri(
            $"{_options.Endpoint!.TrimEnd('/')}/indexes/{Uri.EscapeDataString(_options.IndexName!)}/docs/search?api-version={Uri.EscapeDataString(_options.ApiVersion)}",
            UriKind.Absolute);

        var payload = new SearchRequest(
            Count: true,
            Select: "id,docId,title,sourcePath,chunkIndex,section,content",
            VectorQueries:
            [
                new VectorQuery(
                    Kind: "vector",
                    Vector: questionEmbedding,
                    Fields: "contentVector",
                    K: topK)
            ]);

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
                "Azure Search retrieval failed status={StatusCode} index={IndexName}",
                (int)response.StatusCode,
                _options.IndexName);

            throw new InvalidOperationException(TryExtractErrorMessage(responseBody)
                ?? $"Azure Search retrieval failed with status {(int)response.StatusCode}");
        }

        return ParseResults(responseBody);
    }

    private static IReadOnlyList<RetrievedChunk> ParseResults(string json)
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Azure Search retrieval response did not contain a value array.");
        }

        var results = new List<RetrievedChunk>();
        foreach (var item in value.EnumerateArray())
        {
            var id = item.GetProperty("id").GetString() ?? string.Empty;
            var docId = item.GetProperty("docId").GetString() ?? string.Empty;
            var title = item.GetProperty("title").GetString() ?? string.Empty;
            var sourcePath = item.GetProperty("sourcePath").GetString() ?? string.Empty;
            var chunkIndex = item.GetProperty("chunkIndex").GetInt32();
            var section = item.TryGetProperty("section", out var sectionElement) && sectionElement.ValueKind == JsonValueKind.String
                ? sectionElement.GetString()
                : null;
            var content = item.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.String
                ? contentElement.GetString() ?? string.Empty
                : string.Empty;
            var score = item.TryGetProperty("@search.score", out var scoreElement) && scoreElement.ValueKind == JsonValueKind.Number
                ? scoreElement.GetDouble()
                : 0d;

            results.Add(new RetrievedChunk(id, docId, title, sourcePath, chunkIndex, section, content, score));
        }

        return results;
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

    private sealed record SearchRequest(
        [property: JsonPropertyName("count")] bool Count,
        [property: JsonPropertyName("select")] string Select,
        [property: JsonPropertyName("vectorQueries")] IReadOnlyList<VectorQuery> VectorQueries);

    private sealed record VectorQuery(
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("vector")] IReadOnlyList<float> Vector,
        [property: JsonPropertyName("fields")] string Fields,
        [property: JsonPropertyName("k")] int K);
}

public sealed record RetrievedChunk(
    string ChunkId,
    string DocId,
    string Title,
    string SourcePath,
    int ChunkIndex,
    string? Section,
    string Content,
    double Score);
