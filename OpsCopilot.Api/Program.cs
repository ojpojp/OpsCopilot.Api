using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using OpsCopilot.Api.Kb;
using OpsCopilot.Api.Tools.LogSearch;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

var logDirectory = builder.Configuration["LOG_DIR"];
if (string.IsNullOrWhiteSpace(logDirectory))
{
    logDirectory = Path.Combine(builder.Environment.ContentRootPath, "logs");
}

Directory.CreateDirectory(logDirectory);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(logDirectory, "opscopilot-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.Configure<KbChunkingOptions>(builder.Configuration.GetSection(KbChunkingOptions.SectionName));
builder.Services.Configure<KbIngestionOptions>(builder.Configuration.GetSection(KbIngestionOptions.SectionName));
builder.Services.Configure<AzureOpenAiEmbeddingsOptions>(options =>
{
    options.Endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"];
    options.ApiKey = builder.Configuration["AZURE_OPENAI_API_KEY"];
    options.Deployment = builder.Configuration["AZURE_OPENAI_EMBEDDING_DEPLOYMENT"];
    options.ApiVersion = builder.Configuration["AZURE_OPENAI_EMBEDDING_API_VERSION"];
});
builder.Services.Configure<AzureSearchOptions>(options =>
{
    options.Endpoint = builder.Configuration["AZURE_SEARCH_ENDPOINT"];
    options.ApiKey = builder.Configuration["AZURE_SEARCH_API_KEY"];
    options.IndexName = builder.Configuration["AZURE_SEARCH_INDEX"];
    options.ApiVersion = builder.Configuration["AZURE_SEARCH_API_VERSION"] ?? options.ApiVersion;
});
builder.Services.Configure<KbIngestionPipelineOptions>(builder.Configuration.GetSection(KbIngestionPipelineOptions.SectionName));
builder.Services.Configure<KbRetrievalOptions>(builder.Configuration.GetSection(KbRetrievalOptions.SectionName));
builder.Services.Configure<LogSearchOptions>(builder.Configuration.GetSection(LogSearchOptions.SectionName));
builder.Services.AddSingleton<MarkdownChunker>();
builder.Services.AddSingleton<AzureOpenAiEmbeddingService>();
builder.Services.AddSingleton<AzureSearchIndexingService>();
builder.Services.AddSingleton<AzureSearchRetrievalService>();
builder.Services.AddSingleton<HybridRetrievalService>();
builder.Services.AddSingleton<KbIngestionService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

static string GetServiceVersion()
{
    var assembly = typeof(Program).Assembly;

    var informationalVersion = assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion;

    if (!string.IsNullOrWhiteSpace(informationalVersion))
    {
        return informationalVersion;
    }

    return assembly.GetName().Version?.ToString() ?? "unknown";
}

var healthVersion = GetServiceVersion();

app.MapGet("/health", (IHostEnvironment env) =>
{
    return Results.Ok(new
    {
        status = "ok",
        service = env.ApplicationName,
        utcTime = DateTimeOffset.UtcNow,
        version = healthVersion,
    });
})
.WithName("Health");

static string? TryExtractAzureOpenAiAssistantContent(string json)
{
    try
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        if (choices.GetArrayLength() < 1)
        {
            return null;
        }

        var first = choices[0];
        if (!first.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return content.GetString();
    }
    catch
    {
        return null;
    }
}

static string? TryExtractAzureOpenAiErrorMessage(string json)
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

app.MapPost("/ask", async (
    AskRequest request,
    IConfiguration config,
    Microsoft.Extensions.Options.IOptions<KbRetrievalOptions> retrievalOptions,
    AzureOpenAiEmbeddingService embeddingService,
    AzureSearchRetrievalService retrievalService,
    HybridRetrievalService hybridRetrievalService,
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var requestId = Guid.NewGuid();
    var stopwatch = Stopwatch.StartNew();

    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.BadRequest(new
        {
            error = "invalid_request",
            message = "question is required",
        });
    }

    var endpoint = config["AZURE_OPENAI_ENDPOINT"];
    var apiKey = config["AZURE_OPENAI_API_KEY"];
    var deployment = config["AZURE_OPENAI_DEPLOYMENT"];
    var apiVersion = config["AZURE_OPENAI_API_VERSION"];

    var missing = new List<string>(capacity: 4);
    if (string.IsNullOrWhiteSpace(endpoint)) missing.Add("AZURE_OPENAI_ENDPOINT");
    if (string.IsNullOrWhiteSpace(apiKey)) missing.Add("AZURE_OPENAI_API_KEY");
    if (string.IsNullOrWhiteSpace(deployment)) missing.Add("AZURE_OPENAI_DEPLOYMENT");
    if (string.IsNullOrWhiteSpace(apiVersion)) missing.Add("AZURE_OPENAI_API_VERSION");

    if (missing.Count > 0)
    {
        return Results.BadRequest(new
        {
            error = "missing_env",
            missing,
        });
    }

    var missingEmbeddingsConfig = embeddingService.GetMissingConfiguration();
    if (missingEmbeddingsConfig.Count > 0)
    {
        return Results.BadRequest(new
        {
            error = "missing_config",
            missing = missingEmbeddingsConfig,
        });
    }

    var missingSearchConfig = retrievalService.GetMissingConfiguration();
    if (missingSearchConfig.Count > 0)
    {
        return Results.BadRequest(new
        {
            error = "missing_config",
            missing = missingSearchConfig,
        });
    }

    IReadOnlyList<float> questionEmbedding;
    HybridRetrievalResult retrievalResult;
    try
    {
        questionEmbedding = await embeddingService.GenerateEmbeddingAsync(request.Question, cancellationToken);
        retrievalResult = await hybridRetrievalService.RetrieveAsync(
            request.Question,
            questionEmbedding,
            cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        return Results.Problem(statusCode: StatusCodes.Status499ClientClosedRequest, title: "Request canceled");
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        logger.LogError(ex, "Ask retrieval {RequestId} failed after {LatencyMs}ms", requestId, stopwatch.ElapsedMilliseconds);
        return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Retrieval failed");
    }

    var retrievedChunks = retrievalResult.Hits;
    if (retrievedChunks.Count == 0)
    {
        stopwatch.Stop();
        logger.LogInformation(
            "Ask request {RequestId} no retrieval hits latencyMs={LatencyMs} mode={Mode}",
            requestId,
            stopwatch.ElapsedMilliseconds,
            retrievalResult.Mode);

        return Results.Ok(new AskResponse(
            requestId,
            stopwatch.ElapsedMilliseconds,
            "I could not find enough evidence in the knowledge base to answer this question.",
            Array.Empty<AskCitation>(),
            new AskRetrieval(
                retrievalOptions.Value.TopK,
                retrievalResult.Mode,
                0,
                Array.Empty<string>())));
    }

    var requestUri = new Uri(
        $"{endpoint!.TrimEnd('/')}/openai/deployments/{Uri.EscapeDataString(deployment!)}/chat/completions?api-version={Uri.EscapeDataString(apiVersion!)}",
        UriKind.Absolute);

    var groundedContext = string.Join(
        "\n\n",
        retrievedChunks.Select(static (chunk, idx) =>
            $"[Doc {idx + 1}] title={chunk.Title}; sourcePath={chunk.SourcePath}; section={chunk.Section ?? "n/a"}; chunkIndex={chunk.ChunkIndex}\n{chunk.Content}"));

    var payload = new
    {
        messages = new[]
        {
            new
            {
                role = "system",
                content = "You are an ops copilot. Answer only using the provided knowledge base evidence. If the evidence is insufficient, say so clearly. Do not invent runbooks, incidents, or commands."
            },
            new
            {
                role = "user",
                content =
                    $"Question:\n{request.Question}\n\nKnowledge base evidence:\n{groundedContext}\n\nAnswer the question using only the evidence above. Keep the answer concise and operational."
            },
        },
        temperature = 0.2,
    };

    var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);
    httpRequest.Headers.Add("api-key", apiKey);
    httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    var client = httpClientFactory.CreateClient();

    HttpResponseMessage httpResponse;
    string responseBody;
    try
    {
        httpResponse = await client.SendAsync(httpRequest, cancellationToken);
        responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        return Results.Problem(statusCode: StatusCodes.Status499ClientClosedRequest, title: "Request canceled");
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        logger.LogError(ex, "Ask request {RequestId} failed after {LatencyMs}ms", requestId, stopwatch.ElapsedMilliseconds);

        return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Upstream request failed");
    }

    stopwatch.Stop();

    if (!httpResponse.IsSuccessStatusCode)
    {
        var upstreamMessage = TryExtractAzureOpenAiErrorMessage(responseBody);

        logger.LogWarning(
            "Ask request {RequestId} upstream failure status={StatusCode} latencyMs={LatencyMs} deployment={Deployment}",
            requestId,
            (int)httpResponse.StatusCode,
            stopwatch.ElapsedMilliseconds,
            deployment);

        return Results.Problem(
            statusCode: (int)httpResponse.StatusCode,
            title: "Azure OpenAI request failed",
            detail: upstreamMessage ?? "No upstream error message available");
    }

    var answer = TryExtractAzureOpenAiAssistantContent(responseBody);
    if (string.IsNullOrWhiteSpace(answer))
    {
        logger.LogWarning(
            "Ask request {RequestId} returned unexpected payload latencyMs={LatencyMs} deployment={Deployment}",
            requestId,
            stopwatch.ElapsedMilliseconds,
            deployment);

        return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Unexpected upstream response");
    }

    logger.LogInformation(
        "Ask request {RequestId} ok latencyMs={LatencyMs} deployment={Deployment} retrievalMode={RetrievalMode} retrievalHits={RetrievalHits}",
        requestId,
        stopwatch.ElapsedMilliseconds,
        deployment,
        retrievalResult.Mode,
        retrievedChunks.Count);

    var citations = retrievedChunks
        .Take(Math.Max(retrievalOptions.Value.CitationCount, 1))
        .Select(static chunk => new AskCitation(
            chunk.DocId,
            chunk.ChunkId,
            chunk.Title,
            chunk.SourcePath,
            chunk.Section,
            chunk.ChunkIndex))
        .ToArray();

    return Results.Ok(new AskResponse(
        requestId,
        stopwatch.ElapsedMilliseconds,
        answer,
        citations,
        new AskRetrieval(
            retrievalOptions.Value.TopK,
            retrievalResult.Mode,
            retrievedChunks.Count,
            retrievedChunks.Take(3).Select(static chunk => chunk.DocId).ToArray())));
})
.WithName("Ask");

app.MapPost("/ingest-kb", async (
    Microsoft.Extensions.Options.IOptions<KbIngestionOptions> ingestionOptions,
    Microsoft.Extensions.Options.IOptions<KbChunkingOptions> chunkingOptions,
    AzureOpenAiEmbeddingService embeddingService,
    AzureSearchIndexingService searchIndexingService,
    KbIngestionService ingestionService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var sourceDirectory = ingestionOptions.Value.SourceDirectory;
    if (string.IsNullOrWhiteSpace(sourceDirectory))
    {
        return Results.BadRequest(new
        {
            error = "missing_config",
            message = "KbIngestion:SourceDirectory is required",
        });
    }

    if (!Directory.Exists(sourceDirectory))
    {
        return Results.BadRequest(new
        {
            error = "invalid_config",
            message = $"Source directory does not exist: {sourceDirectory}",
        });
    }

    var missingEmbeddingsConfig = embeddingService.GetMissingConfiguration();
    if (missingEmbeddingsConfig.Count > 0)
    {
        return Results.BadRequest(new
        {
            error = "missing_config",
            missing = missingEmbeddingsConfig,
        });
    }

    var missingSearchConfig = searchIndexingService.GetMissingConfiguration();
    if (missingSearchConfig.Count > 0)
    {
        return Results.BadRequest(new
        {
            error = "missing_config",
            missing = missingSearchConfig,
        });
    }

    var result = await ingestionService.IngestDirectoryAsync(
        sourceDirectory,
        chunkingOptions.Value,
        cancellationToken);

    logger.LogInformation(
        "KB ingest completed sourceDirectory={SourceDirectory} documentsIngested={DocumentsIngested} chunksCreated={ChunksCreated} failures={FailureCount}",
        sourceDirectory,
        result.DocumentsIngested,
        result.ChunksCreated,
        result.Failures.Count);

    return Results.Ok(new IngestKbResponse(
        result.DocumentsIngested,
        result.ChunksCreated,
        result.EmbeddingsCreated,
        result.IndexedDocuments,
        result.Failures));
})
.WithName("IngestKb");

