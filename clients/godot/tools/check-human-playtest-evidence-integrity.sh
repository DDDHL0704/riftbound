#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

fail() {
  echo "FAILED: $*" >&2
  exit 1
}

write_small_png() {
  local path="$1"

  # 1x1 transparent PNG.
  if printf '%s' 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=' \
    | base64 -d >"${path}" 2>/dev/null; then
    return 0
  fi

  printf '%s' 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=' \
    | base64 -D >"${path}"
}

write_full_size_png() {
  local path="$1"
  local suffix="${2:-}"
  local small_path="${path}.small"

  if ! command -v sips >/dev/null 2>&1; then
    fail "sips is required to build full-size PNG fixtures"
  fi

  write_small_png "${small_path}"
  sips -z 900 1440 "${small_path}" --out "${path}" >/dev/null
  rm -f "${small_path}"
  printf '%s' "${suffix}" >>"${path}"
}

write_evidence_dir() {
  local evidence_dir="$1"
  local screenshot_size="${2:-full}"
  local screenshot_log_paths="${3:-actual}"
  local duplicate_screenshots="${4:-0}"
  local duplicate_logs="${5:-0}"
  local missing_deck_ready="${6:-0}"
  local player_a_screenshot_log="/tmp/player-a-result.png"
  local player_b_screenshot_log="/tmp/player-b-result.png"
  local room_id="fixture-room"
  local player_a_handle="player-a-fixture"
  local player_b_handle="player-b-fixture"

  if [[ "${screenshot_log_paths}" == "actual" ]]; then
    player_a_screenshot_log="${evidence_dir}/player-a-result.png"
    player_b_screenshot_log="${evidence_dir}/player-b-result.png"
  fi

  mkdir -p "${evidence_dir}"
  cat >"${evidence_dir}/player-a.log" <<EOF
Authenticate: Registered (${player_a_handle}).
JoinRoom requested: room=${room_id}, player=${player_a_handle}.
[b]Joined[/b] type=JOIN room=${room_id} player=${player_a_handle} tick=0 payload=Object
Preconstructed decks loaded: 9.
SubmitDeck receipt accepted=True state=ACCEPTED
Ready receipt accepted=True state=ACCEPTED
MATCH_STARTED
Match result rendered
Visual screenshot saved: ${player_a_screenshot_log}
EOF

  cat >"${evidence_dir}/player-b.log" <<EOF
Authenticate: Registered (${player_b_handle}).
JoinRoom requested: room=${room_id}, player=${player_b_handle}.
[b]Joined[/b] type=JOIN room=${room_id} player=${player_b_handle} tick=0 payload=Object
Preconstructed decks loaded: 9.
SubmitDeck receipt accepted=True state=ACCEPTED
Ready receipt accepted=True state=ACCEPTED
MATCH_STARTED
MATCH_WON
Visual screenshot saved: ${player_b_screenshot_log}
EOF

  if [[ "${missing_deck_ready}" == "1" ]]; then
    cat >"${evidence_dir}/player-a.log" <<EOF
Authenticate: Registered (${player_a_handle}).
JoinRoom requested: room=${room_id}, player=${player_a_handle}.
[b]Joined[/b] type=JOIN room=${room_id} player=${player_a_handle} tick=0 payload=Object
MATCH_STARTED
Match result rendered
Visual screenshot saved: ${player_a_screenshot_log}
EOF

    cat >"${evidence_dir}/player-b.log" <<EOF
Authenticate: Registered (${player_b_handle}).
JoinRoom requested: room=${room_id}, player=${player_b_handle}.
[b]Joined[/b] type=JOIN room=${room_id} player=${player_b_handle} tick=0 payload=Object
MATCH_STARTED
MATCH_WON
Visual screenshot saved: ${player_b_screenshot_log}
EOF
  fi

  if [[ "${duplicate_logs}" == "1" ]]; then
    cat >"${evidence_dir}/player-a.log" <<EOF
Authenticate: Registered (${player_a_handle}).
JoinRoom requested: room=${room_id}, player=${player_a_handle}.
[b]Joined[/b] type=JOIN room=${room_id} player=${player_a_handle} tick=0 payload=Object
Preconstructed decks loaded: 9.
SubmitDeck receipt accepted=True state=ACCEPTED
Ready receipt accepted=True state=ACCEPTED
MATCH_STARTED
MATCH_WON
Visual screenshot saved: ${player_a_screenshot_log}
EOF
    cp "${evidence_dir}/player-a.log" "${evidence_dir}/player-b.log"
  fi

  if [[ "${screenshot_size}" == "small" ]]; then
    write_small_png "${evidence_dir}/player-a-result.png"
    write_small_png "${evidence_dir}/player-b-result.png"
    return
  fi

  write_full_size_png "${evidence_dir}/player-a-result.png" "player-a"
  if [[ "${duplicate_screenshots}" == "1" ]]; then
    cp "${evidence_dir}/player-a-result.png" "${evidence_dir}/player-b-result.png"
  else
    write_full_size_png "${evidence_dir}/player-b-result.png" "player-b"
  fi
}

tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/riftbound-evidence-integrity.XXXXXX")"
trap 'rm -rf "${tmp_dir}"' EXIT

small_evidence_dir="${tmp_dir}/small"
write_evidence_dir "${small_evidence_dir}" "small"
small_output="${tmp_dir}/small-output.log"
if RIFTBOUND_PLAYTEST_REPORT="${small_evidence_dir}/playtest-report.md" \
  "${script_dir}/check-human-playtest-evidence.sh" "${small_evidence_dir}" >"${small_output}" 2>&1; then
  fail "evidence checker accepted too-small result screenshots"
fi

if ! rg -q "screenshot.*too small|too small.*screenshot|minimum" "${small_output}"; then
  echo "Expected small screenshot rejection output:" >&2
  cat "${small_output}" >&2
  fail "evidence checker did not explain the too-small result screenshots"
fi

mismatched_log_dir="${tmp_dir}/mismatched-log-path"
write_evidence_dir "${mismatched_log_dir}" "full" "mismatch"
mismatched_log_output="${tmp_dir}/mismatched-log-path-output.log"
if RIFTBOUND_PLAYTEST_REPORT="${mismatched_log_dir}/playtest-report.md" \
  "${script_dir}/check-human-playtest-evidence.sh" "${mismatched_log_dir}" >"${mismatched_log_output}" 2>&1; then
  fail "evidence checker accepted result screenshot log paths from another directory"
fi

if ! rg -q "screenshot log.*player-a-result\\.png|screenshot log.*player-b-result\\.png|result screenshot log" "${mismatched_log_output}"; then
  echo "Expected mismatched screenshot log path rejection output:" >&2
  cat "${mismatched_log_output}" >&2
  fail "evidence checker did not explain the mismatched result screenshot log path"
fi

duplicate_screenshot_dir="${tmp_dir}/duplicate-screenshot"
write_evidence_dir "${duplicate_screenshot_dir}" "full" "actual" "1" "0"
duplicate_screenshot_output="${tmp_dir}/duplicate-screenshot-output.log"
if RIFTBOUND_PLAYTEST_REPORT="${duplicate_screenshot_dir}/playtest-report.md" \
  "${script_dir}/check-human-playtest-evidence.sh" "${duplicate_screenshot_dir}" >"${duplicate_screenshot_output}" 2>&1; then
  fail "evidence checker accepted identical player A/B result screenshots"
fi

if ! rg -q "result screenshots.*identical|identical.*result screenshots" "${duplicate_screenshot_output}"; then
  echo "Expected duplicate screenshot rejection output:" >&2
  cat "${duplicate_screenshot_output}" >&2
  fail "evidence checker did not explain the duplicate result screenshots"
fi

duplicate_log_dir="${tmp_dir}/duplicate-log"
write_evidence_dir "${duplicate_log_dir}" "full" "actual" "0" "1"
duplicate_log_output="${tmp_dir}/duplicate-log-output.log"
if RIFTBOUND_PLAYTEST_REPORT="${duplicate_log_dir}/playtest-report.md" \
  "${script_dir}/check-human-playtest-evidence.sh" "${duplicate_log_dir}" >"${duplicate_log_output}" 2>&1; then
  fail "evidence checker accepted identical player A/B logs"
fi

if ! rg -q "player A and player B logs are identical|logs.*identical|identical.*logs" "${duplicate_log_output}"; then
  echo "Expected duplicate log rejection output:" >&2
  cat "${duplicate_log_output}" >&2
  fail "evidence checker did not explain the duplicate player logs"
fi

duplicate_identity_dir="${tmp_dir}/duplicate-identity"
write_evidence_dir "${duplicate_identity_dir}" "full"
cat >"${duplicate_identity_dir}/player-b.log" <<EOF
Authenticate: Registered (player-a-fixture).
JoinRoom requested: room=fixture-room, player=player-a-fixture.
[b]Joined[/b] type=JOIN room=fixture-room player=player-a-fixture tick=0 payload=Object
MATCH_STARTED
MATCH_WON
Visual screenshot saved: ${duplicate_identity_dir}/player-b-result.png
EOF
duplicate_identity_output="${tmp_dir}/duplicate-identity-output.log"
if RIFTBOUND_PLAYTEST_REPORT="${duplicate_identity_dir}/playtest-report.md" \
  "${script_dir}/check-human-playtest-evidence.sh" "${duplicate_identity_dir}" >"${duplicate_identity_output}" 2>&1; then
  fail "evidence checker accepted duplicate player identities"
