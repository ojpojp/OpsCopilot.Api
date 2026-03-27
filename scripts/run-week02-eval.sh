#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

BASE_URL="${BASE_URL:-http://localhost:5200}"
QUESTIONS_FILE="${QUESTIONS_FILE:-$REPO_ROOT/eval/questions.jsonl}"
OUTPUT_DIR="${OUTPUT_DIR:-$REPO_ROOT/eval/results}"
TIMESTAMP="$(date +"%Y%m%d-%H%M%S")"
DETAILS_FILE="$OUTPUT_DIR/week-02-eval-$TIMESTAMP.jsonl"
SUMMARY_FILE="$OUTPUT_DIR/week-02-eval-$TIMESTAMP.md"

if ! command -v jq >/dev/null 2>&1; then
  echo "Error: jq is required but not found."
  exit 1
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "Error: curl is required but not found."
  exit 1
fi

if [[ ! -f "$QUESTIONS_FILE" ]]; then
  echo "Error: questions file not found: $QUESTIONS_FILE"
  exit 1
fi

mkdir -p "$OUTPUT_DIR"
rm -f "$DETAILS_FILE" "$SUMMARY_FILE"

total_questions=0
successful_responses=0
responses_with_citations=0
request_failures=0

while IFS= read -r line || [[ -n "$line" ]]; do
  [[ -z "$line" ]] && continue

  question_id="$(jq -r '.id' <<<"$line")"
  question_text="$(jq -r '.question' <<<"$line")"
  total_questions=$((total_questions + 1))

  payload="$(jq -nc --arg question "$question_text" '{question: $question}')"
  raw_response="$(curl -sS -X POST "$BASE_URL/ask" -H "Content-Type: application/json" -d "$payload" || true)"

  if [[ -z "$raw_response" ]] || ! jq -e . >/dev/null 2>&1 <<<"$raw_response"; then
    request_failures=$((request_failures + 1))
    jq -nc \
      --arg id "$question_id" \
      --arg question "$question_text" \
      --arg status "request_failed" \
      --arg response "$raw_response" \
      '{id:$id,question:$question,status:$status,response:$response}' >>"$DETAILS_FILE"
    continue
  fi

  citations_count="$(jq -r '(.citations // []) | length' <<<"$raw_response")"
  hit_count="$(jq -r '.retrieval.hits // .retrieval.hitCount // 0' <<<"$raw_response")"
  top_k="$(jq -r '.retrieval.topK // 0' <<<"$raw_response")"
  retrieval_mode="$(jq -r '.retrieval.mode // ""' <<<"$raw_response")"
  latency_ms="$(jq -r '.latencyMs // 0' <<<"$raw_response")"
  first_citation_title="$(jq -r '(.citations[0].title // "")' <<<"$raw_response")"
  status="ok"

  if jq -e '.error? != null' >/dev/null 2>&1 <<<"$raw_response"; then
    status="api_error"
    request_failures=$((request_failures + 1))
  else
    successful_responses=$((successful_responses + 1))
    if [[ "$citations_count" -gt 0 ]]; then
      responses_with_citations=$((responses_with_citations + 1))
    fi
  fi

  jq -nc \
    --arg id "$question_id" \
    --arg question "$question_text" \
    --arg status "$status" \
    --argjson citationsCount "$citations_count" \
    --argjson hitCount "$hit_count" \
    --argjson topK "$top_k" \
    --arg retrievalMode "$retrieval_mode" \
    --argjson latencyMs "$latency_ms" \
    --arg firstCitationTitle "$first_citation_title" \
    --argjson response "$(jq -c . <<<"$raw_response")" \
    '{id:$id,question:$question,status:$status,citationsCount:$citationsCount,hitCount:$hitCount,topK:$topK,retrievalMode:$retrievalMode,latencyMs:$latencyMs,firstCitationTitle:$firstCitationTitle,response:$response}' >>"$DETAILS_FILE"
done <"$QUESTIONS_FILE"

citation_coverage="0.0000"
if [[ "$total_questions" -gt 0 ]]; then
  citation_coverage="$(awk -v a="$responses_with_citations" -v b="$total_questions" 'BEGIN { printf "%.4f", a / b }')"
fi

{
  echo "# Week 02 Eval Summary"
  echo
  echo "- run_at: $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  echo "- base_url: $BASE_URL"
  echo "- questions_file: $QUESTIONS_FILE"
  echo "- details_file: $DETAILS_FILE"
  echo
  echo "## Coverage"
  echo
  echo "- total_questions: $total_questions"
  echo "- successful_responses: $successful_responses"
  echo "- request_failures: $request_failures"
  echo "- responses_with_citations: $responses_with_citations"
  echo "- citation_coverage: $citation_coverage"
  echo
  echo "## Notes"
  echo
  echo "- Use \`$DETAILS_FILE\` to copy per-question citation titles into \`eval/week-02-metrics.md\`."
  echo "- For manual relevance checks, compare the first citation title/path with \`eval/labels-week02.md\`."
} >"$SUMMARY_FILE"

echo "Done."
echo "Details: $DETAILS_FILE"
echo "Summary: $SUMMARY_FILE"
