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
incomplete_human_evidence="${RIFTBOUND_INCOMPLETE_HUMAN_EVIDENCE:-0}"
check_inksteel_style="${RIFTBOUND_CHECK_INKSTEEL_STYLE:-1}"
check_battle_layout="${RIFTBOUND_CHECK_BATTLE_LAYOUT:-1}"
auto_smoke_found=0
git_status_output="$(git -C "${repo_root}" status --short 2>/dev/null || true)"
git_worktree_state="clean"
min_result_screenshot_width=800
min_result_screenshot_height=600
inksteel_style_status="skipped"
battle_layout_status="skipped"

if [[ -n "${git_status_output}" ]]; then
  git_worktree_state="dirty"
  notes+=("git worktree is dirty; final P5 evidence should be captured from a clean pushed main revision")
  if [[ "${require_clean_git}" == "1" ]]; then
    failures+=("git worktree is dirty while RIFTBOUND_REQUIRE_CLEAN_GIT=1")
  fi
fi

if [[ "${incomplete_human_evidence}" == "1" ]]; then
  notes+=("incomplete human evidence marker found; this report is not valid final P5 evidence")
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

require_literal_match() {
  local expected="$1"
  local path="$2"
  local label="$3"
  if ! grep -Fq -- "${expected}" "${path}"; then
    failures+=("${label} not found in ${path}")
  fi
}

require_client_setup_matches() {
  local path="$1"
  local label="$2"

  require_match "Preconstructed decks loaded: [1-9][0-9]*\\." "${path}" "${label} preconstructed deck load"
  require_match "SubmitDeck receipt accepted=True" "${path}" "${label} SubmitDeck receipt"
  require_match "Ready receipt accepted=True" "${path}" "${label} Ready receipt"
}

require_hidden_boundary_matches() {
  local path="$1"
  local label="$2"

  require_match "Hidden info boundary ok: .*opponentHandFaces=0.*hiddenCardIdentityLeaks=0" \
    "${path}" "${label} hidden information boundary"

  if rg -q "Hidden info boundary VIOLATION|opponentHandFaces=[1-9][0-9]*|hiddenCardIdentityLeaks=[1-9][0-9]*" "${path}"; then
    failures+=("${label} hidden information boundary violation found in ${path}")
  fi
}

extract_authenticated_handle() {
  local path="$1"
  sed -nE 's/.*Authenticate: .* \(([^)]+)\)\..*/\1/p' "${path}" | head -n 1
}

extract_join_room() {
  local path="$1"
  sed -nE \
    -e 's/.*JoinRoom requested: room=([^,]+), player=.*/\1/p' \
    -e 's/.* type=JOIN room=([^ ]+) player=.*/\1/p' \
    "${path}" | head -n 1
}

extract_join_player() {
  local path="$1"
  sed -nE \
    -e 's/.*JoinRoom requested: room=[^,]+, player=([^.]+)\..*/\1/p' \
    -e 's/.* type=JOIN room=[^ ]+ player=([^ ]+).*/\1/p' \
    "${path}" | head -n 1
}

