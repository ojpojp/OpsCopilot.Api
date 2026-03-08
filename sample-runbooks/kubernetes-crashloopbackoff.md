# Kubernetes CrashLoopBackOff

## Overview

Use this runbook when Pods restart repeatedly and do not stay healthy.

## Signals

Check Pod status, restart count, last termination reason, and recent events. Review container logs from the previous restart if available.

## Actions

Validate environment variables, secrets, probes, resource limits, and image changes. If the issue started after a deploy, compare the last working revision and roll back if needed.

