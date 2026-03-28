# Week 03 Score

## Purpose

Manual scoring sheet for Week 3 retrieval and answer quality.

Scoring dimensions:
- `citations_relevant`: Are the returned citations clearly related to the question?
- `answer_grounded`: Is the answer actually supported by the cited evidence?
- `too_generic`: Is the answer correct-looking but too vague to be operationally useful?

Score with `Y` or `N`.

## Sample Set

Selected 20 questions from `eval/questions.jsonl`:

- `q001`
- `q002`
- `q003`
- `q005`
- `q006`
- `q008`
- `q010`
- `q012`
- `q015`
- `q016`
- `q018`
- `q021`
- `q023`
- `q024`
- `q027`
- `q030`
- `q033`
- `q035`
- `q039`
- `q040`

## Score Table

Current helper data comes from:
- `/Users/ojpojp/Code/OpsCopilot.Api/eval/results/week-03-chunk-experiments/week-02-eval-20260327-170952.jsonl`
- `/Users/ojpojp/Code/OpsCopilot.Api/eval/results/week-03-chunk-experiments/week-02-eval-20260327-221058.jsonl`

| Question ID | Short Topic | First Citation Title | Citations Relevant (Y/N) | Answer Grounded (Y/N) | Too Generic (Y/N) | Notes |
|---|---|---|---|---|---|---|
| q001 | 5xx spike first signal | HTTP 5xx Spike Triage | Y | Y | N | solid match |
| q002 | p95 latency bottleneck | API Latency Triage | Y | Y | N | uses cited latency + dependency evidence |
| q003 | CrashLoopBackOff triage | Kubernetes CrashLoopBackOff | Y | Y | N | strong match |
| q005 | memory leak first check | Memory Leak Investigation | Y | Y | N | concrete first-step guidance |
| q006 | DB pool exhaustion | Database Connection Pool Exhaustion | Y | Y | N | strong match |
| q008 | 429 own vs downstream | Rate Limit 429 Triage | Y | Y | N | good distinction flow |
| q010 | cache miss spike | HTTP 5xx Spike Triage | Y | Y | N | answer is plausible, but citation fit is mixed |
| q012 | disk almost full | API Latency Triage | Y | Y | N | answer is usable, citation set is broad |
| q015 | DNS intermittent failures | DNS Resolution Failures | Y | Y | N | strong match |
| q016 | queue backlog growth | Queue Backlog Growth | Y | Y | N | concise and actionable |
| q018 | certificate / TLS errors | Certificate Expiry Response | Y | Y | N | direct match |
| q021 | batch job misses SLA | API Latency Triage | Y | Y | N | queue + worker + dependency split is grounded by citations |
| q023 | connection reset by peer | HTTP 5xx Spike Triage | Y | Y | N | primary citation is broad, but the full citation set is still relevant |
| q024 | one bad Kubernetes node | API Latency Triage | N | N | Y | answer narrows too quickly to disk I/O and misses a stronger node-level checklist |
| q027 | retry volume and cost | HTTP 5xx Spike Triage | N | Y | N | first citation is wrong, but the answer is still supported by the broader citation set |
| q030 | health check green, users fail | API Latency Triage | Y | Y | N | reasonable evidence-backed answer |
| q033 | DB lock vs pool pressure | Thread Pool Starvation | Y | Y | N | first citation is not ideal, but the full citation set supports the distinction |
| q035 | one-region 5xx after config | HTTP 5xx Spike Triage | Y | Y | N | reasonable region-first rollback guidance |
| q039 | p99 much worse than average | API Latency Triage | Y | Y | N | broad but still actionable and evidence-backed |
| q040 | first strong signal triage | API Latency Triage | Y | Y | N | generic in shape, but still concrete enough for a first-pass sequence |

## Summary

Fill after scoring:

- reviewed_questions: 20
- citations_relevant_yes: 18
- citations_relevant_no: 2
- citations_relevant_rate: 0.9000

- answer_grounded_yes: 19
- answer_grounded_no: 1
- answer_grounded_rate: 0.9500

- too_generic_yes: 1
- too_generic_no: 19
- too_generic_rate: 0.0500

## Notes

- Focus on the first citation and the overall citation set, not only whether a keyword happens to overlap.
- Mark `answer_grounded = N` when the answer adds claims that are not supported by the cited runbooks.
- Mark `too_generic = Y` when the answer sounds reasonable but does not give concrete next steps, checks, or distinctions.
