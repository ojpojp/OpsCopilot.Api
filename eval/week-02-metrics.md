# W2 Metrics

## Goal

Track the minimum retrieval quality baseline for Week 2.

## 1. Citation Coverage

Definition:
- Citation coverage = number of responses with at least one citation / total number of evaluated questions

Formula:
```text
citation_coverage = responses_with_citations / total_questions
```

Current run:
- total_questions: 30
- responses_with_citations: 30
- citation_coverage: 1.0000

Notes:
- Count a response as "with citations" when `citations.length > 0`
- If the API returns an answer but `citations` is empty, count it as "no citation"

## 2. Citation Relevance (manual spot check: 10 questions)

Definition:
- Check whether the returned citation points to a runbook that is relevant to the question
- Mark each sample as `Relevant` or `Not Relevant`

### Sample Review Table

| Question ID | Expected Runbook Title | Returned Citation Title | Result | Notes |
|---|---|---|---|---|
| q001 | HTTP 5xx Spike Triage | HTTP 5xx Spike Triage | Relevant | Title matches expected runbook |
| q003 | Kubernetes CrashLoopBackOff | Kubernetes CrashLoopBackOff | Relevant | Title matches expected runbook |
| q005 | Memory Leak Investigation | Memory Leak Investigation | Relevant | Title matches expected runbook |
| q006 | Database Connection Pool Exhaustion | Database Connection Pool Exhaustion | Relevant | Title matches expected runbook |
| q008 | Rate Limit 429 Triage | Rate Limit 429 Triage | Relevant | Title matches expected runbook |
| q015 | DNS Resolution Failures | DNS Resolution Failures | Relevant | Title matches expected runbook |
| q016 | Queue Backlog Growth | Queue Backlog Growth | Relevant | Title matches expected runbook |
| q018 | Certificate Expiry Response | Certificate Expiry Response | Relevant | Title matches expected runbook |
| q027 | Thread Pool Starvation | HTTP 5xx Spike Triage | Not Relevant | Returned citation does not match expected runbook intent |
| q030 | API Latency Triage | API Latency Triage | Relevant | Title matches expected runbook |

Summary:
- reviewed_questions: 10
- relevant: 9
- not_relevant: 1
- relevance_rate: 0.9000

Formula:
```text
relevance_rate = relevant / reviewed_questions
```

## 3. Interpretation

- If citation coverage is low:
  - inspect Azure Search indexing
  - inspect `KbRetrieval:TopK`
  - inspect chunk quality and runbook coverage

- If coverage is high but relevance is low:
  - inspect embeddings quality
  - inspect Azure Search vector field configuration
  - inspect runbook titles/content overlap

- If both are acceptable:
  - move on to prompt tuning and answer quality checks
