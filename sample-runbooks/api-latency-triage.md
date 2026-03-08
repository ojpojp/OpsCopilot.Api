# API Latency Triage

## Overview

Use this runbook when p95 or p99 latency increases and users report slow responses.

## Signals

Compare latency, request rate, and error rate on the same dashboard. Check whether the issue affects one endpoint or the whole service.

## Actions

Inspect downstream dependency latency, thread pool usage, queue depth, and recent deployments. If latency increased without more traffic, focus on code paths or dependency regressions.

