using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using OpsCopilot.Api.Kb;
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
builder.Services.AddSingleton<MarkdownChunker>();
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

    var requestUri = new Uri(
        $"{endpoint!.TrimEnd('/')}/openai/deployments/{Uri.EscapeDataString(deployment!)}/chat/completions?api-version={Uri.EscapeDataString(apiVersion!)}",
        UriKind.Absolute);

    var payload = new
    {
        messages = new[]
        {
            new { role = "user", content = request.Question },
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
        "Ask request {RequestId} ok latencyMs={LatencyMs} deployment={Deployment}",
        requestId,
        stopwatch.ElapsedMilliseconds,
        deployment);

    return Results.Ok(new AskResponse(requestId, stopwatch.ElapsedMilliseconds, answer));
})
.WithName("Ask");

app.MapPost("/ingest-kb", async (
    Microsoft.Extensions.Options.IOptions<KbIngestionOptions> ingestionOptions,
    Microsoft.Extensions.Options.IOptions<KbChunkingOptions> chunkingOptions,
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
        result.Failures));
})
.WithName("IngestKb");

if (app.Environment.IsDevelopment())
{
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

public sealed record AskResponse(Guid RequestId, long LatencyMs, string Answer);

public sealed record ChunkPreviewRequest(string Markdown);

public sealed record IngestKbResponse(
    int DocumentsIngested,
    int ChunksCreated,
    IReadOnlyList<KbIngestionFailure> Failures);
