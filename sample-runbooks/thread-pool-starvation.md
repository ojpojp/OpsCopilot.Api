# Thread Pool Starvation

## Overview

Use this runbook when requests hang, latency rises, and the service appears busy even with moderate traffic.

## Signals

Look for long request duration, many queued work items, blocked async calls, and dependencies with slow response times.

## Actions

Check for sync-over-async code, long blocking operations, excessive retries, and slow downstream services. A dependency slowdown often triggers thread pool starvation in application code.
