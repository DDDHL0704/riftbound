#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
godot_bin="${RIFTBOUND_GODOT_BIN:-godot}"
proof_log="$(mktemp)"
trap 'rm -f "$proof_log"' EXIT
for resolution in 1280x720 1440x900 1920x1080; do
  if ! "$godot_bin" --headless --resolution "$resolution" --path "$root/clients/godot" \
    res://scenes/debug/BattleTableInteractionProof.tscn -- \
    "--prompt=$root/clients/godot/tests/fixtures/table-interaction-prompt.json" "--table-size=$resolution" > "$proof_log" 2>&1; then
    cat "$proof_log"
    exit 1
  fi
  cat "$proof_log"
  rg -q '^BATTLE_TABLE_INTERACTION_PASS$' "$proof_log"
  ! rg -q '^ERROR:' "$proof_log"
  printf 'Native table interaction and bounds passed: %s\n' "$resolution"
done
