using Microsoft.Extensions.Options;

namespace OpsCopilot.Api.Kb;

public sealed class HybridRetrievalService
{
    private readonly AzureSearchRetrievalService _retrievalService;
    private readonly IOptions<KbRetrievalOptions> _retrievalOptions;

    public HybridRetrievalService(
        AzureSearchRetrievalService retrievalService,
        IOptions<KbRetrievalOptions> retrievalOptions)
    {
        _retrievalService = retrievalService;
        _retrievalOptions = retrievalOptions;
    }

    public async Task<HybridRetrievalResult> RetrieveAsync(
        string question,
        IReadOnlyList<float> questionEmbedding,
        CancellationToken cancellationToken)
    {
        var options = _retrievalOptions.Value;
        var mode = NormalizeMode(options.Mode);
        var topK = Math.Max(options.TopK, 1);

        return mode switch
        {
            "keyword" => new HybridRetrievalResult(
                Mode: "keyword",
                Hits: await _retrievalService.SearchByKeywordAsync(question, topK, cancellationToken),
                KeywordWeight: options.KeywordWeight,
                VectorWeight: options.VectorWeight),
            "vector" => new HybridRetrievalResult(
                Mode: "vector",
                Hits: await _retrievalService.SearchAsync(questionEmbedding, topK, cancellationToken),
                KeywordWeight: options.KeywordWeight,
                VectorWeight: options.VectorWeight),
            _ => await RetrieveHybridAsync(question, questionEmbedding, topK, options, cancellationToken),
        };
    }

    private async Task<HybridRetrievalResult> RetrieveHybridAsync(
        string question,
        IReadOnlyList<float> questionEmbedding,
        int topK,
        KbRetrievalOptions options,
        CancellationToken cancellationToken)
    {
        var keywordHits = await _retrievalService.SearchByKeywordAsync(question, topK, cancellationToken);
        var vectorHits = await _retrievalService.SearchAsync(questionEmbedding, topK, cancellationToken);

        var keywordMaxScore = keywordHits.Count > 0 ? keywordHits.Max(hit => hit.Score) : 0d;
        var vectorMaxScore = vectorHits.Count > 0 ? vectorHits.Max(hit => hit.Score) : 0d;

        var merged = new Dictionary<string, HybridScoreAccumulator>(StringComparer.Ordinal);

        foreach (var hit in keywordHits)
        {
            var accumulator = GetOrAddAccumulator(merged, hit);
            accumulator.KeywordScore = NormalizeScore(hit.Score, keywordMaxScore);
        }

        foreach (var hit in vectorHits)
        {
            var accumulator = GetOrAddAccumulator(merged, hit);
            accumulator.VectorScore = NormalizeScore(hit.Score, vectorMaxScore);
        }

        var ranked = merged.Values
            .Select(accumulator => accumulator.Hit with
            {
                Score = accumulator.KeywordScore * options.KeywordWeight
                    + accumulator.VectorScore * options.VectorWeight,
            })
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.DocId, StringComparer.Ordinal)
            .Take(topK)
            .ToArray();

        return new HybridRetrievalResult(
            Mode: "hybrid",
            Hits: ranked,
            KeywordWeight: options.KeywordWeight,
            VectorWeight: options.VectorWeight);
    }

    private static HybridScoreAccumulator GetOrAddAccumulator(
        IDictionary<string, HybridScoreAccumulator> merged,
        RetrievedChunk hit)
    {
        if (!merged.TryGetValue(hit.ChunkId, out var accumulator))
        {
            accumulator = new HybridScoreAccumulator(hit);
            merged[hit.ChunkId] = accumulator;
        }

        return accumulator;
    }

    private static double NormalizeScore(double score, double maxScore)
    {
        if (maxScore <= 0d)
        {
            return 0d;
        }

        return score / maxScore;
    }

    private static string NormalizeMode(string? mode)
    {
        return mode?.Trim().ToLowerInvariant() switch
        {
            "keyword" => "keyword",
            "vector" => "vector",
            _ => "hybrid",
        };
    }

    private sealed class HybridScoreAccumulator
    {
        public HybridScoreAccumulator(RetrievedChunk hit)
        {
            Hit = hit;
        }

        public RetrievedChunk Hit { get; }

        public double KeywordScore { get; set; }

        public double VectorScore { get; set; }
    }
}

public sealed record HybridRetrievalResult(
    string Mode,
    IReadOnlyList<RetrievedChunk> Hits,
    double KeywordWeight,
    double VectorWeight);
