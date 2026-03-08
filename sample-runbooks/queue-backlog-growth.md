# Queue Backlog Growth

## Overview

Use this runbook when queue depth keeps increasing and consumers cannot keep up.

## Signals

Compare producer rate, consumer rate, processing latency, retry count, and dead-letter volume.

## Actions

Check whether producers changed traffic shape, whether consumers are blocked by dependencies, and whether message size or retry policy increased processing time.

