# Cache Miss Spike

## Overview

Use this runbook when cache hit rate drops and backend load increases.

## Signals

Look for a sudden drop in cache hit ratio, higher database QPS, and increased latency on read-heavy endpoints.

## Actions

Check cache key changes, expiration policy, recent deploys, and whether one node is serving stale or empty cache entries. Validate that the cache cluster is healthy.

