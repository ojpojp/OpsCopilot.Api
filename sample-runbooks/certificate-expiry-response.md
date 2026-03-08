# Certificate Expiry Response

## Overview

Use this runbook when TLS handshake failures increase or a certificate is near expiry.

## Signals

Check error logs for certificate validation failures, handshake errors, and hostname mismatch messages. Confirm expiry dates on public and internal certificates.

## Actions

Identify affected endpoints, rotate the certificate, reload the service if required, and verify with a fresh TLS handshake test after deployment.

