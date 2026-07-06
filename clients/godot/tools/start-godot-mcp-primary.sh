#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
screen_name="${RIFTBOUND_GODOT_MCP_SCREEN:-riftbound-godot-mcp}"
log_path="${RIFTBOUND_GODOT_MCP_LOG:-/tmp/riftbound-godot-mcp-primary.log}"
timeout_ms="${RIFTBOUND_GODOT_MCP_TIMEOUT_MS:-30000}"
idle_timeout_ms="${RIFTBOUND_GODOT_MCP_IDLE_TIMEOUT_MS:-300000}"
health_url="http://127.0.0.1:6506/health"
websocket_endpoint="127.0.0.1:6505"
http_endpoint="127.0.0.1:6506"

usage() {
  cat <<EOF
Usage:
  clients/godot/tools/start-godot-mcp-primary.sh [--start|--status|--stop|--restart|--help]

Starts or inspects a Godot MCP primary server under a detached screen session.
This keeps the primary HTTP bridge alive on ${http_endpoint} and the Godot
editor WebSocket bridge alive on ${websocket_endpoint}, so Codex MCP proxy
tools can talk to the running Godot editor.

Environment:
  RIFTBOUND_GODOT_MCP_SCREEN       screen session name (default: ${screen_name})
  RIFTBOUND_GODOT_MCP_LOG          log file (default: ${log_path})
  RIFTBOUND_GODOT_MCP_TIMEOUT_MS   tool timeout (default: ${timeout_ms})
  RIFTBOUND_GODOT_MCP_IDLE_TIMEOUT_MS idle timeout (default: ${idle_timeout_ms})
EOF
}

have_screen() {
  command -v screen >/dev/null 2>&1
}

screen_is_running() {
  if ! have_screen; then
    return 1
  fi

  local sessions
  sessions="$(screen -ls 2>/dev/null || true)"
  grep -Eq "[[:space:]][0-9]+\\.${screen_name}[[:space:]]" <<<"${sessions}"
}

health_is_ok() {
  curl -fsS "${health_url}" >/dev/null 2>&1
}

print_status() {
  echo "Godot MCP primary status"
  echo "  screen: ${screen_name} ($(screen_is_running && echo running || echo stopped))"
  echo "  websocket: ${websocket_endpoint}"
  echo "  http: ${http_endpoint}"
  echo "  log: ${log_path}"

  if health_is_ok; then
    echo "  health: ok"
    curl -fsS "${health_url}" || true
    echo
  else
    echo "  health: unavailable"
  fi

  lsof -nP -iTCP:6505 -iTCP:6506 -sTCP:LISTEN 2>/dev/null || true
}

stop_primary() {
  if screen_is_running; then
    screen -S "${screen_name}" -X quit >/dev/null 2>&1 || true
    for _ in $(seq 1 20); do
      if ! screen_is_running; then
        break
      fi
      sleep 0.2
    done
  fi
}

start_primary() {
  if ! have_screen; then
    echo "screen is required to keep godot-mcp-server attached to a pty." >&2
    exit 2
  fi

  if health_is_ok; then
    print_status
    return
  fi

  mkdir -p "$(dirname "${log_path}")"
  : >"${log_path}"

  # Keep the npm MCP stdio transport attached to a real pty. Plain nohup exits
  # because the godot-mcp-server primary treats closed stdin as client shutdown.
  screen -dmS "${screen_name}" /bin/zsh -lc \
    "cd '${repo_root}' && env GODOT_MCP_TIMEOUT_MS='${timeout_ms}' GODOT_MCP_IDLE_TIMEOUT_MS='${idle_timeout_ms}' npx -y godot-mcp-server >> '${log_path}' 2>&1"

  for _ in $(seq 1 40); do
    if health_is_ok; then
      print_status
      return
    fi
    sleep 0.25
  done

  echo "Godot MCP primary did not become healthy. Recent log:" >&2
  tail -n 80 "${log_path}" >&2 || true
  exit 1
}

mode="${1:---start}"
case "${mode}" in
  --help|-h)
    usage
    ;;
  --status)
    print_status
    ;;
  --start)
    start_primary
    ;;
  --stop)
    stop_primary
    print_status
    ;;
  --restart)
    stop_primary
    start_primary
    ;;
  *)
    usage >&2
    exit 2
    ;;
esac
