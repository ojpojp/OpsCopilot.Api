namespace OpsCopilot.Api.Kb;

public sealed class AzureSearchOptions
{
    public const string SectionName = "AzureSearch";

    /// <summary>
    /// Azure AI Search endpoint, for example: https://my-search.search.windows.net
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Admin API key for indexing documents.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Target index name.
    /// </summary>
    public string? IndexName { get; set; }

    /// <summary>
    /// Azure Search API version used for indexing.
    /// </summary>
    public string ApiVersion { get; set; } = "2024-07-01";
}