if (app.Environment.IsDevelopment())
{
    app.MapPost("/debug/search-keyword", async (
        KeywordSearchRequest request,
        Microsoft.Extensions.Options.IOptions<KbRetrievalOptions> retrievalOptions,
        AzureSearchRetrievalService retrievalService,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Results.BadRequest(new
            {
                error = "invalid_request",
                message = "query is required",
            });
        }

        var missingSearchConfig = retrievalService.GetMissingConfiguration();
        if (missingSearchConfig.Count > 0)
        {
            return Results.BadRequest(new
            {
                error = "missing_config",
                missing = missingSearchConfig,
            });
        }

        var keywordHits = await retrievalService.SearchByKeywordAsync(
            request.Query,
            retrievalOptions.Value.TopK,
            cancellationToken);

        return Results.Ok(new
        {
            mode = "keyword",
            topK = retrievalOptions.Value.TopK,
            hits = keywordHits.Count,
            results = keywordHits.Select(static hit => new
            {
                hit.DocId,
                hit.ChunkId,
                hit.Score,
                hit.Title,
                hit.SourcePath,
                hit.ChunkIndex,
                hit.Section,
            }),
        });
    })
    .WithName("KeywordSearch");

    app.MapPost("/debug/search-hybrid", async (
        KeywordSearchRequest request,
        AzureOpenAiEmbeddingService embeddingService,
        AzureSearchRetrievalService retrievalService,
        HybridRetrievalService hybridRetrievalService,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Results.BadRequest(new
            {
                error = "invalid_request",
                message = "query is required",
            });
        }

        var missingEmbeddingsConfig = embeddingService.GetMissingConfiguration();
        if (missingEmbeddingsConfig.Count > 0)
        {
            return Results.BadRequest(new
            {
                error = "missing_config",
                missing = missingEmbeddingsConfig,
            });
        }

        var missingSearchConfig = retrievalService.GetMissingConfiguration();
        if (missingSearchConfig.Count > 0)
        {
            return Results.BadRequest(new
            {
                error = "missing_config",
                missing = missingSearchConfig,
            });
        }

        var embedding = await embeddingService.GenerateEmbeddingAsync(request.Query, cancellationToken);
        var result = await hybridRetrievalService.RetrieveAsync(request.Query, embedding, cancellationToken);

        return Results.Ok(new
        {
            result.Mode,
            hits = result.Hits.Count,
            result.KeywordWeight,
            result.VectorWeight,
            results = result.Hits.Select(static hit => new
            {
                hit.DocId,
                hit.ChunkId,
                hit.Score,
                hit.Title,
                hit.SourcePath,
                hit.ChunkIndex,
                hit.Section,
            }),
        });
    })
    .WithName("HybridSearch");

    app.MapPost("/debug/chunk-preview", (
        ChunkPreviewRequest request,
        MarkdownChunker chunker,
        Microsoft.Extensions.Options.IOptions<KbChunkingOptions> options) =>
    {
        var chunks = chunker.Chunk(request.Markdown ?? string.Empty, options.Value);
        return Results.Ok(new
        {
            options = options.Value,
            chunkCount = chunks.Count,
            chunks,
        });
    })
    .WithName("ChunkPreview");
}

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

public sealed record AskRequest(string Question);

public sealed record AskResponse(
    Guid RequestId,
    long LatencyMs,
    string Answer,
    IReadOnlyList<AskCitation> Citations,
    AskRetrieval Retrieval);

public sealed record AskCitation(
    string DocId,
    string ChunkId,
    string Title,
    string SourcePath,
    string? Section,
    int ChunkIndex);

public sealed record AskRetrieval(
    int TopK,
    string Mode,
    int Hits,
    IReadOnlyList<string> DocIds);

public sealed record ChunkPreviewRequest(string Markdown);

public sealed record KeywordSearchRequest(string Query);

public sealed record IngestKbResponse(
    int DocumentsIngested,
    int ChunksCreated,
    int EmbeddingsCreated,
    int IndexedDocuments,
    IReadOnlyList<KbIngestionFailure> Failures);
