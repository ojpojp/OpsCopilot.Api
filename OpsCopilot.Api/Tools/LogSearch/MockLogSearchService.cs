using System.Text.Json;
using Microsoft.Extensions.Options;

namespace OpsCopilot.Api.Tools.LogSearch;

public sealed class MockLogSearchService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHostEnvironment _environment;
    private readonly LogSearchOptions _options;
    private readonly ILogger<MockLogSearchService> _logger;

    public MockLogSearchService(
        IHostEnvironment environment,
        IOptions<LogSearchOptions> options,
        ILogger<MockLogSearchService> logger)
    {
        _environment = environment;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LogSearchResponse> SearchAsync(
        LogSearchRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var queryTokens = Tokenize(request.Query);
        var events = await LoadEventsAsync(cancellationToken);

        var matchedEvents = events
            .Where(logEvent => logEvent.TimeUtc >= request.FromUtc && logEvent.TimeUtc <= request.ToUtc)
            .Select(logEvent => new
            {
                Event = logEvent,
                Score = Score(logEvent, queryTokens),
            })
            .Where(scored => scored.Score > 0)
            .OrderByDescending(scored => scored.Score)
            .ThenBy(scored => scored.Event.TimeUtc)
            .Take(request.MaxResults)
            .Select(scored => scored.Event)
            .ToArray();

        _logger.LogInformation(
            "Mock log search query={Query} fromUtc={FromUtc} toUtc={ToUtc} maxResults={MaxResults} hits={Hits}",
            request.Query,
            request.FromUtc,
            request.ToUtc,
            request.MaxResults,
            matchedEvents.Length);

        return new LogSearchResponse(
            Guid.NewGuid(),
            request.Query,
            request.FromUtc,
            request.ToUtc,
            matchedEvents.Length,
            matchedEvents);
    }

    private void ValidateRequest(LogSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new LogSearchValidationException("query is required");
        }

        if (request.Query.Length > _options.MaxQueryLength)
        {
            throw new LogSearchValidationException($"query cannot exceed {_options.MaxQueryLength} characters");
        }

        if (request.ToUtc <= request.FromUtc)
        {
            throw new LogSearchValidationException("toUtc must be later than fromUtc");
        }

        var timeRange = request.ToUtc - request.FromUtc;
        if (timeRange > TimeSpan.FromHours(_options.MaxTimeRangeHours))
        {
            throw new LogSearchValidationException($"time range cannot exceed {_options.MaxTimeRangeHours} hours");
        }

        if (request.MaxResults < 1)
        {
            throw new LogSearchValidationException("maxResults must be at least 1");
        }

        if (request.MaxResults > _options.MaxResults)
        {
            throw new LogSearchValidationException($"maxResults cannot exceed {_options.MaxResults}");
        }
    }

    private async Task<IReadOnlyList<LogSearchEvent>> LoadEventsAsync(CancellationToken cancellationToken)
    {
        var directory = ResolveFixturesDirectory();
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Log fixtures directory was not found: {directory}");
        }

        var events = new List<LogSearchEvent>();
        var files = Directory
            .EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            await using var stream = File.OpenRead(file);
            var fixture = await JsonSerializer.DeserializeAsync<LogFixture>(
                stream,
                JsonOptions,
                cancellationToken: cancellationToken);

            if (fixture is null)
            {
                continue;
            }

            foreach (var logEvent in fixture.Events)
            {
                events.Add(new LogSearchEvent(
                    logEvent.TimeUtc,
                    logEvent.Service,
                    logEvent.Region,
                    logEvent.Level,
                    logEvent.Message,
                    logEvent.RequestId,
                    logEvent.TraceId,
                    fixture.Scenario));
            }
        }

        return events;
    }

    private string ResolveFixturesDirectory()
    {
        if (Path.IsPathRooted(_options.FixturesDirectory))
        {
            return _options.FixturesDirectory;
        }

        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _options.FixturesDirectory));
    }

    private static IReadOnlyList<string> Tokenize(string query)
    {
        return query
            .Split([' ', '\t', '\r', '\n', ',', '.', ':', ';', '/', '\\', '(', ')', '[', ']', '{', '}', '"', '\''], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static token => token.Length >= 2)
            .Select(static token => token.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static int Score(LogSearchEvent logEvent, IReadOnlyList<string> queryTokens)
    {
        var searchableText = string.Join(
            ' ',
            logEvent.Service,
            logEvent.Region,
            logEvent.Level,
            logEvent.Message,
            logEvent.RequestId,
            logEvent.TraceId,
            logEvent.Scenario).ToLowerInvariant();

        var score = 0;
        foreach (var token in queryTokens)
        {
            if (searchableText.Contains(token, StringComparison.Ordinal))
            {
                score += 1;
            }
        }

        return score;
    }

    private sealed record LogFixture(
        string Scenario,
        string Service,
        string Region,
        IReadOnlyList<LogFixtureEvent> Events);

    private sealed record LogFixtureEvent(
        DateTimeOffset TimeUtc,
        string Service,
        string Region,
        string Level,
        string Message,
        string RequestId,
        string TraceId);
}
