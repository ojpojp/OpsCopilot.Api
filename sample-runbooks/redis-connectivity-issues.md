# Redis Connectivity Issues

## Overview

Use this runbook when Redis operations time out or fail to connect.

## Signals

Check timeout count, connection errors, CPU on the Redis instance, and network latency between the app and Redis.

## Actions

Validate connection string settings, TLS requirements, max client limits, and whether one application node is opening too many connections. Restarting clients should be a last resort after collecting evidence.

