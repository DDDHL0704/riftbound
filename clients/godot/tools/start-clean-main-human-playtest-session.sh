#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  clients/godot/tools/start-clean-main-human-playtest-session.sh
  clients/godot/tools/start-clean-main-human-playtest-session.sh --precheck
  clients/godot/tools/start-clean-main-human-playtest-session.sh --status

Starts the final clean-main two-human Godot playtest wrapper inside a detached
screen session. This does not relax final P5 evidence gates: the wrapped script
still waits for both Godot windows, requires manual confirmations, packages the
evidence, and verifies the package.

Useful environment overrides:
  RIFTBOUND_ROOM=human-local-170000
  RIFTBOUND_SCREENSHOT_DIR=/tmp/riftbound-human-playtest-human-local-170000
  RIFTBOUND_EVIDENCE_PACKAGE=/tmp/riftbound-human-playtest-human-local-170000.tar.gz
  RIFTBOUND_P5_SCREEN_NAME=riftbound-p5-human-local-170000

When --status is used without overrides, the launcher reports the latest
running riftbound-p5-* screen session, falling back to the latest status file.
EOF
}

mode="start"
while (( $# > 0 )); do
  case "${1:-}" in
    -h|--help)
      usage
      exit 0
      ;;
    --precheck)
      mode="precheck"
      shift
      ;;
    --status)
      mode="status"
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
final_wrapper="${script_dir}/run-clean-main-human-playtest-stack.sh"
room="${RIFTBOUND_ROOM:-human-local-$(date +%H%M%S)}"
screenshot_dir="${RIFTBOUND_SCREENSHOT_DIR:-/tmp/riftbound-human-playtest-${room}}"
evidence_package="${RIFTBOUND_EVIDENCE_PACKAGE:-/tmp/riftbound-human-playtest-${room}.tar.gz}"
screen_name="${RIFTBOUND_P5_SCREEN_NAME:-riftbound-p5-${room}}"
screen_dir="${RIFTBOUND_P5_SCREEN_DIR:-/tmp/${screen_name}.screen}"
screen_log="${RIFTBOUND_P5_SCREEN_LOG:-${screen_dir}/screenlog.0}"
status_file="${RIFTBOUND_P5_STATUS_FILE:-/tmp/${screen_name}.status}"

list_screen_names() {
  { screen -list 2>/dev/null || true; } \
    | sed -nE 's/^[[:space:]]*[0-9]+[.]([^[:space:]]+)[[:space:]].*/\1/p'
}

screen_session_running() {
  local name="$1"
  list_screen_names | grep -Fxq "${name}"
}

discover_p5_screen_name() {
  list_screen_names | awk '/^riftbound-p5-/ { print }' | sort | tail -n 1
}

discover_p5_status_name() {
  local latest_status=""
  latest_status="$(ls -t /tmp/riftbound-p5-*.status 2>/dev/null | head -n 1 || true)"
  if [[ -n "${latest_status}" ]]; then
    basename "${latest_status}" .status
  fi
}

configure_session_paths() {
  screen_name="$1"
  room="${screen_name#riftbound-p5-}"
  screenshot_dir="${RIFTBOUND_SCREENSHOT_DIR:-/tmp/riftbound-human-playtest-${room}}"
  evidence_package="${RIFTBOUND_EVIDENCE_PACKAGE:-/tmp/riftbound-human-playtest-${room}.tar.gz}"
  screen_dir="${RIFTBOUND_P5_SCREEN_DIR:-/tmp/${screen_name}.screen}"
  screen_log="${RIFTBOUND_P5_SCREEN_LOG:-${screen_dir}/screenlog.0}"
  status_file="${RIFTBOUND_P5_STATUS_FILE:-/tmp/${screen_name}.status}"
}

latest_log_match() {
  local path="$1"
  local pattern="$2"
  local fallback="$3"
  local value=""

  if [[ -s "${path}" ]]; then
    value="$(rg "${pattern}" "${path}" | tail -n 1 || true)"
  fi

  if [[ -n "${value}" ]]; then
    echo "${value}"
  else
    echo "${fallback}"
  fi
}

print_player_evidence_status() {
  local label="$1"
  local log_path="$2"
  local initial_screenshot="$3"
  local result_screenshot="$4"

  echo "  ${label}:"
  if [[ -s "${log_path}" ]]; then
    echo "    latest prompt: $(latest_log_match "${log_path}" "Prompt actions:" "not observed")"
    echo "    setup: $(latest_log_match "${log_path}" "SubmitDeck receipt|Ready receipt" "waiting for deck submit/ready")"
    echo "    hidden info: $(latest_log_match "${log_path}" "Hidden info boundary" "not observed")"
    echo "    result: $(latest_log_match "${log_path}" "MATCH_WON|Match result rendered|Visual screenshot saved: .*result" "not reached")"
  else
    echo "    log: missing"
  fi

  if [[ -s "${initial_screenshot}" ]]; then
    echo "    initial screenshot: present"
  else
    echo "    initial screenshot: missing"
  fi

  if [[ -s "${result_screenshot}" ]]; then
    echo "    result screenshot: present"
  else
    echo "    result screenshot: missing"
  fi
}