fi

if ! rg -q "Player A handle|Player B handle|distinct|duplicate" "${duplicate_identity_output}"; then
  echo "Expected duplicate identity rejection output:" >&2
  cat "${duplicate_identity_output}" >&2
  fail "evidence checker did not explain the duplicate player identities"
fi

missing_deck_ready_dir="${tmp_dir}/missing-deck-ready"
write_evidence_dir "${missing_deck_ready_dir}" "full" "actual" "0" "0" "1"
missing_deck_ready_output="${tmp_dir}/missing-deck-ready-output.log"
if RIFTBOUND_PLAYTEST_REPORT="${missing_deck_ready_dir}/playtest-report.md" \
  "${script_dir}/check-human-playtest-evidence.sh" "${missing_deck_ready_dir}" >"${missing_deck_ready_output}" 2>&1; then
  fail "evidence checker accepted logs without preconstructed deck submit/ready evidence"
fi

if ! rg -q "Preconstructed|SubmitDeck|Ready" "${missing_deck_ready_output}"; then
  echo "Expected deck/ready rejection output:" >&2
  cat "${missing_deck_ready_output}" >&2
  fail "evidence checker did not explain the missing deck/ready evidence"
fi

incomplete_evidence_dir="${tmp_dir}/incomplete"
write_evidence_dir "${incomplete_evidence_dir}" "full"
incomplete_output="${tmp_dir}/incomplete-output.log"
if ! RIFTBOUND_INCOMPLETE_HUMAN_EVIDENCE=1 \
  RIFTBOUND_PLAYTEST_REPORT="${incomplete_evidence_dir}/playtest-report.md" \
  "${script_dir}/check-human-playtest-evidence.sh" "${incomplete_evidence_dir}" >"${incomplete_output}" 2>&1; then
  echo "Expected incomplete evidence check to pass machine gates while marking the report:" >&2
  cat "${incomplete_output}" >&2
  fail "evidence checker rejected incomplete marker fixture"
fi

if ! rg -q "Incomplete human evidence: 1" "${incomplete_evidence_dir}/playtest-report.md"; then
  echo "Expected incomplete evidence report marker:" >&2
  cat "${incomplete_evidence_dir}/playtest-report.md" >&2
  fail "evidence checker did not write the incomplete evidence marker"
fi

covered_evidence_dir="${tmp_dir}/covered"
write_evidence_dir "${covered_evidence_dir}" "full"
covered_output="${tmp_dir}/covered-output.log"
if ! RIFTBOUND_PLAYTEST_REPORT="${covered_evidence_dir}/playtest-report.md" \
  "${script_dir}/check-human-playtest-evidence.sh" "${covered_evidence_dir}" >"${covered_output}" 2>&1; then
  echo "Expected covered evidence to pass:" >&2
  cat "${covered_output}" >&2
  fail "evidence checker rejected covered full-size screenshots"
fi

if ! rg -q "Required result screenshots: present" "${covered_evidence_dir}/playtest-report.md"; then
  echo "Expected covered report to include screenshot machine-check status:" >&2
  cat "${covered_evidence_dir}/playtest-report.md" >&2
  fail "evidence checker did not write the expected report"
fi

if ! rg -q "Room: fixture-room" "${covered_evidence_dir}/playtest-report.md"; then
  echo "Expected covered report to include room identity:" >&2
  cat "${covered_evidence_dir}/playtest-report.md" >&2
  fail "evidence checker did not write the room identity"
fi

if ! rg -q "Player A handle: player-a-fixture" "${covered_evidence_dir}/playtest-report.md"; then
  echo "Expected covered report to include player A handle:" >&2
  cat "${covered_evidence_dir}/playtest-report.md" >&2
  fail "evidence checker did not write the player A handle"
fi

if ! rg -q "Player B handle: player-b-fixture" "${covered_evidence_dir}/playtest-report.md"; then
  echo "Expected covered report to include player B handle:" >&2
  cat "${covered_evidence_dir}/playtest-report.md" >&2
  fail "evidence checker did not write the player B handle"
fi

echo "Human playtest evidence integrity checks passed."
