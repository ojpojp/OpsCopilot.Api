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
- `AZURE_OPENAI_EMBEDDING_DEPLOYMENT` (your embeddings deployment name)
- `AZURE_OPENAI_API_VERSION` (example: `2025-01-01-preview`)
- `AZURE_OPENAI_EMBEDDING_API_VERSION` (example: `2023-05-15`)
- `AZURE_SEARCH_ENDPOINT` (example: `https://<search-name>.search.windows.net`)
- `AZURE_SEARCH_API_KEY`
- `AZURE_SEARCH_INDEX`

Optional:
- `LOG_DIR` (default: `./OpsCopilot.Api/logs`)
- `AZURE_SEARCH_API_VERSION` (default: `2024-07-01`)

Notes:
- Do not commit secrets. Prefer `dotnet user-secrets` for local development.

Set local secrets:
```bash
dotnet user-secrets init --project ./OpsCopilot.Api/OpsCopilot.Api.csproj

dotnet user-secrets set "AZURE_OPENAI_ENDPOINT" "https://your-resource.openai.azure.com/" --project ./OpsCopilot.Api/OpsCopilot.Api.csproj
dotnet user-secrets set "AZURE_OPENAI_API_KEY" "your-openai-key" --project ./OpsCopilot.Api/OpsCopilot.Api.csproj
dotnet user-secrets set "AZURE_OPENAI_DEPLOYMENT" "opscopilot-gpt-5-chat" --project ./OpsCopilot.Api/OpsCopilot.Api.csproj
dotnet user-secrets set "AZURE_OPENAI_EMBEDDING_DEPLOYMENT" "opscopilot-embedding" --project ./OpsCopilot.Api/OpsCopilot.Api.csproj
dotnet user-secrets set "AZURE_OPENAI_API_VERSION" "2025-01-01-preview" --project ./OpsCopilot.Api/OpsCopilot.Api.csproj
dotnet user-secrets set "AZURE_OPENAI_EMBEDDING_API_VERSION" "2023-05-15" --project ./OpsCopilot.Api/OpsCopilot.Api.csproj
dotnet user-secrets set "AZURE_SEARCH_ENDPOINT" "https://your-search.search.windows.net" --project ./OpsCopilot.Api/OpsCopilot.Api.csproj
dotnet user-secrets set "AZURE_SEARCH_API_KEY" "your-search-key" --project ./OpsCopilot.Api/OpsCopilot.Api.csproj
dotnet user-secrets set "AZURE_SEARCH_INDEX" "opscopilot-kb" --project ./OpsCopilot.Api/OpsCopilot.Api.csproj
```

Check current secrets:
```bash
dotnet user-secrets list --project ./OpsCopilot.Api/OpsCopilot.Api.csproj
```

`dotnet user-secrets` is loaded automatically in `Development`, so the app can read these values without code changes.

## Run
```bash
dotnet run --project ./OpsCopilot.Api/OpsCopilot.Api.csproj
```

By default the app listens on `http://localhost:5200` (see `./OpsCopilot.Api/Properties/launchSettings.json`).

## Prepare Runbooks
- Put Markdown runbooks in the directory configured by `KbIngestion:SourceDirectory`.
- The current development default is `sample-runbooks/`.
- Use one `.md` file per runbook.
- Prefer a top-level `# Title` and section headings such as `## Overview`, `## Signals`, and `## Actions`.
- Keep file names stable because `sourcePath` is used in citations and ingest output.

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
{"requestId":"...","latencyMs":1234,"answer":"...","citations":[...],"retrieval":{"topK":3,"hitCount":3}}
```

If any Azure env var is missing, `/ask` returns HTTP 400 with the missing list.

`/ask` now uses a minimal RAG flow:
- generate an embedding for the question
- retrieve top K chunks from Azure AI Search
- answer using only the retrieved evidence
- return `citations` and `retrieval` metadata

Retrieval tuning:
- `KbRetrieval:TopK` controls how many chunks are retrieved
- `KbRetrieval:CitationCount` controls how many citations are returned

`citations` fields:
- `docId`: stable document id for the runbook
- `chunkId`: stable chunk id inside the indexed document
- `title`: runbook title
- `sourcePath`: Markdown file path for the cited runbook
- `section`: section heading associated with the retrieved chunk
- `chunkIndex`: chunk position inside the runbook

`/ingest-kb` reads Markdown files from `KbIngestion:SourceDirectory`, chunks them, generates one embedding per chunk, uploads them to Azure AI Search, and returns:
- `documentsIngested`
- `chunksCreated`
- `embeddingsCreated`
- `indexedDocuments`
- `failures` (`path` + `reason`)

The target Azure AI Search index must include a vector field named `contentVector` that matches the embedding dimension of your selected embeddings model.

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
