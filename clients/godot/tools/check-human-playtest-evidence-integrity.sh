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

  python3 - "${path}" <<'PY'
import sys
from PIL import Image, ImageDraw

path = sys.argv[1]
image = Image.new("RGB", (1440, 900), (5, 6, 6))
draw = ImageDraw.Draw(image)
line = (178, 171, 145)
dim = (78, 76, 66)
draw.rectangle((22, 58, 1128, 872), outline=line, width=2, fill=(17, 17, 15))
for y in (170, 310, 582, 720):
    draw.line((22, y, 1128, y), fill=line, width=2)
for x in (122, 456, 792, 960):
    draw.line((x, 58, x, 872), fill=dim, width=2)
for lane_x0, lane_x1 in ((128, 620), (628, 1120)):
    draw.rectangle((lane_x0, 342, lane_x1, 578), outline=line, width=2, fill=(61, 59, 52))
    draw.rectangle((lane_x0 + 80, 418, lane_x1 - 8, 502), outline=line, width=2, fill=(16, 16, 14))
draw.rectangle((1152, 16, 1424, 396), outline=line, width=2, fill=(6, 6, 5))
draw.rectangle((1152, 406, 1424, 630), outline=line, width=2, fill=(11, 10, 9))
draw.rectangle((1152, 640, 1424, 884), outline=line, width=2, fill=(6, 6, 5))
image.save(path)
PY
  printf '%s' "${suffix}" >>"${path}"
}

write_cropped_layout_png() {
  local path="$1"
  local suffix="${2:-}"

  python3 - "${path}" <<'PY'
import sys
from PIL import Image, ImageDraw

path = sys.argv[1]
image = Image.new("RGB", (1440, 900), (5, 6, 6))
draw = ImageDraw.Draw(image)
line = (178, 171, 145)
dim = (78, 76, 66)
draw.rectangle((22, 64, 1128, 774), outline=line, width=2, fill=(17, 17, 15))
for y in (224, 358, 626, 712):
    draw.line((22, y, 1128, y), fill=line, width=2)
for x in (122, 456, 792, 960):
    draw.line((x, 64, x, 774), fill=dim, width=2)
for lane_x0, lane_x1 in ((128, 620), (628, 1120)):
    draw.rectangle((lane_x0, 392, lane_x1, 676), outline=line, width=2, fill=(61, 59, 52))
draw.rectangle((1152, 16, 1424, 396), outline=line, width=2, fill=(6, 6, 5))
draw.rectangle((1152, 406, 1424, 630), outline=line, width=2, fill=(11, 10, 9))
draw.rectangle((1152, 640, 1424, 884), outline=line, width=2, fill=(6, 6, 5))
image.save(path)
PY
  printf '%s' "${suffix}" >>"${path}"
}

