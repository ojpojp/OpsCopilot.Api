# Rate Limit 429 Triage

## Overview

Use this runbook when clients or dependencies receive many 429 responses.

## Signals

Find which route or client is triggering the highest rate of 429s. Check request bursts, concurrency, and whether throttling happens locally or downstream.

## Actions

Inspect rate-limit configuration, recent traffic spikes, retry storms, and abusive clients. Apply backoff, reduce retries, or raise limits only after the root cause is clear.

