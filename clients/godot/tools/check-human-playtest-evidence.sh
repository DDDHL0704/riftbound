#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../../.." && pwd)"

usage() {
  cat <<'EOF'
Usage:
  clients/godot/tools/check-human-playtest-evidence.sh /path/to/evidence-dir

The evidence directory should contain the logs and screenshots produced by
run-local-human-playtest.sh after two human players finish a Godot match.
The script verifies machine-checkable gates only; the human operators must still
confirm that the match was played by two humans and that opponent hidden cards
are visible only as backs/counts in the final screenshots.
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

evidence_dir="${1:-}"
if [[ -z "${evidence_dir}" ]]; then
  usage >&2
  exit 2
fi

if [[ ! -d "${evidence_dir}" ]]; then
  echo "Evidence directory not found: ${evidence_dir}" >&2
  exit 2
fi

failures=()
manual_failures=()
notes=()
report_path="${RIFTBOUND_PLAYTEST_REPORT:-${evidence_dir}/playtest-report.md}"
confirm_manual="${RIFTBOUND_CONFIRM_MANUAL:-0}"
require_clean_git="${RIFTBOUND_REQUIRE_CLEAN_GIT:-0}"
auto_smoke_found=0
git_status_output="$(git -C "${repo_root}" status --short 2>/dev/null || true)"
git_worktree_state="clean"
min_result_screenshot_width=800
min_result_screenshot_height=600

if [[ -n "${git_status_output}" ]]; then
  git_worktree_state="dirty"
  notes+=("git worktree is dirty; final P5 evidence should be captured from a clean pushed main revision")
  if [[ "${require_clean_git}" == "1" ]]; then
    failures+=("git worktree is dirty while RIFTBOUND_REQUIRE_CLEAN_GIT=1")
  fi
fi

require_file() {
  local path="$1"
  local label="$2"
  if [[ ! -s "${path}" ]]; then
    failures+=("missing ${label}: ${path}")
  fi
}

require_match() {
  local pattern="$1"
  local path="$2"
  local label="$3"
  if ! rg -q "${pattern}" "${path}"; then
    failures+=("${label} not found in ${path}")
  fi
}

require_minimum_png_dimensions() {
  local label="$1"
  local width="$2"
  local height="$3"

  if (( width < min_result_screenshot_width || height < min_result_screenshot_height )); then
    failures+=("${label} is too small for final evidence (${width}x${height}, minimum ${min_result_screenshot_width}x${min_result_screenshot_height})")
  fi
}

require_png_screenshot() {
  local path="$1"
  local label="$2"
  local header=""
  local signature=""
  local ihdr_length=""
  local ihdr_type=""
  local width_hex=""
  local height_hex=""
  local sips_output=""
  local width=0
  local height=0

  if [[ ! -s "${path}" ]]; then
    return
  fi

  header="$(od -An -tx1 -N24 "${path}" | tr -d ' \n')"
  signature="${header:0:16}"
  ihdr_length="${header:16:8}"
  ihdr_type="${header:24:8}"
  width_hex="${header:32:8}"
  height_hex="${header:40:8}"

  if [[ "${signature}" != "89504e470d0a1a0a" || "${ihdr_length}" != "0000000d" || "${ihdr_type}" != "49484452" ]]; then
    failures+=("${label} is not a PNG screenshot")
    return
  fi

  if command -v sips >/dev/null 2>&1; then
    if ! sips_output="$(sips -g pixelWidth -g pixelHeight "${path}" 2>/dev/null)"; then
      failures+=("${label} is not a readable PNG screenshot")
      return
    fi

    width="$(awk '/pixelWidth:/ {print $2}' <<<"${sips_output}")"
    height="$(awk '/pixelHeight:/ {print $2}' <<<"${sips_output}")"
    if [[ ! "${width}" =~ ^[0-9]+$ || ! "${height}" =~ ^[0-9]+$ || "${width}" == "0" || "${height}" == "0" ]]; then
      failures+=("${label} has invalid PNG dimensions")
      return
    fi
    require_minimum_png_dimensions "${label}" "${width}" "${height}"
    return
  fi

  width=$((16#${width_hex}))
  height=$((16#${height_hex}))
  if (( width <= 0 || height <= 0 )); then
    failures+=("${label} has invalid PNG dimensions")
    return
  fi
  require_minimum_png_dimensions "${label}" "${width}" "${height}"
}

player_a_log="${evidence_dir}/player-a.log"
player_b_log="${evidence_dir}/player-b.log"
player_a_result="${evidence_dir}/player-a-result.png"
player_b_result="${evidence_dir}/player-b-result.png"

require_file "${player_a_log}" "player A log"
require_file "${player_b_log}" "player B log"
require_file "${player_a_result}" "player A result screenshot"
require_file "${player_b_result}" "player B result screenshot"
require_png_screenshot "${player_a_result}" "player A result screenshot"
require_png_screenshot "${player_b_result}" "player B result screenshot"

if [[ -s "${player_a_log}" ]]; then
  require_match "MATCH_STARTED" "${player_a_log}" "MATCH_STARTED"
  require_match "MATCH_WON|Match result rendered" "${player_a_log}" "match result"
  require_match "Visual screenshot saved: .*player-a-result\\.png" "${player_a_log}" "player A result screenshot log"
fi

if [[ -s "${player_b_log}" ]]; then
  require_match "MATCH_STARTED" "${player_b_log}" "MATCH_STARTED"
  require_match "MATCH_WON|Match result rendered" "${player_b_log}" "match result"
  require_match "Visual screenshot saved: .*player-b-result\\.png" "${player_b_log}" "player B result screenshot log"
fi

if compgen -G "${evidence_dir}/*.log" >/dev/null; then
  if rg -n "Message queue out of memory|handle_crash|Exception|ERROR|FATAL|REJECTED|rejected|sharing violation" "${evidence_dir}"/*.log; then
    failures+=("error/rejection pattern found in logs")
  fi

  if rg -q "Auto smoke:" "${evidence_dir}"/*.log; then
    auto_smoke_found=1
    notes+=("auto smoke entries found; this evidence is not sufficient for the two-human P5 gate")
  fi
fi

cat <<EOF
Riftbound Godot human playtest evidence check
  evidence_dir: ${evidence_dir}
  player_a_log: ${player_a_log}
  player_b_log: ${player_b_log}
  player_a_result: ${player_a_result}
  player_b_result: ${player_b_result}
EOF

if (( ${#notes[@]} > 0 )); then
  printf '\nNotes:\n'
  printf '  - %s\n' "${notes[@]}"
fi

if (( ${#failures[@]} > 0 )); then
  printf '\nFAILED:\n' >&2
  printf '  - %s\n' "${failures[@]}" >&2
  exit 1
fi

confirm_box_two_humans="[ ]"
confirm_box_a_result="[ ]"
confirm_box_b_result="[ ]"
confirm_box_a_hidden="[ ]"
confirm_box_b_hidden="[ ]"

prompt_confirmation() {
  local variable_name="$1"
  local label="$2"
  local answer=""

  printf '%s [y/N] ' "${label}"
  if read -r answer && [[ "${answer}" =~ ^[Yy]$ ]]; then
    printf -v "${variable_name}" '[x]'
    return 0
  fi

  manual_failures+=("${label}")
  return 0
}

if [[ "${confirm_manual}" == "1" ]]; then
  if [[ "${auto_smoke_found}" == "1" ]]; then
    manual_failures+=("auto smoke entries found; cannot record two-human manual completion")
  else
    printf '\nManual confirmation mode is enabled.\n'
    prompt_confirmation confirm_box_two_humans "Two human players operated the two Godot clients."
    prompt_confirmation confirm_box_a_result "Player A final screenshot shows the server result panel."
    prompt_confirmation confirm_box_b_result "Player B final screenshot shows the server result panel."
    prompt_confirmation confirm_box_a_hidden "Player A sees opponent hand/hidden cards only as card backs and counts."
    prompt_confirmation confirm_box_b_hidden "Player B sees opponent hand/hidden cards only as card backs and counts."
  fi
fi

checked_at="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
git_revision="$(git -C "${repo_root}" rev-parse --short HEAD 2>/dev/null || printf 'unknown')"
{
  cat <<EOF
# Riftbound Godot Human Playtest Report

- Checked at: ${checked_at}
- Git revision: ${git_revision}
- Git worktree: ${git_worktree_state}
- Require clean git: ${require_clean_git}
- Evidence directory: ${evidence_dir}
- Player A log: ${player_a_log}
- Player B log: ${player_b_log}
- Player A result screenshot: ${player_a_result}
- Player B result screenshot: ${player_b_result}

## Machine Check

- Status: passed
- Required logs: present
- Required result screenshots: present and at least ${min_result_screenshot_width}x${min_result_screenshot_height}
- Match lifecycle: MATCH_STARTED and MATCH_WON/result rendering observed
- Error scan: no crash/error/rejection patterns found
- Manual confirmation mode: ${confirm_manual}

## Git Status
EOF

  if [[ -n "${git_status_output}" ]]; then
    printf '\n```text\n%s\n```\n' "${git_status_output}"
  else
    printf '\n- Clean\n'
  fi

  cat <<EOF

## Notes
EOF

  if (( ${#notes[@]} > 0 )); then
    printf '\n'
    printf -- '- %s\n' "${notes[@]}"
  else
    printf '\n- None\n'
  fi

  cat <<EOF

## Manual Confirmations

- ${confirm_box_two_humans} Two human players operated the two Godot clients.
- ${confirm_box_a_result} Player A final screenshot shows the server result panel.
- ${confirm_box_b_result} Player B final screenshot shows the server result panel.
- ${confirm_box_a_hidden} Player A sees opponent hand/hidden cards only as card backs and counts.
- ${confirm_box_b_hidden} Player B sees opponent hand/hidden cards only as card backs and counts.
EOF
} >"${report_path}"

if (( ${#manual_failures[@]} > 0 )); then
  printf '\nMANUAL CONFIRMATION INCOMPLETE:\n' >&2
  printf '  - %s\n' "${manual_failures[@]}" >&2
  printf 'Report written: %s\n' "${report_path}" >&2
  exit 1
fi

if [[ "${confirm_manual}" == "1" ]]; then
  cat <<'EOF'

Machine-checkable gates passed.
Manual confirmations recorded in report:
  - Two human players operated the two Godot clients.
  - Both final screenshots show the result panel.
  - Each player sees opponent hand/hidden cards only as card backs and counts.
EOF
else
  cat <<'EOF'

Machine-checkable gates passed.
Manual confirmations still required:
  - Two human players operated the two Godot clients.
  - Both final screenshots show the result panel.
  - Each player sees opponent hand/hidden cards only as card backs and counts.
EOF
fi
printf 'Report written: %s\n' "${report_path}"