write_bright_gray_png() {
  local path="$1"

  python3 - "${path}" <<'PY'
import sys
from PIL import Image, ImageDraw

path = sys.argv[1]
image = Image.new("RGB", (1440, 900), (184, 184, 184))
draw = ImageDraw.Draw(image)
for x in range(40, 1400, 140):
    draw.rectangle((x, 80, x + 96, 820), outline=(238, 238, 238), width=6, fill=(150, 150, 150))
for y in range(120, 840, 120):
    draw.line((24, y, 1416, y), fill=(224, 224, 224), width=8)
image.save(path)
PY
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
Hidden info boundary ok: opponentHandFaces=0 opponentHandBacks=4 hiddenCardIdentityLeaks=0
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
Hidden info boundary ok: opponentHandFaces=0 opponentHandBacks=4 hiddenCardIdentityLeaks=0
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
Hidden info boundary ok: opponentHandFaces=0 opponentHandBacks=4 hiddenCardIdentityLeaks=0
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

  if [[ "${screenshot_size}" == "bright" ]]; then
    write_bright_gray_png "${evidence_dir}/player-a-result.png"
    write_bright_gray_png "${evidence_dir}/player-b-result.png"
    printf 'player-a' >>"${evidence_dir}/player-a-result.png"
    printf 'player-b' >>"${evidence_dir}/player-b-result.png"
    return
  fi

  if [[ "${screenshot_size}" == "cropped-layout" ]]; then
    write_cropped_layout_png "${evidence_dir}/player-a-result.png" "player-a"
    write_cropped_layout_png "${evidence_dir}/player-b-result.png" "player-b"
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

bright_style_dir="${tmp_dir}/bright-style"
write_evidence_dir "${bright_style_dir}" "bright"
bright_style_output="${tmp_dir}/bright-style-output.log"
if RIFTBOUND_PLAYTEST_REPORT="${bright_style_dir}/playtest-report.md" \
  "${script_dir}/check-human-playtest-evidence.sh" "${bright_style_dir}" >"${bright_style_output}" 2>&1; then
  fail "evidence checker accepted bright gray result screenshots that drift from inksteel style"
fi

if ! rg -q "inksteel|style|bright|gray|screenshot" "${bright_style_output}"; then
  echo "Expected bright style rejection output:" >&2
  cat "${bright_style_output}" >&2
  fail "evidence checker did not explain the inksteel style rejection"
fi

cropped_layout_dir="${tmp_dir}/cropped-layout"
write_evidence_dir "${cropped_layout_dir}" "cropped-layout"
cropped_layout_output="${tmp_dir}/cropped-layout-output.log"
if RIFTBOUND_PLAYTEST_REPORT="${cropped_layout_dir}/playtest-report.md" \
  "${script_dir}/check-human-playtest-evidence.sh" "${cropped_layout_dir}" >"${cropped_layout_output}" 2>&1; then
  fail "evidence checker accepted result screenshots with a clipped wire-table layout"
fi

if ! rg -q "battle layout|wire table|bottom|right rail|result" "${cropped_layout_output}"; then
  echo "Expected cropped layout rejection output:" >&2
  cat "${cropped_layout_output}" >&2
  fail "evidence checker did not explain the battle-layout screenshot rejection"
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
Hidden info boundary ok: opponentHandFaces=0 opponentHandBacks=4 hiddenCardIdentityLeaks=0
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

missing_hidden_boundary_dir="${tmp_dir}/missing-hidden-boundary"
write_evidence_dir "${missing_hidden_boundary_dir}" "full"
sed -i '' '/Hidden info boundary/d' "${missing_hidden_boundary_dir}/player-a.log" "${missing_hidden_boundary_dir}/player-b.log"
missing_hidden_boundary_output="${tmp_dir}/missing-hidden-boundary-output.log"
if RIFTBOUND_PLAYTEST_REPORT="${missing_hidden_boundary_dir}/playtest-report.md" \
  "${script_dir}/check-human-playtest-evidence.sh" "${missing_hidden_boundary_dir}" >"${missing_hidden_boundary_output}" 2>&1; then
  fail "evidence checker accepted logs without hidden information boundary evidence"
fi

if ! rg -q "Hidden info boundary|hidden information" "${missing_hidden_boundary_output}"; then
  echo "Expected hidden boundary rejection output:" >&2
  cat "${missing_hidden_boundary_output}" >&2
  fail "evidence checker did not explain the missing hidden information boundary evidence"
fi

hidden_leak_dir="${tmp_dir}/hidden-leak"
write_evidence_dir "${hidden_leak_dir}" "full"
printf 'Hidden info boundary VIOLATION: opponentHandFaces=1 opponentHandBacks=4 hiddenCardIdentityLeaks=1\n' >>"${hidden_leak_dir}/player-a.log"
hidden_leak_output="${tmp_dir}/hidden-leak-output.log"
if RIFTBOUND_PLAYTEST_REPORT="${hidden_leak_dir}/playtest-report.md" \
  "${script_dir}/check-human-playtest-evidence.sh" "${hidden_leak_dir}" >"${hidden_leak_output}" 2>&1; then
  fail "evidence checker accepted hidden information boundary violation logs"
fi

if ! rg -q "Hidden info boundary|hidden information|identity leak" "${hidden_leak_output}"; then
  echo "Expected hidden leak rejection output:" >&2
  cat "${hidden_leak_output}" >&2
  fail "evidence checker did not explain the hidden information boundary violation"
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

if ! rg -q "Inksteel style: passed" "${covered_evidence_dir}/playtest-report.md"; then
  echo "Expected covered report to include inksteel style machine-check status:" >&2
  cat "${covered_evidence_dir}/playtest-report.md" >&2
  fail "evidence checker did not write the inksteel style report line"
fi

if ! rg -q "Battle layout: passed" "${covered_evidence_dir}/playtest-report.md"; then
  echo "Expected covered report to include battle layout machine-check status:" >&2
  cat "${covered_evidence_dir}/playtest-report.md" >&2
  fail "evidence checker did not write the battle layout report line"
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
