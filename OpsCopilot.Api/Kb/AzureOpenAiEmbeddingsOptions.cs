namespace OpsCopilot.Api.Kb;

public sealed class AzureOpenAiEmbeddingsOptions
{
    public const string SectionName = "AzureOpenAiEmbeddings";

    /// <summary>
    /// Azure OpenAI endpoint, for example: https://my-resource.openai.azure.com/
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Azure OpenAI API key used for embeddings requests.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Deployment name for the embeddings model.
    /// </summary>
    public string? Deployment { get; set; }

    /// <summary>
    /// Azure OpenAI API version used for embeddings requests.
    /// </summary>
    public string? ApiVersion { get; set; }
}

