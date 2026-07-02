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
  RIFTBOUND_ALLOW_INCOMPLETE_HUMAN_EVIDENCE=1

The wrapped stack defaults to:
  RIFTBOUND_REQUIRE_CLEAN_GIT=1
  RIFTBOUND_CONFIRM_MANUAL=1
  RIFTBOUND_PACKAGE_EVIDENCE=1
  RIFTBOUND_VERIFY_EVIDENCE_PACKAGE=1
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
room="${RIFTBOUND_ROOM:-human-local-$(date +%H%M%S)}"
screenshot_dir="${RIFTBOUND_SCREENSHOT_DIR:-/tmp/riftbound-human-playtest-${room}}"
evidence_package="${RIFTBOUND_EVIDENCE_PACKAGE:-/tmp/riftbound-human-playtest-${room}.tar.gz}"
confirm_manual="${RIFTBOUND_CONFIRM_MANUAL:-1}"
require_clean_git="${RIFTBOUND_REQUIRE_CLEAN_GIT:-1}"
check_evidence="${RIFTBOUND_CHECK_EVIDENCE:-1}"
package_evidence="${RIFTBOUND_PACKAGE_EVIDENCE:-1}"
verify_evidence_package="${RIFTBOUND_VERIFY_EVIDENCE_PACKAGE:-1}"
build_godot="${RIFTBOUND_BUILD_GODOT:-1}"
wait_for_windows="${RIFTBOUND_WAIT:-1}"
quit_after="${RIFTBOUND_QUIT_AFTER:-}"
allow_incomplete_evidence="${RIFTBOUND_ALLOW_INCOMPLETE_HUMAN_EVIDENCE:-0}"
created_worktree=0

if [[ "${RIFTBOUND_EXTRA_GODOT_ARGS:-}" == *"--riftbound-smoke-auto-"* ]]; then
  cat >&2 <<'EOF'
Refusing final clean-main human playtest with automated smoke Godot arguments.
Use clients/godot/tools/run-local-simulated-playtest-stack.sh for automated
diagnostics; final P5 evidence must come from two human operators.
EOF
  exit 2
fi

disabled_final_gates=()
if [[ "${confirm_manual}" != "1" ]]; then
  disabled_final_gates+=("RIFTBOUND_CONFIRM_MANUAL=${confirm_manual}")
fi
if [[ "${require_clean_git}" != "1" ]]; then
  disabled_final_gates+=("RIFTBOUND_REQUIRE_CLEAN_GIT=${require_clean_git}")
fi
if [[ "${check_evidence}" != "1" ]]; then
  disabled_final_gates+=("RIFTBOUND_CHECK_EVIDENCE=${check_evidence}")
fi
if [[ "${package_evidence}" != "1" ]]; then
  disabled_final_gates+=("RIFTBOUND_PACKAGE_EVIDENCE=${package_evidence}")
fi
if [[ "${verify_evidence_package}" != "1" ]]; then
  disabled_final_gates+=("RIFTBOUND_VERIFY_EVIDENCE_PACKAGE=${verify_evidence_package}")
fi
if [[ "${build_godot}" == "0" ]]; then
  disabled_final_gates+=("RIFTBOUND_BUILD_GODOT=0")
fi
if [[ "${wait_for_windows}" == "0" ]]; then
  disabled_final_gates+=("RIFTBOUND_WAIT=0")
fi
if [[ -n "${quit_after}" ]]; then
  disabled_final_gates+=("RIFTBOUND_QUIT_AFTER=${quit_after}")
fi
if [[ "${fetch_ref}" != "1" ]]; then
  disabled_final_gates+=("RIFTBOUND_CLEAN_WORKTREE_FETCH=${fetch_ref}")
fi
if [[ "${ref}" != "origin/main" ]]; then
  disabled_final_gates+=("RIFTBOUND_CLEAN_WORKTREE_REF=${ref}")
fi
if [[ -e "${screenshot_dir}" && -n "$(find "${screenshot_dir}" -mindepth 1 -maxdepth 1 -print -quit)" ]]; then
  disabled_final_gates+=("RIFTBOUND_SCREENSHOT_DIR=${screenshot_dir} is not empty")
fi
if [[ -e "${evidence_package}" ]]; then
  disabled_final_gates+=("RIFTBOUND_EVIDENCE_PACKAGE=${evidence_package} already exists")
fi

if (( ${#disabled_final_gates[@]} > 0 )); then
  if [[ "${allow_incomplete_evidence}" != "1" ]]; then
    cat >&2 <<EOF
Refusing final clean-main human playtest with disabled final P5 evidence gates:
  - ${disabled_final_gates[*]}

Final P5 evidence requires a fetched origin/main clean worktree, manual
confirmations, a clean-git report marker, evidence checking, evidence packaging,
package verification, a fresh Godot build, waiting for both Godot windows to
exit, and no automatic Godot quit timer. The evidence directory must be new or
empty, and the evidence package path must not already exist. Set
RIFTBOUND_ALLOW_INCOMPLETE_HUMAN_EVIDENCE=1 only for wrapper development; that
output is not valid final P5 evidence.
EOF
    exit 2
  fi

  cat >&2 <<EOF
WARNING: clean-main human playtest is running with disabled final P5 evidence gates:
  - ${disabled_final_gates[*]}
This run is for wrapper development only and is not valid final P5 evidence.
EOF
fi

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
  fetch origin/main: ${fetch_ref}
  keep worktree: ${keep_worktree}
  evidence dir: ${screenshot_dir}
  evidence package: ${evidence_package}
  manual confirmations: ${confirm_manual}
  require clean git: ${require_clean_git}
  check evidence: ${check_evidence}
  package evidence: ${package_evidence}
  verify evidence package: ${verify_evidence_package}
  build Godot: ${build_godot}
  wait for windows: ${wait_for_windows}
  quit after: ${quit_after:-<unset>}

The evidence checker will run inside the clean worktree, so
RIFTBOUND_REQUIRE_CLEAN_GIT=1 can pass without touching unrelated local edits.

Final P5 operator checklist:
  1. Two human players must operate the two Godot clients.
  2. Both players must use preconstructed decks and play to the server result panel.
  3. Both final screenshots must show the result panel.
  4. Each player must verify opponent hands and hidden cards are visible only as card backs/counts.
  5. Answer the manual confirmation prompts only after checking the final screenshots.
EOF

export RIFTBOUND_ROOM="${room}"
export RIFTBOUND_SCREENSHOT_DIR="${screenshot_dir}"
export RIFTBOUND_EVIDENCE_PACKAGE="${evidence_package}"
export RIFTBOUND_REQUIRE_CLEAN_GIT="${require_clean_git}"
export RIFTBOUND_CONFIRM_MANUAL="${confirm_manual}"
export RIFTBOUND_CHECK_EVIDENCE="${check_evidence}"
export RIFTBOUND_PACKAGE_EVIDENCE="${package_evidence}"

"${clean_worktree}/clients/godot/tools/run-local-human-playtest-stack.sh"

if [[ "${package_evidence}" != "0" && "${verify_evidence_package}" != "0" ]]; then
  "${clean_worktree}/clients/godot/tools/verify-human-playtest-package.sh" "${RIFTBOUND_EVIDENCE_PACKAGE}"
fi
