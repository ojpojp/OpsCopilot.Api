# Week 03 Report

## Chunk Parameter Results

Run ID: `20260327-164905`

| Chunk Size | Overlap | Documents Ingested | Chunks Created | Indexed Documents | Citation Coverage | Avg Hits | Avg Latency (ms) | Label Relevance (10-question sample) | Eval Details |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 800 | 0 | 13 | 13 | 13 | 1.0000 | 3.0 | 2652.3 | 8/10 | `eval/results/week-03-chunk-experiments/week-02-eval-20260327-164911.jsonl` |
| 800 | 200 | 13 | 13 | 13 | 1.0000 | 3.0 | 2820.8 | 8/10 | `eval/results/week-03-chunk-experiments/week-02-eval-20260327-165035.jsonl` |
| 1200 | 0 | 13 | 13 | 13 | 1.0000 | 3.0 | 2566.1 | 8/10 | `eval/results/week-03-chunk-experiments/week-02-eval-20260327-165204.jsonl` |
| 1200 | 200 | 13 | 13 | 13 | 1.0000 | 3.0 | 2175.5 | 8/10 | `eval/results/week-03-chunk-experiments/week-02-eval-20260327-165325.jsonl` |

## Baseline vs Hybrid

Comparison basis:
- same runbook corpus (`13` sample runbooks)
- same chosen chunk parameters (`ChunkSizeChars=1200`, `ChunkOverlapChars=200`)
- same eval question set (`30` questions)
- baseline = `vector`
- candidate = `hybrid`

| Retrieval Mode | Citation Coverage | Avg Hits | Avg Latency (ms) | Label Relevance (10-question sample) | Notes |
|---|---:|---:|---:|---:|---|
| vector | 1.0000 | 3.0 | 2398.8 | 9/10 | Missed `q027` (`Thread Pool Starvation` -> `HTTP 5xx Spike Triage`) |
| hybrid | 1.0000 | 3.0 | 2175.5 | 8/10 | Missed `q027` and `q030` (`API Latency Triage` -> `Database Connection Pool Exhaustion`) |

## Interpretation

- The chunk experiment is mostly inconclusive on retrieval quality because all four configurations produced exactly `13` chunks from `13` documents.
- In practice, the current sample runbooks are short enough that none of the tested chunk thresholds (`800` or `1200`) forced additional splitting.
- Because the indexed corpus is effectively unchanged across the four runs, the small latency differences are noise-level signals, not strong evidence that one chunking strategy is better.
- Given equal retrieval quality across the four chunk experiments, `1200/200` is the most practical provisional baseline because it had the lowest observed average latency.
- On the current corpus, `hybrid` did not outperform `vector`. It matched coverage and hits, but it reduced the labeled relevance check from `9/10` to `8/10`.

## Conclusion

Recommended Week 4 baseline:
- `ChunkSizeChars = 1200`
- `ChunkOverlapChars = 200`

Retrieval recommendation for the current sample corpus:
- Keep `vector` as the baseline evaluation mode.
- Keep `hybrid` implemented and available, but do not treat it as a proven improvement yet.

## Follow-up

- To make chunk tuning meaningful, add longer runbooks or lower the chunk sizes enough to force actual splitting.
- A good next pass is to test with either longer source documents or smaller settings such as `500/100` and `700/100`.
- Re-run the same 30-question eval after the corpus starts producing multi-chunk documents.
