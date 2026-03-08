# OpsCopilot.Api

Minimal Web API slice for an ops-focused copilot. W1 delivers:
- `GET /health`
- `POST /ask` (single Azure OpenAI Chat call)
- Basic telemetry in response: `requestId`, `latencyMs`

## Prerequisites
- .NET SDK matching the project target framework (currently `net10.0`)
- An Azure OpenAI resource + a chat model deployment

## Configuration (environment variables)
Required:
- `AZURE_OPENAI_ENDPOINT` (example: `https://<resource-name>.openai.azure.com/`)
- `AZURE_OPENAI_API_KEY`
- `AZURE_OPENAI_DEPLOYMENT` (your deployment name, e.g. `opscopilot-gpt-5-chat`)
- `AZURE_OPENAI_API_VERSION` (example: `2025-01-01-preview`)

Optional:
- `LOG_DIR` (default: `./OpsCopilot.Api/logs`)

Notes:
- Do not commit secrets. Prefer shell `export` or `dotnet user-secrets`.

## Run
```bash
dotnet run --project ./OpsCopilot.Api/OpsCopilot.Api.csproj
```

By default the app listens on `http://localhost:5200` (see `./OpsCopilot.Api/Properties/launchSettings.json`).

## Test with curl
Health:
```bash
curl http://localhost:5200/health
```

Ingest KB:
```bash
curl -X POST http://localhost:5200/ingest-kb
```

Ask:
```bash
curl -X POST http://localhost:5200/ask \
  -H 'Content-Type: application/json' \
  -d '{"question":"If a service’s 5xx error rate suddenly spikes, what’s the first step to troubleshoot?"}'
```

Expected response shape:
```json
{"requestId":"...","latencyMs":1234,"answer":"..."}
```

If any Azure env var is missing, `/ask` returns HTTP 400 with the missing list.

`/ingest-kb` reads Markdown files from `KbIngestion:SourceDirectory`, chunks them, and returns:
- `documentsIngested`
- `chunksCreated`
- `failures` (`path` + `reason`)

## Using `OpsCopilot.Api.http`
File: `./OpsCopilot.Api/OpsCopilot.Api.http`
- VS Code: install the `REST Client` extension (`humao.rest-client`), then click `Send Request`.
- Rider: open the file and use the built-in HTTP client run action.

## Logs
- Console + rolling file logs (daily): `./OpsCopilot.Api/logs/opscopilot-YYYYMMDD.log`
- `logs/` and `*.log` are ignored by git (see `.gitignore`)

## Eval (W1)
- Question set: `./eval/questions.jsonl`
- Failure taxonomy: `./eval/failure-taxonomy.md`
