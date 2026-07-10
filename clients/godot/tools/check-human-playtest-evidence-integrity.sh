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
gradient = Image.linear_gradient("L").resize((1440, 900))
image = Image.merge(
    "RGB",
    (
        gradient.point(lambda value: 12 + value // 32),
        gradient.point(lambda value: 16 + value // 28),
        gradient.point(lambda value: 20 + value // 24),
    ),
)
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((28, 52, 1412, 850), radius=18, fill=(18, 23, 27), outline=(71, 82, 89), width=2)
draw.rounded_rectangle((54, 92, 1386, 350), radius=12, fill=(23, 29, 34), outline=(55, 65, 72), width=2)
draw.rounded_rectangle((54, 394, 1386, 652), radius=12, fill=(25, 31, 36), outline=(64, 75, 82), width=2)
draw.rounded_rectangle((54, 694, 1386, 824), radius=12, fill=(16, 21, 25), outline=(55, 65, 72), width=2)

card_colors = [(45, 111, 168), (153, 68, 58), (55, 130, 92), (126, 78, 162), (183, 121, 42)]
for row_y, offset in ((126, 0), (426, 2)):
    for index in range(7):
        x0 = 118 + index * 176
        color = card_colors[(index + offset) % len(card_colors)]
        draw.rounded_rectangle((x0, row_y, x0 + 112, row_y + 170), radius=8, fill=(8, 11, 14), outline=(135, 146, 152), width=2)
        for band in range(14):
            blend = tuple(min(220, component + band * 3) for component in color)
            y0 = row_y + 8 + band * 8
            draw.rectangle((x0 + 8, y0, x0 + 104, y0 + 8), fill=blend)
        draw.rectangle((x0 + 8, row_y + 122, x0 + 104, row_y + 160), fill=(19, 23, 27))

# The result is a centered neutral modal over a still-visible official-card table.
image = Image.blend(image, Image.new("RGB", image.size, (0, 0, 0)), 0.22)
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((480, 270, 960, 630), radius=14, fill=(68, 73, 77), outline=(198, 205, 208), width=3)
draw.rectangle((520, 330, 920, 334), fill=(151, 163, 169))
draw.rounded_rectangle((590, 516, 850, 574), radius=8, fill=(29, 35, 39), outline=(171, 183, 188), width=2)
image.save(path)
PY
  printf '%s' "${suffix}" >>"${path}"
}

write_missing_result_overlay_png() {
  local path="$1"
  local suffix="${2:-}"

  python3 - "${path}" <<'PY'
import sys
from PIL import Image, ImageDraw

path = sys.argv[1]
gradient = Image.linear_gradient("L").resize((1440, 900))
image = Image.merge(
    "RGB",
    (
        gradient.point(lambda value: 12 + value // 32),
        gradient.point(lambda value: 16 + value // 28),
        gradient.point(lambda value: 20 + value // 24),
    ),
)
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((28, 52, 1412, 850), radius=18, fill=(18, 23, 27), outline=(71, 82, 89), width=2)
for row_y in (126, 426):
    draw.rounded_rectangle((54, row_y - 34, 1386, row_y + 224), radius=12, fill=(23, 29, 34), outline=(55, 65, 72), width=2)
    for index, color in enumerate(((45, 111, 168), (153, 68, 58), (55, 130, 92), (126, 78, 162), (183, 121, 42), (45, 111, 168), (153, 68, 58))):
        x0 = 118 + index * 176
        draw.rounded_rectangle((x0, row_y, x0 + 112, row_y + 170), radius=8, fill=color, outline=(135, 146, 152), width=2)
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
Hidden info boundary ok: opponentHandFaces=0 opponentHandBacks=4 opponentStandbyFaces=0 opponentStandbyBacks=2 hiddenCardIdentityLeaks=0
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
Hidden info boundary ok: opponentHandFaces=0 opponentHandBacks=4 opponentStandbyFaces=0 opponentStandbyBacks=2 hiddenCardIdentityLeaks=0
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
Hidden info boundary ok: opponentHandFaces=0 opponentHandBacks=4 opponentStandbyFaces=0 opponentStandbyBacks=2 hiddenCardIdentityLeaks=0
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

  if [[ "${screenshot_size}" == "missing-result" ]]; then
    write_missing_result_overlay_png "${evidence_dir}/player-a-result.png" "player-a"
    write_missing_result_overlay_png "${evidence_dir}/player-b-result.png" "player-b"
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

blank_table_dir="${tmp_dir}/blank-table"
write_evidence_dir "${blank_table_dir}" "bright"
blank_table_output="${tmp_dir}/blank-table-output.log"
if RIFTBOUND_PLAYTEST_REPORT="${blank_table_dir}/playtest-report.md" \
  "${script_dir}/check-human-playtest-evidence.sh" "${blank_table_dir}" >"${blank_table_output}" 2>&1; then
  fail "evidence checker accepted result screenshots without official-card table content"
fi

if ! rg -q "official-card|dark table|bright|blank|screenshot" "${blank_table_output}"; then
  echo "Expected missing official-card table rejection output:" >&2
  cat "${blank_table_output}" >&2
  fail "evidence checker did not explain the missing official-card table content"
fi

missing_result_dir="${tmp_dir}/missing-result"
write_evidence_dir "${missing_result_dir}" "missing-result"
missing_result_output="${tmp_dir}/missing-result-output.log"
if RIFTBOUND_PLAYTEST_REPORT="${missing_result_dir}/playtest-report.md" \
  "${script_dir}/check-human-playtest-evidence.sh" "${missing_result_dir}" >"${missing_result_output}" 2>&1; then
  fail "evidence checker accepted result screenshots without a centered result overlay"
fi

if ! rg -q "centered result|result panel|neutral result|center" "${missing_result_output}"; then
  echo "Expected centered result overlay rejection output:" >&2
  cat "${missing_result_output}" >&2
  fail "evidence checker did not explain the missing centered result overlay"
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
Hidden info boundary ok: opponentHandFaces=0 opponentHandBacks=4 opponentStandbyFaces=0 opponentStandbyBacks=2 hiddenCardIdentityLeaks=0
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
printf 'Hidden info boundary VIOLATION: opponentHandFaces=1 opponentHandBacks=4 opponentStandbyFaces=1 opponentStandbyBacks=2 hiddenCardIdentityLeaks=1\n' >>"${hidden_leak_dir}/player-a.log"
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

if ! rg -q "Official-card table: passed" "${covered_evidence_dir}/playtest-report.md"; then
  echo "Expected covered report to include official-card table machine-check status:" >&2
  cat "${covered_evidence_dir}/playtest-report.md" >&2
  fail "evidence checker did not write the official-card table report line"
fi

if ! rg -q "Centered result overlay: passed" "${covered_evidence_dir}/playtest-report.md"; then
  echo "Expected covered report to include centered result overlay machine-check status:" >&2
  cat "${covered_evidence_dir}/playtest-report.md" >&2
  fail "evidence checker did not write the centered result overlay report line"
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
