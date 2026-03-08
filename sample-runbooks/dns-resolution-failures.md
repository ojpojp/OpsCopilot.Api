# DNS Resolution Failures

## Overview

Use this runbook when outbound calls fail because hosts cannot be resolved.

## Signals

Search for errors such as name resolution failed, no such host, or temporary DNS failure. Compare failure rate across nodes and regions.

## Actions

Check resolver configuration, recent network changes, DNS provider health, and whether the failing hostname was recently updated. Retry using a different resolver if needed for diagnosis.

