namespace OpsCopilot.Api.Tools.LogSearch;

public sealed record LogSearchRequest(
    string Query,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int MaxResults);

public sealed record LogSearchResponse(
    Guid QueryId,
    string Query,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int TotalHits,
    IReadOnlyList<LogSearchEvent> Events);

public sealed record LogSearchEvent(
    DateTimeOffset TimeUtc,
    string Service,
    string Region,
    string Level,
    string Message,
    string RequestId,
    string TraceId,
    string Scenario);