print_evidence_status() {
  if [[ ! -d "${screenshot_dir}" ]]; then
    return
  fi

  echo
  echo "Evidence snapshot (${screenshot_dir}):"
  print_player_evidence_status \
    "Player A" \
    "${screenshot_dir}/player-a.log" \
    "${screenshot_dir}/player-a.png" \
    "${screenshot_dir}/player-a-result.png"
  print_player_evidence_status \
    "Player B" \
    "${screenshot_dir}/player-b.log" \
    "${screenshot_dir}/player-b.png" \
    "${screenshot_dir}/player-b-result.png"
}

if [[ "${mode}" == "status"
  && -z "${RIFTBOUND_P5_SCREEN_NAME:-}"
  && -z "${RIFTBOUND_ROOM:-}" ]]; then
  discovered_screen_name="$(discover_p5_screen_name)"
  if [[ -z "${discovered_screen_name}" ]]; then
    discovered_screen_name="$(discover_p5_status_name)"
  fi

  if [[ -n "${discovered_screen_name}" ]]; then
    configure_session_paths "${discovered_screen_name}"
  fi
fi

export RIFTBOUND_ROOM="${room}"
export RIFTBOUND_SCREENSHOT_DIR="${screenshot_dir}"
export RIFTBOUND_EVIDENCE_PACKAGE="${evidence_package}"
export RIFTBOUND_CONFIRM_MANUAL=1
export RIFTBOUND_REQUIRE_CLEAN_GIT=1
export RIFTBOUND_CHECK_EVIDENCE=1
export RIFTBOUND_PACKAGE_EVIDENCE=1
export RIFTBOUND_VERIFY_EVIDENCE_PACKAGE=1
export RIFTBOUND_BUILD_GODOT=1
export RIFTBOUND_WAIT=1

unset RIFTBOUND_EXTRA_GODOT_ARGS
unset RIFTBOUND_QUIT_AFTER
unset RIFTBOUND_PRECHECK_ONLY
unset RIFTBOUND_ALLOW_INCOMPLETE_HUMAN_EVIDENCE

if [[ "${mode}" == "precheck" ]]; then
  "${final_wrapper}" --precheck
  exit 0
fi

if [[ "${mode}" == "status" ]]; then
  if screen_session_running "${screen_name}"; then
    echo "Final P5 screen session is running: ${screen_name}"
  else
    echo "Final P5 screen session is not running: ${screen_name}"
  fi

  if [[ -s "${status_file}" ]]; then
    echo
    cat "${status_file}"
  fi

  print_evidence_status

  if [[ -s "${screen_log}" ]]; then
    echo
    echo "Last screen log lines (${screen_log}):"
    tail -40 "${screen_log}"
  fi

  exit 0
fi

if ! command -v screen >/dev/null 2>&1; then
  echo "screen is required to launch a detached final P5 session." >&2
  exit 1
fi

"${final_wrapper}" --precheck

if screen_session_running "${screen_name}"; then
  echo "A final P5 screen session is already running: ${screen_name}" >&2
  echo "Attach with: screen -r ${screen_name}" >&2
  exit 2
fi

mkdir -p "${screen_dir}" "$(dirname "${status_file}")"
rm -f "${screen_log}" "${status_file}"

cat >"${status_file}" <<EOF
Final P5 detached session
  screen: ${screen_name}
  attach: screen -r ${screen_name}
  source repo: ${repo_root}
  wrapper: ${final_wrapper}
  room: ${room}
  evidence dir: ${screenshot_dir}
  operator guide: ${screenshot_dir}/OPERATOR_GUIDE.md
  evidence package: ${evidence_package}
  screen dir: ${screen_dir}
  screen log: ${screen_log}

After both Godot windows reach the result panel, attach to the screen session
and answer the manual confirmation prompts only after checking both final
screenshots and hidden-information boundaries.
EOF

(
  cd "${screen_dir}"
  screen -L -dmS "${screen_name}" bash -lc \
    "cd $(printf '%q' "${repo_root}") && exec $(printf '%q' "${final_wrapper}")"
)

cat <<EOF
Started final P5 detached screen session.
  screen: ${screen_name}
  attach: screen -r ${screen_name}
  evidence dir: ${screenshot_dir}
  operator guide: ${screenshot_dir}/OPERATOR_GUIDE.md
  evidence package: ${evidence_package}
  status file: ${status_file}
  screen dir: ${screen_dir}
  screen log: ${screen_log}

The wrapped final P5 script still waits for both Godot windows and requires
manual confirmations. This launcher does not create valid P5 evidence by
itself; two human operators must complete the match and confirmations.
EOF
