# HTTP 5xx Spike Triage

## Overview

Use this runbook when a service starts returning a high rate of 5xx responses.

## Step 1

Confirm the time window, impacted endpoint, and whether the issue is global or limited to one region.

## Step 2

Check request rate, error rate, and latency together. Compare the failing route against healthy routes.

## Step 3

Inspect dependency health for timeouts, connection pool exhaustion, DNS failures, and TLS errors.

