# Database Connection Pool Exhaustion

## Overview

Use this runbook when requests fail because the application cannot get a database connection.

## Signals

Look for connection timeout errors, rising queue depth, and long-running queries.

## Actions

Check max pool size, connection lifetime settings, and whether connections are properly disposed.
