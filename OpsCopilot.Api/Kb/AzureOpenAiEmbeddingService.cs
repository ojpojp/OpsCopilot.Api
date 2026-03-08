using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace OpsCopilot.Api.Kb;

public sealed class AzureOpenAiEmbeddingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AzureOpenAiEmbeddingsOptions _options;
    private readonly ILogger<AzureOpenAiEmbeddingService> _logger;

    public AzureOpenAiEmbeddingService(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAiEmbeddingsOptions> options,
        ILogger<AzureOpenAiEmbeddingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlyList<string> GetMissingConfiguration()
    {
        var missing = new List<string>(capacity: 4);

        if (string.IsNullOrWhiteSpace(_options.Endpoint)) missing.Add("AZURE_OPENAI_ENDPOINT");
        if (string.IsNullOrWhiteSpace(_options.ApiKey)) missing.Add("AZURE_OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(_options.Deployment)) missing.Add("AZURE_OPENAI_EMBEDDING_DEPLOYMENT");
        if (string.IsNullOrWhiteSpace(_options.ApiVersion)) missing.Add("AZURE_OPENAI_API_VERSION");

        return missing;
    }

    public async Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken)
    {
        var missing = GetMissingConfiguration();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Missing embeddings configuration: {string.Join(", ", missing)}");
        }

        var requestUri = new Uri(
            $"{_options.Endpoint!.TrimEnd('/')}/openai/deployments/{Uri.EscapeDataString(_options.Deployment!)}/embeddings?api-version={Uri.EscapeDataString(_options.ApiVersion!)}",
            UriKind.Absolute);

        var payload = new
        {
            input,
        };

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
                "Embedding request failed status={StatusCode} deployment={Deployment}",
                (int)response.StatusCode,
                _options.Deployment);

            throw new InvalidOperationException(TryExtractErrorMessage(responseBody)
                ?? $"Azure OpenAI embeddings request failed with status {(int)response.StatusCode}");
        }

        return ParseEmbedding(responseBody);
    }

    private static IReadOnlyList<float> ParseEmbedding(string json)
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Embeddings response did not contain a data array.");
        }

        var first = data[0];
        if (!first.TryGetProperty("embedding", out var embedding) || embedding.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Embeddings response did not contain an embedding vector.");
        }

        var vector = new float[embedding.GetArrayLength()];
        var index = 0;
        foreach (var item in embedding.EnumerateArray())
        {
            vector[index++] = item.GetSingle();
        }

        return vector;
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
}
