# W1 Failure Taxonomy

Goal: during each evaluation/manual run, use consistent labels to record *why* an answer failed, so each week’s improvements can be targeted.

## How to use
- When an answer is unsatisfactory, assign one primary category (optionally with a subcategory).
- Record minimal evidence: the input question, the system output, your expected output, and why you judged it that way.
- Prefer actionable causes; avoid vague notes like “it’s not good”.

## Categories

### 1) `missing_context`
The question lacks critical context; the system should ask clarifying questions or refuse, but it didn’t.
- Typical missing fields: service name, time window, environment (prod/staging), error signature (status code / exception / trace id), blast radius
- Example: user only says “the service is slow” without endpoint, time range, or whether it affects all users
- Fix direction: clarification strategy, required-field checks, templated follow-ups

### 2) `too_generic`
The answer is a textbook checklist and fails to converge to an actionable next step.
- Example: says “check logs/monitoring” but doesn’t specify which metrics or query conditions
- Fix direction: provide top 3 next actions + required inputs; structured output (steps, commands, expected observations)

### 3) `hallucination`
The answer includes unsupported facts/commands/conclusions, or states uncertain info as certain.
- Example: invents configuration options/dashboards/service names that don’t exist
- Fix direction: prompt constraints, evidence/citation requirements, refuse/clarify when evidence is insufficient

### 4) `wrong_tool`
Chooses an inappropriate information source/tool, or uses obviously unreasonable tool parameters.
- Example: a KB/runbook question gets only generic advice; log time range is needed but not requested and a query is attempted anyway
- Fix direction: tool routing, parameter validation, default time-window strategy

### 5) `format_error`
The output format doesn’t meet expectations (missing fields, invalid JSON, missing requestId/latency, etc.).
- Example: API response is missing `requestId` or `latencyMs`
- Fix direction: contract/integration tests, unified response models

### 6) `safety_issue`
The output contains sensitive info, over-privileged guidance, or encourages dangerous actions.
- Example: suggests deleting production data, outputs secrets, bypasses access controls
- Fix direction: safety rules, disallowed-action list, RBAC/security trimming (later weeks)

## Suggested logging template (paste into an issue/notes)
- id:
- question:
- expected:
- actual:
- failure_category:
- notes:
