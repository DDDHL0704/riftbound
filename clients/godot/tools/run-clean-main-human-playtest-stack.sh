#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  clients/godot/tools/run-clean-main-human-playtest-stack.sh
  clients/godot/tools/run-clean-main-human-playtest-stack.sh --precheck

Creates a temporary clean git worktree, checks out the pushed main revision,
then runs the Godot two-human playtest stack from that clean worktree.

Use --precheck to validate the final P5 gates and fetch origin/main without
creating the worktree, opening Godot windows, or writing evidence.

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

precheck_only="${RIFTBOUND_PRECHECK_ONLY:-0}"

while (( $# > 0 )); do
  case "${1:-}" in
    -h|--help)
      usage
      exit 0
      ;;
    --precheck)
      precheck_only=1
      shift
      ;;
    *)
      usage >&2
      exit 2
      ;;
  esac
done

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../../.." && pwd)"

ref="${RIFTBOUND_CLEAN_WORKTREE_REF:-origin/main}"
fetch_ref="${RIFTBOUND_CLEAN_WORKTREE_FETCH:-1}"
keep_worktree="${RIFTBOUND_KEEP_CLEAN_WORKTREE:-0}"
user_worktree_dir="${RIFTBOUND_CLEAN_WORKTREE_DIR:-}"
room="${RIFTBOUND_ROOM:-human-local-$(date +%H%M%S)}"
screenshot_dir="${RIFTBOUND_SCREENSHOT_DIR:-/tmp/riftbound-human-playtest-${room}}"
evidence_package="${RIFTBOUND_EVIDENCE_PACKAGE:-/tmp/riftbound-human-playtest-${room}.tar.gz}"
playtest_report="${RIFTBOUND_PLAYTEST_REPORT:-}"
server="${RIFTBOUND_SERVER:-http://127.0.0.1:5088}"
dotnet_bin="${RIFTBOUND_DOTNET_BIN:-${HOME}/.dotnet/dotnet}"
godot_bin="${RIFTBOUND_GODOT_BIN:-/Applications/Godot_dotnet.app/Contents/MacOS/Godot}"
handle_a="${RIFTBOUND_HANDLE_A:-player-a-${room}}"
handle_b="${RIFTBOUND_HANDLE_B:-player-b-${room}}"
player_key_a="${RIFTBOUND_PLAYER_KEY_A:-pk_${room}_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa}"
player_key_b="${RIFTBOUND_PLAYER_KEY_B:-pk_${room}_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb}"
confirm_manual="${RIFTBOUND_CONFIRM_MANUAL:-1}"
require_clean_git="${RIFTBOUND_REQUIRE_CLEAN_GIT:-1}"
check_evidence="${RIFTBOUND_CHECK_EVIDENCE:-1}"
package_evidence="${RIFTBOUND_PACKAGE_EVIDENCE:-1}"
verify_evidence_package="${RIFTBOUND_VERIFY_EVIDENCE_PACKAGE:-1}"
build_godot="${RIFTBOUND_BUILD_GODOT:-1}"
wait_for_windows="${RIFTBOUND_WAIT:-1}"
quit_after="${RIFTBOUND_QUIT_AFTER:-}"
extra_godot_args="${RIFTBOUND_EXTRA_GODOT_ARGS:-}"
allow_incomplete_evidence="${RIFTBOUND_ALLOW_INCOMPLETE_HUMAN_EVIDENCE:-0}"
incomplete_human_evidence=0
created_worktree=0
require_owned_local_api=1

fingerprint_secret() {
  local secret="$1"
  local length="${#secret}"

  if (( length <= 10 )); then
    printf '<set:%s chars>' "${length}"
    return
  fi

  printf '%s...%s' "${secret:0:4}" "${secret: -4}"
}

validate_output_parent() {
  local target="$1"
  local label="$2"
  local parent
  local existing_parent

  parent="$(dirname "${target}")"
  existing_parent="${parent}"
  while [[ ! -e "${existing_parent}" && "${existing_parent}" != "/" ]]; do
    existing_parent="$(dirname "${existing_parent}")"
  done

  if [[ -e "${parent}" && ! -d "${parent}" ]]; then
    disabled_final_gates+=("${label} parent is not a directory: ${parent}")
    return
  fi

  if [[ -e "${existing_parent}" && ! -d "${existing_parent}" ]]; then
    disabled_final_gates+=("${label} nearest existing parent is not a directory: ${existing_parent}")
    return
  fi

  if [[ -d "${existing_parent}" && ! -w "${existing_parent}" ]]; then
    disabled_final_gates+=("${label} parent is not writable: ${existing_parent}")
  fi
}

write_operator_guide() {
  mkdir -p "${screenshot_dir}"

  local operator_screen_session="${RIFTBOUND_P5_SCREEN_NAME:-not detached}"
  local operator_attach_command="not available; this wrapper was not launched through start-clean-main-human-playtest-session.sh"
  local operator_screen_log="${RIFTBOUND_P5_SCREEN_LOG:-not available}"
  local operator_status_file="${RIFTBOUND_P5_STATUS_FILE:-not available}"
  local operator_status_command="${repo_root}/clients/godot/tools/start-clean-main-human-playtest-session.sh --status"

  if [[ -n "${RIFTBOUND_P5_SCREEN_NAME:-}" ]]; then
    operator_attach_command="screen -r ${RIFTBOUND_P5_SCREEN_NAME}"
  fi

  cat >"${screenshot_dir}/OPERATOR_GUIDE.md" <<EOF
# Riftbound Godot P5 Operator Guide

- Generated at: $(date -u +"%Y-%m-%dT%H:%M:%SZ")
- Source repo: ${repo_root}
- Clean worktree: ${clean_worktree}
- Ref: ${ref}
- Server: ${server}
- Room: ${room}
- Player A handle: ${handle_a}
- Player B handle: ${handle_b}
- Player A key fingerprint: $(fingerprint_secret "${player_key_a}")
- Player B key fingerprint: $(fingerprint_secret "${player_key_b}")
- Evidence directory: ${screenshot_dir}
- Evidence package: ${evidence_package}
- Playtest report: ${screenshot_dir}/playtest-report.md
- Screen session: ${operator_screen_session}
- Screen attach command: ${operator_attach_command}
- Status command: ${operator_status_command}
- Launcher status file: ${operator_status_file}
- Screen log: ${operator_screen_log}

## Final P5 operator checklist

1. Two human players operate the two Godot clients.
2. Both players use preconstructed decks, submit decks, and ready up.
3. Play the match to the server result panel on both clients.
4. Confirm both final screenshots show the server result panel.
5. Confirm each player sees opponent hand and hidden cards only as card backs/counts.
6. Answer the manual confirmation prompts only after checking the final screenshots.

This guide is written before the Godot windows launch so the operators can
recover the room, player handles, player key fingerprints, evidence directory,
package path, detached screen session, attach command, and status command even
if the terminal scrollback is lost.
EOF
}

if [[ "${extra_godot_args}" == *"--riftbound-smoke-auto-"* ]]; then
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
if [[ -n "${extra_godot_args}" ]]; then
  disabled_final_gates+=("RIFTBOUND_EXTRA_GODOT_ARGS=${extra_godot_args}")
fi
if [[ "${fetch_ref}" != "1" ]]; then
  disabled_final_gates+=("RIFTBOUND_CLEAN_WORKTREE_FETCH=${fetch_ref}")
fi
if [[ "${ref}" != "origin/main" ]]; then
  disabled_final_gates+=("RIFTBOUND_CLEAN_WORKTREE_REF=${ref}")
fi
if [[ "${handle_a}" == "${handle_b}" ]]; then
  disabled_final_gates+=("RIFTBOUND_HANDLE_A and RIFTBOUND_HANDLE_B must be distinct")
fi
if [[ "${player_key_a}" == "${player_key_b}" ]]; then
  disabled_final_gates+=("RIFTBOUND_PLAYER_KEY_A and RIFTBOUND_PLAYER_KEY_B must be distinct")
fi
if [[ -e "${screenshot_dir}" && ! -d "${screenshot_dir}" ]]; then
  disabled_final_gates+=("RIFTBOUND_SCREENSHOT_DIR=${screenshot_dir} exists but is not a directory")
fi
if [[ -e "${screenshot_dir}" && -n "$(find "${screenshot_dir}" -mindepth 1 -maxdepth 1 -print -quit)" ]]; then
  disabled_final_gates+=("RIFTBOUND_SCREENSHOT_DIR=${screenshot_dir} is not empty")
fi
if [[ -e "${evidence_package}" ]]; then
  disabled_final_gates+=("RIFTBOUND_EVIDENCE_PACKAGE=${evidence_package} already exists")
fi
validate_output_parent "${screenshot_dir}" "RIFTBOUND_SCREENSHOT_DIR"
validate_output_parent "${evidence_package}" "RIFTBOUND_EVIDENCE_PACKAGE"
if [[ -n "${user_worktree_dir}" ]]; then
  if [[ -e "${user_worktree_dir}" && ! -d "${user_worktree_dir}" ]]; then
    disabled_final_gates+=("RIFTBOUND_CLEAN_WORKTREE_DIR=${user_worktree_dir} exists but is not a directory")
  elif [[ -d "${user_worktree_dir}" && -n "$(find "${user_worktree_dir}" -mindepth 1 -maxdepth 1 -print -quit)" ]]; then
    disabled_final_gates+=("RIFTBOUND_CLEAN_WORKTREE_DIR=${user_worktree_dir} is not empty")
  fi
  validate_output_parent "${user_worktree_dir}" "RIFTBOUND_CLEAN_WORKTREE_DIR"
fi
if [[ -n "${playtest_report}" ]]; then
  disabled_final_gates+=("RIFTBOUND_PLAYTEST_REPORT=${playtest_report}")
fi
case "${server}" in
  http://127.0.0.1:5088|http://localhost:5088)
    if curl -fsS "${server%/}/health" >/dev/null 2>&1; then
      disabled_final_gates+=("RIFTBOUND_SERVER=${server} already has a local API running; stop it so the clean worktree can start its own API")
    fi
    ;;
  *)
    require_owned_local_api=0
    ;;
esac

if (( ${#disabled_final_gates[@]} > 0 )); then
  if [[ "${allow_incomplete_evidence}" != "1" ]]; then
    cat >&2 <<EOF
Refusing final clean-main human playtest with disabled final P5 evidence gates:
  - ${disabled_final_gates[*]}

Final P5 evidence requires a fetched origin/main clean worktree, manual
confirmations, a clean-git report marker, evidence checking, evidence packaging,
package verification, a fresh Godot build, waiting for both Godot windows to
exit, no automatic Godot quit timer, and no extra client arguments. The evidence
directory must be new or empty, the evidence package path must not already
exist, their output parent directories must be writable directories, any custom
clean worktree directory must be empty with a usable parent, and the playtest
report must be generated inside the new evidence directory. When using the
default local API, port 5088 must be free so the clean worktree starts the API
used by the playtest. Set
RIFTBOUND_ALLOW_INCOMPLETE_HUMAN_EVIDENCE=1 only for wrapper development; that
output is not valid final P5 evidence.
EOF
    exit 2
  fi

  incomplete_human_evidence=1
  cat >&2 <<EOF
WARNING: clean-main human playtest is running with disabled final P5 evidence gates:
  - ${disabled_final_gates[*]}
This run is for wrapper development only and is not valid final P5 evidence.
EOF
fi

if [[ "${fetch_ref}" != "0" ]]; then
  git -C "${repo_root}" fetch origin main
fi

if [[ "${precheck_only}" == "1" ]]; then
  precheck_failures=()

  if [[ "${build_godot}" != "0" && ! -x "${godot_bin}" ]]; then
    precheck_failures+=("Godot executable not found: ${godot_bin}")
  fi

  case "${server}" in
    http://127.0.0.1:5088|http://localhost:5088)
      if [[ ! -x "${dotnet_bin}" ]]; then
        precheck_failures+=(".NET executable not found for local API auto-start: ${dotnet_bin}")
      fi
      ;;
  esac

  if (( ${#precheck_failures[@]} > 0 )); then
    printf 'Final P5 precheck failed:\n' >&2
    printf '  - %s\n' "${precheck_failures[@]}" >&2
    exit 2
  fi

  cat <<EOF
Final P5 precheck passed.
  source repo: ${repo_root}
  ref: ${ref}
  fetch origin/main: ${fetch_ref}
  server: ${server}
  Godot executable: ${godot_bin}
  .NET executable: ${dotnet_bin}
  evidence dir: ${screenshot_dir}
  evidence package: ${evidence_package}
  player A handle: ${handle_a}
  player B handle: ${handle_b}
  player A key fingerprint: $(fingerprint_secret "${player_key_a}")
  player B key fingerprint: $(fingerprint_secret "${player_key_b}")
  manual confirmations: ${confirm_manual}
  require clean git: ${require_clean_git}
  check evidence: ${check_evidence}
  package evidence: ${package_evidence}
  verify evidence package: ${verify_evidence_package}
  build Godot: ${build_godot}
  wait for windows: ${wait_for_windows}
  require owned local API: ${require_owned_local_api}

No Godot windows were launched and no evidence was written. Run without
--precheck when both human operators are ready.
EOF
  exit 0
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
write_operator_guide

cat <<EOF
Started clean-main Riftbound Godot human playtest stack.
  source repo: ${repo_root}
  clean worktree: ${clean_worktree}
  ref: ${ref}
  fetch origin/main: ${fetch_ref}
  keep worktree: ${keep_worktree}
  evidence dir: ${screenshot_dir}
  evidence package: ${evidence_package}
  playtest report: ${screenshot_dir}/playtest-report.md
  player A handle: ${handle_a}
  player B handle: ${handle_b}
  player A key: $(fingerprint_secret "${player_key_a}")
  player B key: $(fingerprint_secret "${player_key_b}")
  manual confirmations: ${confirm_manual}
  require clean git: ${require_clean_git}
  check evidence: ${check_evidence}
  package evidence: ${package_evidence}
  verify evidence package: ${verify_evidence_package}
  build Godot: ${build_godot}
  wait for windows: ${wait_for_windows}
  quit after: ${quit_after:-<unset>}
  extra Godot args: ${extra_godot_args:-<unset>}
  incomplete human evidence: ${incomplete_human_evidence}
  operator guide: ${screenshot_dir}/OPERATOR_GUIDE.md

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
export RIFTBOUND_HANDLE_A="${handle_a}"
export RIFTBOUND_HANDLE_B="${handle_b}"
export RIFTBOUND_PLAYER_KEY_A="${player_key_a}"
export RIFTBOUND_PLAYER_KEY_B="${player_key_b}"
export RIFTBOUND_REQUIRE_CLEAN_GIT="${require_clean_git}"
export RIFTBOUND_CONFIRM_MANUAL="${confirm_manual}"
export RIFTBOUND_CHECK_EVIDENCE="${check_evidence}"
export RIFTBOUND_PACKAGE_EVIDENCE="${package_evidence}"
export RIFTBOUND_INCOMPLETE_HUMAN_EVIDENCE="${incomplete_human_evidence}"
export RIFTBOUND_REFUSE_EXISTING_LOCAL_API="${require_owned_local_api}"

"${clean_worktree}/clients/godot/tools/run-local-human-playtest-stack.sh"

if [[ "${package_evidence}" != "0" && "${verify_evidence_package}" != "0" ]]; then
  "${clean_worktree}/clients/godot/tools/verify-human-playtest-package.sh" "${RIFTBOUND_EVIDENCE_PACKAGE}"
fi
