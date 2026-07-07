#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
launcher_path="${repo_root}/clients/godot/tools/start-clean-main-human-playtest-session.sh"

fail() {
  echo "FAILED: $*" >&2
  exit 1
}

require_pattern() {
  local pattern="$1"
  local message="$2"

  if ! rg -q "${pattern}" "${launcher_path}"; then
    fail "${message}"
  fi
}

if [[ ! -f "${launcher_path}" ]]; then
  fail "detached final P5 launcher is missing: ${launcher_path}"
fi

require_pattern "final_wrapper=.*run-clean-main-human-playtest-stack\\.sh" \
  "launcher must target the final clean-main human wrapper"
require_pattern "final_wrapper.* --precheck" \
  "launcher must run the final wrapper precheck before opening windows"
require_pattern "screen -L -dmS" \
  "launcher must use a detached screen session with portable logging supported by macOS screen"
require_pattern "screenlog\\.0" \
  "launcher must report the default screen -L log file"
require_pattern "screen_session_running" \
  "launcher must use a portable screen list parser when checking running sessions"
require_pattern "discover_p5_screen_name" \
  "launcher status must auto-discover an existing final P5 screen session"
require_pattern "print_evidence_status" \
  "launcher status must summarize the current evidence directory"
require_pattern "print_revision_status" \
  "launcher status must summarize the running clean-main revision"
require_pattern "fetch origin main" \
  "launcher status must refresh origin/main before comparing the running clean-main revision"
require_pattern "RIFTBOUND_P5_STATUS_FETCH" \
  "launcher status must allow an offline status check without fetching origin/main"
require_pattern "revision status: STALE" \
  "launcher status must warn when a running final P5 session is behind local origin/main"
require_pattern "Prompt actions:" \
  "launcher status must show the latest prompt actions from player logs"
require_pattern "result screenshot" \
  "launcher status must show whether final result screenshots exist"
require_pattern "run-clean-main-human-playtest-stack\\.sh" \
  "launcher must run the final clean-main human wrapper"
require_pattern "RIFTBOUND_CONFIRM_MANUAL=1" \
  "launcher must preserve manual confirmations"
require_pattern "RIFTBOUND_PACKAGE_EVIDENCE=1" \
  "launcher must preserve evidence packaging"
require_pattern "RIFTBOUND_VERIFY_EVIDENCE_PACKAGE=1" \
  "launcher must preserve package verification"
require_pattern "RIFTBOUND_REQUIRE_CLEAN_GIT=1" \
  "launcher must require clean-git evidence"
require_pattern "export RIFTBOUND_P5_SCREEN_NAME" \
  "launcher must pass the detached screen name into the operator guide"
require_pattern "export RIFTBOUND_P5_STATUS_FILE" \
  "launcher must pass the detached status file into the operator guide"
require_pattern "screen -r" \
  "launcher must tell operators how to attach for manual confirmations"
require_pattern "OPERATOR_GUIDE.md" \
  "launcher must point operators at the generated guide"

if rg -q "RIFTBOUND_WAIT=0|RIFTBOUND_QUIT_AFTER=|--riftbound-smoke-auto-|-Logfile|screen -ls" "${launcher_path}"; then
  fail "launcher must not disable waiting, add auto quit, use automated smoke arguments, or rely on non-portable screen -Logfile/screen -ls behavior"
fi

echo "Clean-main human session launcher checks passed."
