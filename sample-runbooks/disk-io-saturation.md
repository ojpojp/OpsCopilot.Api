# Disk I/O Saturation

## Overview

Use this runbook when storage latency rises and services become slow or unstable.

## Signals

Look at disk queue length, read/write latency, throughput, and application logs for timeout or slow write warnings.

## Actions

Identify the process driving I/O, check log volume, database activity, and temporary file usage. Reduce noisy writes and move heavy batch work if necessary.

