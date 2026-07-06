#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
script_path="${repo_root}/clients/godot/tools/start-godot-mcp-primary.sh"

fail() {
  echo "FAILED: $*" >&2
  exit 1
}

if [[ ! -x "${script_path}" ]]; then
  fail "start-godot-mcp-primary.sh must exist and be executable"
fi

for expected in \
  "--status" \
  "--start" \
  "--stop" \
  "--restart" \
  "screen -dmS" \
  "riftbound-godot-mcp" \
  "GODOT_MCP_TIMEOUT_MS" \
  "GODOT_MCP_IDLE_TIMEOUT_MS" \
  "127.0.0.1:6505" \
  "127.0.0.1:6506" \
  "godot-mcp-server"
do
  if ! rg -q --fixed-strings -- "${expected}" "${script_path}"; then
    fail "start-godot-mcp-primary.sh is missing expected MCP primary contract: ${expected}"
  fi
done

"${script_path}" --help >/tmp/riftbound-godot-mcp-help.txt
for expected in "Usage:" "--status" "--restart" "screen"; do
  if ! rg -q --fixed-strings -- "${expected}" /tmp/riftbound-godot-mcp-help.txt; then
    fail "--help output is missing ${expected}"
  fi
done

"${script_path}" --status >/tmp/riftbound-godot-mcp-status.txt || true
if ! rg -q "Godot MCP primary|6505|6506" /tmp/riftbound-godot-mcp-status.txt; then
  fail "--status output should describe the MCP primary and ports"
fi

echo "Godot MCP primary script checks passed."
