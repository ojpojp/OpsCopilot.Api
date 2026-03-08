# Memory Leak Investigation

## Overview

Use this runbook when memory usage keeps growing and the process is eventually killed or restarted.

## Signals

Watch working set, heap size, GC frequency, and restart events. Confirm whether memory returns after garbage collection or stays high.

## Actions

Review recent code changes, cache growth, large object allocations, and long-lived collections. Capture a memory dump if the issue is repeatable in a safe environment.

