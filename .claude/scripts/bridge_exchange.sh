#!/usr/bin/env bash
# Unity Agent Bridge fixed-slot single-flight exchange (per installed AGENT.md protocol).
# usage: bridge_exchange.sh '<json-request>' [timeout_seconds]
set -u

BRIDGE="/d/GitHubProject/FGUI2GDK/Unity/.agentbridge"
REQ_JSON="${1:-}"
TIMEOUT="${2:-120}"

cd "$BRIDGE" || { echo '{"error":"bridge root missing"}'; exit 1; }

# Acknowledge any stale exchange from a previous crashed session first:
# wait for the host to clean up processing.json, then delete response.json as ack.
if [ -f response.json ]; then
  for _ in $(seq 1 30); do [ -f processing.json ] || break; sleep 1; done
  rm -f response.json
fi
if [ -f processing.json ]; then
  echo '{"error":"processing.json stuck after ack wait"}'
  exit 1
fi

# Atomic publish: tmp write + rename into the fixed slot.
printf '%s' "$REQ_JSON" > request.json.tmp
mv -f request.json.tmp request.json

for _ in $(seq 1 "$TIMEOUT"); do
  if [ -f response.json ]; then
    cat response.json
    # Protocol invariant: keep response.json until Unity removes processing.json.
    for _ in $(seq 1 30); do [ -f processing.json ] || break; sleep 1; done
    rm -f response.json
    exit 0
  fi
  sleep 1
done
echo '{"error":"TIMEOUT"}'
exit 1
