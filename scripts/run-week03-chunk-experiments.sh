#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_PATH="$REPO_ROOT/OpsCopilot.Api/OpsCopilot.Api.csproj"
EVAL_SCRIPT="$REPO_ROOT/scripts/run-week02-eval.sh"

BASE_PORT="${BASE_PORT:-5210}"
BASE_URL="${BASE_URL:-http://localhost:$BASE_PORT}"
OUTPUT_DIR="${OUTPUT_DIR:-$REPO_ROOT/eval/results/week-03-chunk-experiments}"
COMBINATIONS=(
  "800:0"
  "800:200"
  "1200:0"
  "1200:200"
)

if ! command -v curl >/dev/null 2>&1; then
  echo "Error: curl is required but not found."
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "Error: jq is required but not found."
  exit 1
fi

if [[ ! -x "$EVAL_SCRIPT" ]]; then
  echo "Error: eval script not found or not executable: $EVAL_SCRIPT"
  exit 1
fi

mkdir -p "$OUTPUT_DIR"
RUN_ID="$(date +"%Y%m%d-%H%M%S")"
SUMMARY_FILE="$OUTPUT_DIR/week-03-chunk-experiments-$RUN_ID.md"

app_pid=""

cleanup() {
  if [[ -n "$app_pid" ]] && kill -0 "$app_pid" >/dev/null 2>&1; then
    kill "$app_pid" >/dev/null 2>&1 || true
    wait "$app_pid" 2>/dev/null || true
  fi
  app_pid=""
}

trap cleanup EXIT

wait_for_api() {
  local attempts=0
  until curl -fsS "$BASE_URL/health" >/dev/null 2>&1; do
    attempts=$((attempts + 1))
    if [[ "$attempts" -ge 60 ]]; then
      echo "Error: API did not become healthy at $BASE_URL"
      exit 1
    fi
    sleep 1
  done
}

start_api() {
  cleanup
  (
    cd "$REPO_ROOT"
    ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS="$BASE_URL" \
    KbChunking__ChunkSizeChars="$1" \
    KbChunking__ChunkOverlapChars="$2" \
    dotnet run --no-launch-profile --project "$PROJECT_PATH"
  ) >"$OUTPUT_DIR/app-$1-$2.log" 2>&1 &
  app_pid=$!
  wait_for_api
}

{
  echo "# Week 03 Chunk Experiments"
  echo
  echo "- run_id: $RUN_ID"
  echo "- base_url: $BASE_URL"
  echo "- combinations: ${COMBINATIONS[*]}"
  echo
  echo "| Chunk Size | Overlap | Ingested Docs | Chunks | Embeddings | Indexed | Citation Coverage | Eval Summary | Ingest Response |"
  echo "|---|---:|---:|---:|---:|---:|---:|---|---|"
} >"$SUMMARY_FILE"

for combo in "${COMBINATIONS[@]}"; do
  chunk_size="${combo%%:*}"
  overlap="${combo##*:}"

  echo "Running experiment for chunk_size=$chunk_size overlap=$overlap"
  start_api "$chunk_size" "$overlap"

  ingest_file="$OUTPUT_DIR/ingest-$chunk_size-$overlap.json"
  curl -fsS -X POST "$BASE_URL/ingest-kb" >"$ingest_file"

  pushd "$REPO_ROOT" >/dev/null
  eval_output="$(BASE_URL="$BASE_URL" OUTPUT_DIR="$OUTPUT_DIR" "$EVAL_SCRIPT")"
  popd >/dev/null

  details_file="$(awk -F': ' '/^Details:/ {print $2}' <<<"$eval_output")"
  eval_summary_file="$(awk -F': ' '/^Summary:/ {print $2}' <<<"$eval_output")"

  ingested_docs="$(jq -r '.documentsIngested // 0' "$ingest_file")"
  chunks_created="$(jq -r '.chunksCreated // 0' "$ingest_file")"
  embeddings_created="$(jq -r '.embeddingsCreated // 0' "$ingest_file")"
  indexed_documents="$(jq -r '.indexedDocuments // 0' "$ingest_file")"
  citation_coverage="$(awk -F': ' '/citation_coverage/ {print $2}' "$eval_summary_file")"

  printf '| %s | %s | %s | %s | %s | %s | %s | %s | %s |\n' \
    "$chunk_size" \
    "$overlap" \
    "$ingested_docs" \
    "$chunks_created" \
    "$embeddings_created" \
    "$indexed_documents" \
    "$citation_coverage" \
    "$(basename "$eval_summary_file")" \
    "$(basename "$ingest_file")" >>"$SUMMARY_FILE"

  {
    echo
    echo "## chunk_size=$chunk_size overlap=$overlap"
    echo
    echo "- ingest_file: $(basename "$ingest_file")"
    echo "- eval_summary_file: $(basename "$eval_summary_file")"
    echo "- eval_details_file: $(basename "$details_file")"
  } >>"$SUMMARY_FILE"
done

cleanup

echo "Done."
echo "Summary: $SUMMARY_FILE"
