# W2 Retrieval Labels

## Purpose

Manual labels for Week 2 retrieval evaluation.
Each selected question is mapped to the runbook that should be retrieved.
This file labels documents only and does not provide final answers.

## Labels

- `q001`
  - question: `A service’s 5xx error rate suddenly spikes—what’s the first metric/signal you check?`
  - expected_title: `HTTP 5xx Spike Triage`
  - expected_source_path: `sample-runbooks/http-5xx-triage.md`

- `q003`
  - question: `In Kubernetes, a Deployment’s Pods are frequently restarting (CrashLoopBackOff). What troubleshooting order would you follow?`
  - expected_title: `Kubernetes CrashLoopBackOff`
  - expected_source_path: `sample-runbooks/kubernetes-crashloopbackoff.md`

- `q005`
  - question: `Memory keeps increasing until OOM and a leak is suspected—what’s the first step to verify it?`
  - expected_title: `Memory Leak Investigation`
  - expected_source_path: `sample-runbooks/memory-leak-investigation.md`

- `q006`
  - question: `A database connection pool is exhausted (too many connections / pool exhausted). What are the common root causes and troubleshooting steps?`
  - expected_title: `Database Connection Pool Exhaustion`
  - expected_source_path: `sample-runbooks/database-connection-pool.md`

- `q008`
  - question: `A service is returning lots of 429s (rate limit). How do you tell whether it’s your own throttling or downstream throttling?`
  - expected_title: `Rate Limit 429 Triage`
  - expected_source_path: `sample-runbooks/rate-limit-429-triage.md`

- `q015`
  - question: `How do you tell whether intermittent failures are caused by DNS issues? Which metrics or log fields do you check?`
  - expected_title: `DNS Resolution Failures`
  - expected_source_path: `sample-runbooks/dns-resolution-failures.md`

- `q016`
  - question: `A message queue backlog keeps growing and consumers can’t keep up. How do you quickly tell whether producers are too fast or consumers are slower?`
  - expected_title: `Queue Backlog Growth`
  - expected_source_path: `sample-runbooks/queue-backlog-growth.md`

- `q018`
  - question: `TLS/certificate-related errors suddenly increase (handshake failed / cert expired). What’s the first step to confirm blast radius and root cause?`
  - expected_title: `Certificate Expiry Response`
  - expected_source_path: `sample-runbooks/certificate-expiry-response.md`

- `q027`
  - question: `A service has high retry volume and rising cost, but the top-line success rate still looks acceptable. What should you inspect first?`
  - expected_title: `Thread Pool Starvation`
  - expected_source_path: `sample-runbooks/thread-pool-starvation.md`

- `q030`
  - question: `A health check stays green while real user requests fail. How would you explain the gap and what would you instrument next?`
  - expected_title: `API Latency Triage`
  - expected_source_path: `sample-runbooks/api-latency-triage.md`

## Notes

- Use these labels to check whether `/ask` citations point to the expected runbook.
- For W2, title/path matching is enough; exact chunk matching is optional.
- If a question reasonably matches more than one runbook, count it as relevant when the returned citation is clearly related.
