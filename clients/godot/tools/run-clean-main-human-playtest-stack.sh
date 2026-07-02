#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  clients/godot/tools/run-clean-main-human-playtest-stack.sh

Creates a temporary clean git worktree, checks out the pushed main revision,
then runs the Godot two-human playtest stack from that clean worktree.

Useful environment overrides:
  RIFTBOUND_CLEAN_WORKTREE_REF=origin/main
  RIFTBOUND_CLEAN_WORKTREE_DIR=/tmp/riftbound-p5-clean
  RIFTBOUND_CLEAN_WORKTREE_FETCH=0
  RIFTBOUND_KEEP_CLEAN_WORKTREE=1
  RIFTBOUND_EVIDENCE_PACKAGE=/tmp/riftbound-human-playtest.tar.gz

The wrapped stack defaults to:
  RIFTBOUND_REQUIRE_CLEAN_GIT=1
  RIFTBOUND_CONFIRM_MANUAL=1
  RIFTBOUND_PACKAGE_EVIDENCE=1
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
created_worktree=0

if [[ "${fetch_ref}" != "0" ]]; then
  git -C "${repo_root}" fetch origin main
fi

if [[ -n "${user_worktree_dir}" ]]; then
  clean_worktree="${user_worktree_dir}"
else
  clean_worktree="$(mktemp -d "${TMPDIR:-/tmp}/riftbound-p5-clean.XXXXXX")"
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
Started clean-main Riftbound Godot human playtest stack.
  source repo: ${repo_root}
  clean worktree: ${clean_worktree}
  ref: ${ref}
  keep worktree: ${keep_worktree}

The evidence checker will run inside the clean worktree, so
RIFTBOUND_REQUIRE_CLEAN_GIT=1 can pass without touching unrelated local edits.
EOF

export RIFTBOUND_REQUIRE_CLEAN_GIT="${RIFTBOUND_REQUIRE_CLEAN_GIT:-1}"
export RIFTBOUND_CONFIRM_MANUAL="${RIFTBOUND_CONFIRM_MANUAL:-1}"
export RIFTBOUND_PACKAGE_EVIDENCE="${RIFTBOUND_PACKAGE_EVIDENCE:-1}"

"${clean_worktree}/clients/godot/tools/run-local-human-playtest-stack.sh"
