#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  clients/godot/tools/run-local-simulated-playtest-stack.sh

Runs a visible two-Godot-client simulated playtest against the local memory API.
Both clients use the server-provided auto-smoke path to submit a preconstructed
deck, ready, confirm mulligan, and surrender when the server prompt exposes it.

This is a diagnostic/preflight tool only. Its evidence intentionally contains
Auto smoke entries and is not valid final two-human P5 evidence.

Useful environment overrides:
  RIFTBOUND_ROOM=sim-local-test
  RIFTBOUND_SCREENSHOT_DIR=/tmp/riftbound-sim-local-test
  RIFTBOUND_QUIT_AFTER=9000
  RIFTBOUND_SIMULATED_EXTRA_ARGS="--riftbound-smoke-auto-tap-rune"
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
room="${RIFTBOUND_ROOM:-sim-local-$(date +%H%M%S)}"
screenshot_dir="${RIFTBOUND_SCREENSHOT_DIR:-/tmp/riftbound-simulated-playtest-${room}}"
base_args="--riftbound-smoke-auto-ready --riftbound-smoke-auto-mulligan --riftbound-smoke-auto-surrender"
extra_args="${RIFTBOUND_SIMULATED_EXTRA_ARGS:-}"

if [[ -n "${RIFTBOUND_EXTRA_GODOT_ARGS:-}" ]]; then
  extra_args="${extra_args:+${extra_args} }${RIFTBOUND_EXTRA_GODOT_ARGS}"
fi

cat <<EOF
Starting simulated Riftbound Godot playtest.
  room: ${room}
  evidence dir: ${screenshot_dir}

This run is intentionally automated and is not valid final two-human P5 evidence.
EOF

export RIFTBOUND_ROOM="${room}"
export RIFTBOUND_SCREENSHOT_DIR="${screenshot_dir}"
export RIFTBOUND_QUIT_AFTER="${RIFTBOUND_QUIT_AFTER:-9000}"
export RIFTBOUND_SCREENSHOT_MIN_TABLE_CARDS="${RIFTBOUND_SCREENSHOT_MIN_TABLE_CARDS:-0}"
export RIFTBOUND_CHECK_EVIDENCE="${RIFTBOUND_CHECK_EVIDENCE:-1}"
export RIFTBOUND_PACKAGE_EVIDENCE="${RIFTBOUND_PACKAGE_EVIDENCE:-0}"
export RIFTBOUND_CONFIRM_MANUAL=0
export RIFTBOUND_EXTRA_GODOT_ARGS="${base_args}${extra_args:+ ${extra_args}}"

"${script_dir}/run-local-human-playtest-stack.sh"