validate_player_identity() {
  local log_path="$1"
  local label="$2"
  local handle_var="$3"
  local room_var="$4"
  local authenticated_handle=""
  local joined_handle=""
  local joined_room=""
  local selected_handle=""

  if [[ ! -s "${log_path}" ]]; then
    return
  fi

  authenticated_handle="$(extract_authenticated_handle "${log_path}")"
  joined_handle="$(extract_join_player "${log_path}")"
  joined_room="$(extract_join_room "${log_path}")"
  selected_handle="${authenticated_handle:-${joined_handle}}"

  if [[ -z "${selected_handle}" ]]; then
    failures+=("${label} handle not found in ${log_path}")
  fi

  if [[ -z "${joined_room}" ]]; then
    failures+=("${label} room not found in ${log_path}")
  fi

  if [[ -n "${authenticated_handle}" && -n "${joined_handle}" && "${authenticated_handle}" != "${joined_handle}" ]]; then
    failures+=("${label} authenticated handle and joined player disagree in ${log_path}")
  fi

  printf -v "${handle_var}" '%s' "${selected_handle}"
  printf -v "${room_var}" '%s' "${joined_room}"
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

run_inksteel_style_check() {
  local output_path=""
  local output_summary=""

  if [[ "${check_inksteel_style}" == "0" ]]; then
    notes+=("inksteel screenshot style check skipped by RIFTBOUND_CHECK_INKSTEEL_STYLE=0")
    return
  fi

  if [[ ! -s "${player_a_result}" || ! -s "${player_b_result}" ]]; then
    return
  fi

  if [[ ! -x "${script_dir}/check-inksteel-screenshot-style.sh" ]]; then
    failures+=("inksteel screenshot style checker missing: ${script_dir}/check-inksteel-screenshot-style.sh")
    return
  fi

  output_path="$(mktemp)"
  if "${script_dir}/check-inksteel-screenshot-style.sh" \
    "${player_a_result}" \
    "${player_b_result}" >"${output_path}" 2>&1; then
    inksteel_style_status="passed"
    rm -f "${output_path}"
    return
  fi

  output_summary="$(tr '\n' ' ' <"${output_path}" | sed 's/[[:space:]][[:space:]]*/ /g' | cut -c 1-500)"
  rm -f "${output_path}"
  failures+=("inksteel screenshot style check failed: ${output_summary}")
}

run_battle_layout_check() {
  local output_path=""
  local output_summary=""

  if [[ "${check_battle_layout}" == "0" ]]; then
    notes+=("battle layout screenshot check skipped by RIFTBOUND_CHECK_BATTLE_LAYOUT=0")
    return
  fi

  if [[ ! -s "${player_a_result}" || ! -s "${player_b_result}" ]]; then
    return
  fi

  if [[ ! -x "${script_dir}/check-battle-layout-screenshot.sh" ]]; then
    failures+=("battle layout screenshot checker missing: ${script_dir}/check-battle-layout-screenshot.sh")
    return
  fi

  output_path="$(mktemp)"
  if "${script_dir}/check-battle-layout-screenshot.sh" \
    "${player_a_result}" \
    "${player_b_result}" >"${output_path}" 2>&1; then
    battle_layout_status="passed"
    rm -f "${output_path}"
    return
  fi

  output_summary="$(tr '\n' ' ' <"${output_path}" | sed 's/[[:space:]][[:space:]]*/ /g' | cut -c 1-500)"
  rm -f "${output_path}"
  failures+=("battle layout screenshot check failed: ${output_summary}")
}

player_a_log="${evidence_dir}/player-a.log"
player_b_log="${evidence_dir}/player-b.log"
player_a_result="${evidence_dir}/player-a-result.png"
player_b_result="${evidence_dir}/player-b-result.png"
player_a_handle=""
player_b_handle=""
player_a_room=""
player_b_room=""
room_id=""

require_file "${player_a_log}" "player A log"
require_file "${player_b_log}" "player B log"
require_file "${player_a_result}" "player A result screenshot"
require_file "${player_b_result}" "player B result screenshot"
require_png_screenshot "${player_a_result}" "player A result screenshot"
require_png_screenshot "${player_b_result}" "player B result screenshot"
run_inksteel_style_check
run_battle_layout_check

if [[ -s "${player_a_log}" ]]; then
  require_client_setup_matches "${player_a_log}" "Player A"
  require_hidden_boundary_matches "${player_a_log}" "Player A"
  require_match "MATCH_STARTED" "${player_a_log}" "MATCH_STARTED"
  require_match "MATCH_WON|Match result rendered" "${player_a_log}" "match result"
  require_literal_match "Visual screenshot saved: ${player_a_result}" "${player_a_log}" "player A result screenshot log"
fi

if [[ -s "${player_b_log}" ]]; then
  require_client_setup_matches "${player_b_log}" "Player B"
  require_hidden_boundary_matches "${player_b_log}" "Player B"
  require_match "MATCH_STARTED" "${player_b_log}" "MATCH_STARTED"
  require_match "MATCH_WON|Match result rendered" "${player_b_log}" "match result"
  require_literal_match "Visual screenshot saved: ${player_b_result}" "${player_b_log}" "player B result screenshot log"
fi

validate_player_identity "${player_a_log}" "Player A" player_a_handle player_a_room
validate_player_identity "${player_b_log}" "Player B" player_b_handle player_b_room

if [[ -n "${player_a_room}" && -n "${player_b_room}" ]]; then
  if [[ "${player_a_room}" != "${player_b_room}" ]]; then
    failures+=("Player A and Player B joined different rooms (${player_a_room} vs ${player_b_room})")
  else
    room_id="${player_a_room}"
  fi
else
  room_id="${player_a_room:-${player_b_room}}"
fi

if [[ -n "${player_a_handle}" && -n "${player_b_handle}" && "${player_a_handle}" == "${player_b_handle}" ]]; then
  failures+=("Player A handle and Player B handle must be distinct (${player_a_handle})")
fi

if [[ -s "${player_a_log}" && -s "${player_b_log}" ]] && cmp -s "${player_a_log}" "${player_b_log}"; then
  failures+=("player A and player B logs are identical")
fi

if [[ -s "${player_a_result}" && -s "${player_b_result}" ]] && cmp -s "${player_a_result}" "${player_b_result}"; then
  failures+=("player A and player B result screenshots are identical")
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
  room: ${room_id:-unknown}
  player_a_handle: ${player_a_handle:-unknown}
  player_b_handle: ${player_b_handle:-unknown}
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
- Incomplete human evidence: ${incomplete_human_evidence}
- Room: ${room_id}
- Player A handle: ${player_a_handle}
- Player B handle: ${player_b_handle}
- Evidence directory: ${evidence_dir}
- Player A log: ${player_a_log}
- Player B log: ${player_b_log}
- Player A result screenshot: ${player_a_result}
- Player B result screenshot: ${player_b_result}

## Machine Check

- Status: passed
- Required logs: present
- Required result screenshots: present and at least ${min_result_screenshot_width}x${min_result_screenshot_height}
- Inksteel style: ${inksteel_style_status}
- Battle layout: ${battle_layout_status}
- Client setup: preconstructed deck load, SubmitDeck, and Ready observed for both players
- Hidden information boundary: both client logs report zero opponent hand faces and zero hidden identity leaks
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
