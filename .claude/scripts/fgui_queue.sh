#!/usr/bin/env bash
# FGUI Editor agent-bridge queue helper (plugins/agent-bridge v0.8.1).
# Protocol: requests/<id>.json -> responses/<id>.json under <project>/.agent/.
# usage: fgui_queue.sh '<request-json>' [timeout_seconds]
set -u

QUEUE="/d/Unity/Project/GDK_FGUI/.agent"
REQ_JSON="${1:-}"
TIMEOUT="${2:-60}"

mkdir -p "$QUEUE/requests" "$QUEUE/responses" 2>/dev/null || true

id="req-$(date +%s)-$RANDOM"
printf '%s' "$REQ_JSON" | python -c "
import json,sys
d=json.load(sys.stdin)
d['id']='$id'
print(json.dumps(d,ensure_ascii=False))
" > "$QUEUE/requests/$id.json"

for _ in $(seq 1 "$TIMEOUT"); do
  if [ -f "$QUEUE/responses/$id.json" ]; then
    cat "$QUEUE/responses/$id.json"
    rm -f "$QUEUE/responses/$id.json"
    exit 0
  fi
  sleep 3
done
echo "{\"ok\":false,\"error\":{\"message\":\"TIMEOUT waiting for $id\"}}"
exit 1
