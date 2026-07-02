#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  clients/godot/tools/run-clean-main-simulated-playtest-stack.sh

Creates a temporary clean git worktree from the pushed main revision, then runs
the visible two-Godot-client simulated playtest stack from that clean worktree.

This is a diagnostic/preflight tool only. It intentionally uses auto-smoke
actions and is not valid final two-human P5 evidence.

Useful environment overrides:
  RIFTBOUND_CLEAN_WORKTREE_REF=origin/main
  RIFTBOUND_CLEAN_WORKTREE_DIR=/tmp/riftbound-p5-sim-clean
  RIFTBOUND_CLEAN_WORKTREE_FETCH=0
  RIFTBOUND_KEEP_CLEAN_WORKTREE=1
  RIFTBOUND_ROOM=sim-clean-local-test
  RIFTBOUND_SCREENSHOT_DIR=/tmp/riftbound-sim-clean-local-test
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../../.." && pwd)"

ref="${RIFTBOUND_CLEAN_WORKTREE_REF:-origin/main}"
fetch_ref="${RIFTBOUND_CLEAN_WORKTREE_FETCH:-1}"
keep_worktree="${RIFTBOUND_KEEP_CLEAN_WORKTREE:-0}"
user_worktree_dir="${RIFTBOUND_CLEAN_WORKTREE_DIR:-}"
room="${RIFTBOUND_ROOM:-sim-clean-main-$(date +%H%M%S)}"
screenshot_dir="${RIFTBOUND_SCREENSHOT_DIR:-/tmp/riftbound-simulated-playtest-${room}}"
created_worktree=0

if [[ "${fetch_ref}" != "0" ]]; then
  git -C "${repo_root}" fetch origin main
fi

if [[ -n "${user_worktree_dir}" ]]; then
  clean_worktree="${user_worktree_dir}"
else
  clean_worktree="$(mktemp -d "${TMPDIR:-/tmp}/riftbound-p5-sim-clean.XXXXXX")"
  rmdir "${clean_worktree}"
fi

if [[ -e "${clean_worktree}" ]]; then
  if [[ -n "$(find "${clean_worktree}" -mindepth 1 -maxdepth 1 -print -quit)" ]]; then
    echo "Clean worktree directory is not empty: ${clean_worktree}" >&2
    exit 2
  fi
fi

cleanup() {
  if [[ "${created_worktree}" == "1" && "${keep_worktree}" != "1" ]]; then
    git -C "${repo_root}" worktree remove --force "${clean_worktree}" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

git -C "${repo_root}" worktree add --detach "${clean_worktree}" "${ref}"
created_worktree=1

cat <<EOF
Started clean-main Riftbound Godot simulated playtest stack.
  source repo: ${repo_root}
  clean worktree: ${clean_worktree}
  ref: ${ref}
  keep worktree: ${keep_worktree}
  room: ${room}
  evidence dir: ${screenshot_dir}

This run is automated and is not valid final two-human P5 evidence.
Use run-clean-main-human-playtest-stack.sh for the final two-human handoff.
EOF

export RIFTBOUND_ROOM="${room}"
export RIFTBOUND_SCREENSHOT_DIR="${screenshot_dir}"

"${clean_worktree}/clients/godot/tools/run-local-simulated-playtest-stack.sh"
