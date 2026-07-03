#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../../.." && pwd)"

fail() {
  echo "FAILED: $*" >&2
  exit 1
}

write_small_png() {
  local path="$1"
  local suffix="${2:-}"

  # 1x1 transparent PNG.
  if printf '%s' 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=' \
    | base64 -d >"${path}" 2>/dev/null; then
    printf '%s' "${suffix}" >>"${path}"
    return 0
  fi

  printf '%s' 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=' \
    | base64 -D >"${path}"
  printf '%s' "${suffix}" >>"${path}"
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
for x in range(8, 1136, 56):
    draw.line((x, 96, x, 890), fill=(155, 149, 128), width=2)
for y in range(108, 840, 72):
    draw.line((0, y, 1136, y), fill=(161, 154, 132), width=2)
for rect in ((22, 118, 682, 300), (108, 442, 444, 636), (806, 442, 1144, 636), (1152, 16, 1424, 536)):
    draw.rectangle(rect, outline=(185, 176, 148), width=3, fill=(18, 18, 16))
draw.rectangle((22, 120, 300, 298), outline=(158, 36, 28), width=3)
draw.rectangle((604, 128, 680, 292), outline=(143, 113, 56), width=3)
image.save(path)
PY
  printf '%s' "${suffix}" >>"${path}"
}

write_bright_gray_png() {
  local path="$1"
  local suffix="${2:-}"

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
  printf '%s' "${suffix}" >>"${path}"
}

write_result_png() {
  local path="$1"
  local suffix="${2:-}"
  local size="${3:-full}"

  if [[ "${size}" == "small" ]]; then
    write_small_png "${path}" "${suffix}"
    return
  fi

  if [[ "${size}" == "bright" ]]; then
    write_bright_gray_png "${path}" "${suffix}"
    return
  fi

  write_full_size_png "${path}" "${suffix}"
}

write_evidence_bundle() {
  local bundle_dir="$1"
  local revision="$2"
  local manual_confirmation_mode="${3:-1}"
  local duplicate_screenshots="${4:-0}"
  local duplicate_logs="${5:-0}"
  local extra_file="${6:-0}"
  local missing_deck_ready="${7:-0}"
  local screenshot_size="${8:-full}"
  local screenshot_report_paths="${9:-match}"
  local incomplete_human_evidence="${10:-0}"
  local player_a_result_path="/tmp/riftbound-human-playtest/player-a-result.png"
  local player_b_result_path="/tmp/riftbound-human-playtest/player-b-result.png"
  local room_id="fixture-room"
  local player_a_handle="player-a-fixture"
  local player_b_handle="player-b-fixture"

  if [[ "${screenshot_report_paths}" == "mismatch" ]]; then
    player_a_result_path="/tmp/old-riftbound-human-playtest/player-a-result.png"
    player_b_result_path="/tmp/old-riftbound-human-playtest/player-b-result.png"
  fi

  mkdir -p "${bundle_dir}"
  cat >"${bundle_dir}/README.md" <<'EOF'
# Riftbound Godot Human Playtest Evidence

Package integrity fixture.
- Machine inksteel style: passed
- Machine hidden-information boundary: both client logs report zero opponent hand faces and zero hidden identity leaks
EOF

  cat >"${bundle_dir}/player-a.log" <<'EOF'
Authenticate: Registered (player-a-fixture).
JoinRoom requested: room=fixture-room, player=player-a-fixture.
[b]Joined[/b] type=JOIN room=fixture-room player=player-a-fixture tick=0 payload=Object
Preconstructed decks loaded: 9.
SubmitDeck receipt accepted=True state=ACCEPTED
Ready receipt accepted=True state=ACCEPTED
MATCH_STARTED
Hidden info boundary ok: opponentHandFaces=0 opponentHandBacks=4 opponentStandbyFaces=0 opponentStandbyBacks=0 hiddenCardIdentityLeaks=0
Match result rendered
Visual screenshot saved: /tmp/riftbound-human-playtest/player-a-result.png
EOF

  cat >"${bundle_dir}/player-b.log" <<'EOF'
Authenticate: Registered (player-b-fixture).
JoinRoom requested: room=fixture-room, player=player-b-fixture.
[b]Joined[/b] type=JOIN room=fixture-room player=player-b-fixture tick=0 payload=Object
Preconstructed decks loaded: 9.
SubmitDeck receipt accepted=True state=ACCEPTED
Ready receipt accepted=True state=ACCEPTED
MATCH_STARTED
Hidden info boundary ok: opponentHandFaces=0 opponentHandBacks=4 opponentStandbyFaces=0 opponentStandbyBacks=0 hiddenCardIdentityLeaks=0
MATCH_WON
Visual screenshot saved: /tmp/riftbound-human-playtest/player-b-result.png
EOF

  if [[ "${duplicate_logs}" == "1" ]]; then
    cat >"${bundle_dir}/player-a.log" <<'EOF'
Authenticate: Registered (player-a-fixture).
JoinRoom requested: room=fixture-room, player=player-a-fixture.
[b]Joined[/b] type=JOIN room=fixture-room player=player-a-fixture tick=0 payload=Object
Preconstructed decks loaded: 9.
SubmitDeck receipt accepted=True state=ACCEPTED
Ready receipt accepted=True state=ACCEPTED
MATCH_STARTED
Hidden info boundary ok: opponentHandFaces=0 opponentHandBacks=4 opponentStandbyFaces=0 opponentStandbyBacks=0 hiddenCardIdentityLeaks=0
MATCH_WON
Visual screenshot saved: /tmp/riftbound-human-playtest/player-a-result.png
Visual screenshot saved: /tmp/riftbound-human-playtest/player-b-result.png
EOF
    cp "${bundle_dir}/player-a.log" "${bundle_dir}/player-b.log"
  fi

  if [[ "${missing_deck_ready}" == "1" ]]; then
    cat >"${bundle_dir}/player-a.log" <<'EOF'
Authenticate: Registered (player-a-fixture).
JoinRoom requested: room=fixture-room, player=player-a-fixture.
[b]Joined[/b] type=JOIN room=fixture-room player=player-a-fixture tick=0 payload=Object
MATCH_STARTED
Hidden info boundary ok: opponentHandFaces=0 opponentHandBacks=4 opponentStandbyFaces=0 opponentStandbyBacks=0 hiddenCardIdentityLeaks=0
Match result rendered
Visual screenshot saved: /tmp/riftbound-human-playtest/player-a-result.png
EOF
    cat >"${bundle_dir}/player-b.log" <<'EOF'
Authenticate: Registered (player-b-fixture).
JoinRoom requested: room=fixture-room, player=player-b-fixture.
[b]Joined[/b] type=JOIN room=fixture-room player=player-b-fixture tick=0 payload=Object
MATCH_STARTED
Hidden info boundary ok: opponentHandFaces=0 opponentHandBacks=4 opponentStandbyFaces=0 opponentStandbyBacks=0 hiddenCardIdentityLeaks=0
MATCH_WON
Visual screenshot saved: /tmp/riftbound-human-playtest/player-b-result.png
EOF
  fi

  if [[ "${duplicate_screenshots}" == "1" ]]; then
    write_result_png "${bundle_dir}/player-a-result.png" "" "${screenshot_size}"
    write_result_png "${bundle_dir}/player-b-result.png" "" "${screenshot_size}"
  else
    write_result_png "${bundle_dir}/player-a-result.png" "player-a" "${screenshot_size}"
    write_result_png "${bundle_dir}/player-b-result.png" "player-b" "${screenshot_size}"
  fi

  cat >"${bundle_dir}/playtest-report.md" <<EOF
# Riftbound Godot Human Playtest Report

- Git revision: ${revision}
- Git worktree: clean
- Require clean git: 1
- Incomplete human evidence: ${incomplete_human_evidence}
- Room: ${room_id}
- Player A handle: ${player_a_handle}
- Player B handle: ${player_b_handle}
- Player A result screenshot: ${player_a_result_path}
- Player B result screenshot: ${player_b_result_path}

## Machine Check

- Status: passed
- Inksteel style: passed
- Hidden information boundary: both client logs report zero opponent hand faces and zero hidden identity leaks
- Manual confirmation mode: ${manual_confirmation_mode}

## Manual Confirmations

- [x] Two human players operated the two Godot clients.
- [x] Player A final screenshot shows the server result panel.
- [x] Player B final screenshot shows the server result panel.
- [x] Player A sees opponent hand/hidden cards only as card backs and counts.
- [x] Player B sees opponent hand/hidden cards only as card backs and counts.
EOF

  cat >"${bundle_dir}/P5_HANDOFF.md" <<EOF
# Riftbound Godot P5 Handoff

- Git revision: ${revision}
- Room: ${room_id}
- Player A handle: ${player_a_handle}
- Player B handle: ${player_b_handle}
- Player A result screenshot: player-a-result.png
- Player B result screenshot: player-b-result.png
- Report: playtest-report.md
- Inksteel style: passed
- Hidden information boundary: both client logs report zero opponent hand faces and zero hidden identity leaks
- Manual confirmation mode: ${manual_confirmation_mode}

This handoff summary is machine generated from the playtest report and is only
valid final P5 evidence when playtest-report.md has all manual confirmations
checked after a real two-human Godot match.
EOF

  cat >"${bundle_dir}/VISUAL_REVIEW.md" <<EOF
# Riftbound Godot Visual Review

- Room: ${room_id}
- Player A handle: ${player_a_handle}
- Player B handle: ${player_b_handle}
- Player A result screenshot: player-a-result.png
- Player B result screenshot: player-b-result.png
- Report: playtest-report.md
- Machine inksteel style: passed
- Machine hidden-information boundary: both client logs report zero opponent hand faces and zero hidden identity leaks

- Both screenshots show the server result panel.
- Player A sees opponent hand and hidden cards only as card backs and counts.
- Player B sees opponent hand and hidden cards only as card backs and counts.
- No opponent hidden card face, name, text, or identity is visible in either screenshot.
EOF

  cat >"${bundle_dir}/OPERATOR_GUIDE.md" <<EOF
# Riftbound Godot P5 Operator Guide

- Room: ${room_id}
- Player A handle: ${player_a_handle}
- Player B handle: ${player_b_handle}
- Evidence directory: /tmp/riftbound-human-playtest
- Evidence package: /tmp/riftbound-human-playtest.tar.gz
- Playtest report: /tmp/riftbound-human-playtest/playtest-report.md

## Final P5 operator checklist

1. Two human players operate the two Godot clients.
2. Both players use preconstructed decks, submit decks, and ready up.
3. Play the match to the server result panel on both clients.
4. Confirm each player sees opponent hand and hidden cards only as card backs/counts.
EOF

  if [[ "${extra_file}" == "1" ]]; then
    printf 'unexpected package payload\n' >"${bundle_dir}/secret.txt"
  fi
}

make_package() {
  local bundle_dir="$1"
  local package_path="$2"
  local staging_dir

  staging_dir="$(dirname "${bundle_dir}")"
  tar -czf "${package_path}" -C "${staging_dir}" riftbound-human-playtest-evidence
}

tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/riftbound-package-integrity.XXXXXX")"
trap 'rm -rf "${tmp_dir}"' EXIT

revision="$(git -C "${repo_root}" rev-parse --short HEAD)"

missing_checksum_bundle="${tmp_dir}/missing/riftbound-human-playtest-evidence"
write_evidence_bundle "${missing_checksum_bundle}" "${revision}"
(
  cd "${missing_checksum_bundle}"
  shasum -a 256 OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
missing_checksum_package="${tmp_dir}/missing-readme-checksum.tar.gz"
make_package "${missing_checksum_bundle}" "${missing_checksum_package}"

missing_output="${tmp_dir}/missing-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${missing_checksum_package}" >"${missing_output}" 2>&1; then
  fail "verifier accepted package whose SHA256SUMS did not cover README.md"
fi

if ! rg -q "SHA256SUMS.*README\\.md|README\\.md.*SHA256SUMS" "${missing_output}"; then
  echo "Expected checksum coverage rejection output:" >&2
  cat "${missing_output}" >&2
  fail "verifier did not explain the missing README.md checksum coverage"
fi

manual_mode_bundle="${tmp_dir}/manual-mode/riftbound-human-playtest-evidence"
write_evidence_bundle "${manual_mode_bundle}" "${revision}" "0"
(
  cd "${manual_mode_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
manual_mode_package="${tmp_dir}/manual-mode-zero.tar.gz"
make_package "${manual_mode_bundle}" "${manual_mode_package}"

manual_mode_output="${tmp_dir}/manual-mode-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${manual_mode_package}" >"${manual_mode_output}" 2>&1; then
  fail "verifier accepted package whose report was not produced with manual confirmation mode"
fi

if ! rg -q "Manual confirmation mode" "${manual_mode_output}"; then
  echo "Expected manual confirmation mode rejection output:" >&2
  cat "${manual_mode_output}" >&2
  fail "verifier did not explain the missing manual confirmation mode"
fi

incomplete_bundle="${tmp_dir}/incomplete/riftbound-human-playtest-evidence"
write_evidence_bundle "${incomplete_bundle}" "${revision}" "1" "0" "0" "0" "0" "full" "match" "1"
(
  cd "${incomplete_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
incomplete_package="${tmp_dir}/incomplete.tar.gz"
make_package "${incomplete_bundle}" "${incomplete_package}"

incomplete_output="${tmp_dir}/incomplete-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${incomplete_package}" >"${incomplete_output}" 2>&1; then
  fail "verifier accepted package marked as incomplete human evidence"
fi

if ! rg -q "Incomplete human evidence|incomplete human evidence" "${incomplete_output}"; then
  echo "Expected incomplete marker rejection output:" >&2
  cat "${incomplete_output}" >&2
  fail "verifier did not explain the incomplete human evidence marker"
fi

duplicate_screenshot_bundle="${tmp_dir}/duplicate-screenshot/riftbound-human-playtest-evidence"
write_evidence_bundle "${duplicate_screenshot_bundle}" "${revision}" "1" "1"
(
  cd "${duplicate_screenshot_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
duplicate_screenshot_package="${tmp_dir}/duplicate-screenshot.tar.gz"
make_package "${duplicate_screenshot_bundle}" "${duplicate_screenshot_package}"

duplicate_screenshot_output="${tmp_dir}/duplicate-screenshot-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${duplicate_screenshot_package}" >"${duplicate_screenshot_output}" 2>&1; then
  fail "verifier accepted package with identical player A/B result screenshots"
fi

if ! rg -q "result screenshots.*identical|identical.*result screenshots" "${duplicate_screenshot_output}"; then
  echo "Expected duplicate screenshot rejection output:" >&2
  cat "${duplicate_screenshot_output}" >&2
  fail "verifier did not explain the duplicate result screenshots"
fi

duplicate_log_bundle="${tmp_dir}/duplicate-log/riftbound-human-playtest-evidence"
write_evidence_bundle "${duplicate_log_bundle}" "${revision}" "1" "0" "1"
(
  cd "${duplicate_log_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
duplicate_log_package="${tmp_dir}/duplicate-log.tar.gz"
make_package "${duplicate_log_bundle}" "${duplicate_log_package}"

duplicate_log_output="${tmp_dir}/duplicate-log-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${duplicate_log_package}" >"${duplicate_log_output}" 2>&1; then
  fail "verifier accepted package with identical player A/B logs"
fi

if ! rg -q "player A and player B logs are identical|logs.*identical|identical.*logs" "${duplicate_log_output}"; then
  echo "Expected duplicate log rejection output:" >&2
  cat "${duplicate_log_output}" >&2
  fail "verifier did not explain the duplicate player logs"
fi

duplicate_identity_bundle="${tmp_dir}/duplicate-identity/riftbound-human-playtest-evidence"
write_evidence_bundle "${duplicate_identity_bundle}" "${revision}"
cat >"${duplicate_identity_bundle}/player-b.log" <<'EOF'
Authenticate: Registered (player-a-fixture).
JoinRoom requested: room=fixture-room, player=player-a-fixture.
[b]Joined[/b] type=JOIN room=fixture-room player=player-a-fixture tick=0 payload=Object
Preconstructed decks loaded: 9.
SubmitDeck receipt accepted=True state=ACCEPTED
Ready receipt accepted=True state=ACCEPTED
MATCH_STARTED
Hidden info boundary ok: opponentHandFaces=0 opponentHandBacks=4 opponentStandbyFaces=0 opponentStandbyBacks=0 hiddenCardIdentityLeaks=0
MATCH_WON
Visual screenshot saved: /tmp/riftbound-human-playtest/player-b-result.png
EOF
awk '{
  if ($0 == "- Player B handle: player-b-fixture") {
    print "- Player B handle: player-a-fixture"
  } else {
    print
  }
}' "${duplicate_identity_bundle}/playtest-report.md" >"${duplicate_identity_bundle}/playtest-report.md.tmp"
mv "${duplicate_identity_bundle}/playtest-report.md.tmp" "${duplicate_identity_bundle}/playtest-report.md"
(
  cd "${duplicate_identity_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
duplicate_identity_package="${tmp_dir}/duplicate-identity.tar.gz"
make_package "${duplicate_identity_bundle}" "${duplicate_identity_package}"

duplicate_identity_output="${tmp_dir}/duplicate-identity-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${duplicate_identity_package}" >"${duplicate_identity_output}" 2>&1; then
  fail "verifier accepted package with duplicate player identities"
fi

if ! rg -q "Player A handle|Player B handle|distinct|duplicate" "${duplicate_identity_output}"; then
  echo "Expected duplicate identity rejection output:" >&2
  cat "${duplicate_identity_output}" >&2
  fail "verifier did not explain the duplicate player identities"
fi

missing_handoff_bundle="${tmp_dir}/missing-handoff/riftbound-human-playtest-evidence"
write_evidence_bundle "${missing_handoff_bundle}" "${revision}"
rm -f "${missing_handoff_bundle}/P5_HANDOFF.md"
(
  cd "${missing_handoff_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md > SHA256SUMS
)
missing_handoff_package="${tmp_dir}/missing-handoff.tar.gz"
make_package "${missing_handoff_bundle}" "${missing_handoff_package}"

missing_handoff_output="${tmp_dir}/missing-handoff-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${missing_handoff_package}" >"${missing_handoff_output}" 2>&1; then
  fail "verifier accepted package without P5 handoff summary"
fi

if ! rg -q "P5_HANDOFF|handoff" "${missing_handoff_output}"; then
  echo "Expected missing handoff rejection output:" >&2
  cat "${missing_handoff_output}" >&2
  fail "verifier did not explain the missing P5 handoff summary"
fi

missing_visual_review_bundle="${tmp_dir}/missing-visual-review/riftbound-human-playtest-evidence"
write_evidence_bundle "${missing_visual_review_bundle}" "${revision}"
rm -f "${missing_visual_review_bundle}/VISUAL_REVIEW.md"
(
  cd "${missing_visual_review_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
missing_visual_review_package="${tmp_dir}/missing-visual-review.tar.gz"
make_package "${missing_visual_review_bundle}" "${missing_visual_review_package}"

missing_visual_review_output="${tmp_dir}/missing-visual-review-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${missing_visual_review_package}" >"${missing_visual_review_output}" 2>&1; then
  fail "verifier accepted package without visual review checklist"
fi

if ! rg -q "VISUAL_REVIEW|visual review" "${missing_visual_review_output}"; then
  echo "Expected missing visual review rejection output:" >&2
  cat "${missing_visual_review_output}" >&2
  fail "verifier did not explain the missing visual review checklist"
fi

missing_handoff_hidden_boundary_bundle="${tmp_dir}/missing-handoff-hidden-boundary/riftbound-human-playtest-evidence"
write_evidence_bundle "${missing_handoff_hidden_boundary_bundle}" "${revision}"
sed -i '' '/Hidden information boundary/d' "${missing_handoff_hidden_boundary_bundle}/P5_HANDOFF.md"
(
  cd "${missing_handoff_hidden_boundary_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
missing_handoff_hidden_boundary_package="${tmp_dir}/missing-handoff-hidden-boundary.tar.gz"
make_package "${missing_handoff_hidden_boundary_bundle}" "${missing_handoff_hidden_boundary_package}"

missing_handoff_hidden_boundary_output="${tmp_dir}/missing-handoff-hidden-boundary-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${missing_handoff_hidden_boundary_package}" >"${missing_handoff_hidden_boundary_output}" 2>&1; then
  fail "verifier accepted package whose P5 handoff omitted hidden information boundary evidence"
fi

if ! rg -q "Hidden information boundary|hidden information" "${missing_handoff_hidden_boundary_output}"; then
  echo "Expected handoff hidden-boundary rejection output:" >&2
  cat "${missing_handoff_hidden_boundary_output}" >&2
  fail "verifier did not explain the missing P5 handoff hidden information boundary"
fi

missing_visual_hidden_boundary_bundle="${tmp_dir}/missing-visual-hidden-boundary/riftbound-human-playtest-evidence"
write_evidence_bundle "${missing_visual_hidden_boundary_bundle}" "${revision}"
sed -i '' '/Machine hidden-information boundary/d' "${missing_visual_hidden_boundary_bundle}/VISUAL_REVIEW.md"
(
  cd "${missing_visual_hidden_boundary_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
missing_visual_hidden_boundary_package="${tmp_dir}/missing-visual-hidden-boundary.tar.gz"
make_package "${missing_visual_hidden_boundary_bundle}" "${missing_visual_hidden_boundary_package}"

missing_visual_hidden_boundary_output="${tmp_dir}/missing-visual-hidden-boundary-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${missing_visual_hidden_boundary_package}" >"${missing_visual_hidden_boundary_output}" 2>&1; then
  fail "verifier accepted package whose visual review omitted hidden information boundary evidence"
fi

if ! rg -q "Machine hidden-information boundary|hidden information" "${missing_visual_hidden_boundary_output}"; then
  echo "Expected visual-review hidden-boundary rejection output:" >&2
  cat "${missing_visual_hidden_boundary_output}" >&2
  fail "verifier did not explain the missing visual review hidden information boundary"
fi

missing_readme_hidden_boundary_bundle="${tmp_dir}/missing-readme-hidden-boundary/riftbound-human-playtest-evidence"
write_evidence_bundle "${missing_readme_hidden_boundary_bundle}" "${revision}"
sed -i '' '/Machine hidden-information boundary/d' "${missing_readme_hidden_boundary_bundle}/README.md"
(
  cd "${missing_readme_hidden_boundary_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
missing_readme_hidden_boundary_package="${tmp_dir}/missing-readme-hidden-boundary.tar.gz"
make_package "${missing_readme_hidden_boundary_bundle}" "${missing_readme_hidden_boundary_package}"

missing_readme_hidden_boundary_output="${tmp_dir}/missing-readme-hidden-boundary-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${missing_readme_hidden_boundary_package}" >"${missing_readme_hidden_boundary_output}" 2>&1; then
  fail "verifier accepted package whose README omitted hidden information boundary evidence"
fi

if ! rg -q "README.*hidden information|hidden information.*README" "${missing_readme_hidden_boundary_output}"; then
  echo "Expected README hidden-boundary rejection output:" >&2
  cat "${missing_readme_hidden_boundary_output}" >&2
  fail "verifier did not explain the missing README hidden information boundary"
fi

missing_handoff_inksteel_style_bundle="${tmp_dir}/missing-handoff-inksteel-style/riftbound-human-playtest-evidence"
write_evidence_bundle "${missing_handoff_inksteel_style_bundle}" "${revision}"
sed -i '' '/Inksteel style/d' "${missing_handoff_inksteel_style_bundle}/P5_HANDOFF.md"
(
  cd "${missing_handoff_inksteel_style_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
missing_handoff_inksteel_style_package="${tmp_dir}/missing-handoff-inksteel-style.tar.gz"
make_package "${missing_handoff_inksteel_style_bundle}" "${missing_handoff_inksteel_style_package}"

missing_handoff_inksteel_style_output="${tmp_dir}/missing-handoff-inksteel-style-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${missing_handoff_inksteel_style_package}" >"${missing_handoff_inksteel_style_output}" 2>&1; then
  fail "verifier accepted package whose P5 handoff omitted inksteel style evidence"
fi

if ! rg -q "Inksteel style|inksteel" "${missing_handoff_inksteel_style_output}"; then
  echo "Expected handoff inksteel-style rejection output:" >&2
  cat "${missing_handoff_inksteel_style_output}" >&2
  fail "verifier did not explain the missing P5 handoff inksteel style"
fi

missing_visual_inksteel_style_bundle="${tmp_dir}/missing-visual-inksteel-style/riftbound-human-playtest-evidence"
write_evidence_bundle "${missing_visual_inksteel_style_bundle}" "${revision}"
sed -i '' '/Machine inksteel style/d' "${missing_visual_inksteel_style_bundle}/VISUAL_REVIEW.md"
(
  cd "${missing_visual_inksteel_style_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
missing_visual_inksteel_style_package="${tmp_dir}/missing-visual-inksteel-style.tar.gz"
make_package "${missing_visual_inksteel_style_bundle}" "${missing_visual_inksteel_style_package}"

missing_visual_inksteel_style_output="${tmp_dir}/missing-visual-inksteel-style-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${missing_visual_inksteel_style_package}" >"${missing_visual_inksteel_style_output}" 2>&1; then
  fail "verifier accepted package whose visual review omitted inksteel style evidence"
fi

if ! rg -q "Machine inksteel style|inksteel" "${missing_visual_inksteel_style_output}"; then
  echo "Expected visual-review inksteel-style rejection output:" >&2
  cat "${missing_visual_inksteel_style_output}" >&2
  fail "verifier did not explain the missing visual review inksteel style"
fi

missing_readme_inksteel_style_bundle="${tmp_dir}/missing-readme-inksteel-style/riftbound-human-playtest-evidence"
write_evidence_bundle "${missing_readme_inksteel_style_bundle}" "${revision}"
sed -i '' '/Machine inksteel style/d' "${missing_readme_inksteel_style_bundle}/README.md"
(
  cd "${missing_readme_inksteel_style_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
missing_readme_inksteel_style_package="${tmp_dir}/missing-readme-inksteel-style.tar.gz"
make_package "${missing_readme_inksteel_style_bundle}" "${missing_readme_inksteel_style_package}"

missing_readme_inksteel_style_output="${tmp_dir}/missing-readme-inksteel-style-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${missing_readme_inksteel_style_package}" >"${missing_readme_inksteel_style_output}" 2>&1; then
  fail "verifier accepted package whose README omitted inksteel style evidence"
fi

if ! rg -q "README.*inksteel|inksteel.*README" "${missing_readme_inksteel_style_output}"; then
  echo "Expected README inksteel-style rejection output:" >&2
  cat "${missing_readme_inksteel_style_output}" >&2
  fail "verifier did not explain the missing README inksteel style"
fi

missing_operator_guide_bundle="${tmp_dir}/missing-operator-guide/riftbound-human-playtest-evidence"
write_evidence_bundle "${missing_operator_guide_bundle}" "${revision}"
rm -f "${missing_operator_guide_bundle}/OPERATOR_GUIDE.md"
(
  cd "${missing_operator_guide_bundle}"
  shasum -a 256 README.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
missing_operator_guide_package="${tmp_dir}/missing-operator-guide.tar.gz"
make_package "${missing_operator_guide_bundle}" "${missing_operator_guide_package}"

missing_operator_guide_output="${tmp_dir}/missing-operator-guide-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${missing_operator_guide_package}" >"${missing_operator_guide_output}" 2>&1; then
  fail "verifier accepted package without operator guide"
fi

if ! rg -q "OPERATOR_GUIDE|operator guide" "${missing_operator_guide_output}"; then
  echo "Expected missing operator guide rejection output:" >&2
  cat "${missing_operator_guide_output}" >&2
  fail "verifier did not explain the missing operator guide"
fi

operator_guide_bundle="${tmp_dir}/operator-guide/riftbound-human-playtest-evidence"
write_evidence_bundle "${operator_guide_bundle}" "${revision}"
cat >"${operator_guide_bundle}/OPERATOR_GUIDE.md" <<'EOF'
# Riftbound Godot P5 Operator Guide

- Room: fixture-room
- Player A handle: player-a-fixture
- Player B handle: player-b-fixture
- Evidence directory: /tmp/riftbound-human-playtest
- Evidence package: /tmp/riftbound-human-playtest.tar.gz
- Playtest report: /tmp/riftbound-human-playtest/playtest-report.md

## Final P5 operator checklist

1. Two human players operate the two Godot clients.
2. Both players use preconstructed decks, submit decks, and ready up.
3. Play the match to the server result panel on both clients.
4. Confirm each player sees opponent hand and hidden cards only as card backs/counts.
EOF
(
  cd "${operator_guide_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
operator_guide_package="${tmp_dir}/operator-guide.tar.gz"
make_package "${operator_guide_bundle}" "${operator_guide_package}"

"${script_dir}/verify-human-playtest-package.sh" "${operator_guide_package}" >/dev/null

missing_operator_package_path_bundle="${tmp_dir}/missing-operator-package-path/riftbound-human-playtest-evidence"
write_evidence_bundle "${missing_operator_package_path_bundle}" "${revision}"
python3 - "${missing_operator_package_path_bundle}/OPERATOR_GUIDE.md" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
lines = path.read_text(encoding="utf-8").splitlines()
path.write_text(
    "\n".join(line for line in lines if not line.startswith("- Evidence package: ")) + "\n",
    encoding="utf-8",
)
PY
(
  cd "${missing_operator_package_path_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
missing_operator_package_path_package="${tmp_dir}/missing-operator-package-path.tar.gz"
make_package "${missing_operator_package_path_bundle}" "${missing_operator_package_path_package}"

missing_operator_package_path_output="${tmp_dir}/missing-operator-package-path-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${missing_operator_package_path_package}" >"${missing_operator_package_path_output}" 2>&1; then
  fail "verifier accepted operator guide without the evidence package path"
fi

if ! rg -q "OPERATOR_GUIDE|operator guide|Evidence package|evidence package" "${missing_operator_package_path_output}"; then
  echo "Expected missing operator evidence package rejection output:" >&2
  cat "${missing_operator_package_path_output}" >&2
  fail "verifier did not explain the missing operator evidence package path"
fi

missing_operator_report_path_bundle="${tmp_dir}/missing-operator-report-path/riftbound-human-playtest-evidence"
write_evidence_bundle "${missing_operator_report_path_bundle}" "${revision}"
python3 - "${missing_operator_report_path_bundle}/OPERATOR_GUIDE.md" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
lines = path.read_text(encoding="utf-8").splitlines()
path.write_text(
    "\n".join(line for line in lines if not line.startswith("- Playtest report: ")) + "\n",
    encoding="utf-8",
)
PY
(
  cd "${missing_operator_report_path_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
missing_operator_report_path_package="${tmp_dir}/missing-operator-report-path.tar.gz"
make_package "${missing_operator_report_path_bundle}" "${missing_operator_report_path_package}"

missing_operator_report_path_output="${tmp_dir}/missing-operator-report-path-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${missing_operator_report_path_package}" >"${missing_operator_report_path_output}" 2>&1; then
  fail "verifier accepted operator guide without the playtest report path"
fi

if ! rg -q "OPERATOR_GUIDE|operator guide|Playtest report|playtest report" "${missing_operator_report_path_output}"; then
  echo "Expected missing operator playtest report rejection output:" >&2
  cat "${missing_operator_report_path_output}" >&2
  fail "verifier did not explain the missing operator playtest report path"
fi

placeholder_operator_package_path_bundle="${tmp_dir}/placeholder-operator-package-path/riftbound-human-playtest-evidence"
write_evidence_bundle "${placeholder_operator_package_path_bundle}" "${revision}"
python3 - "${placeholder_operator_package_path_bundle}/OPERATOR_GUIDE.md" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
path.write_text(text.replace("- Evidence package: /tmp/riftbound-human-playtest.tar.gz", "- Evidence package: TBD"), encoding="utf-8")
PY
(
  cd "${placeholder_operator_package_path_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
placeholder_operator_package_path_package="${tmp_dir}/placeholder-operator-package-path.tar.gz"
make_package "${placeholder_operator_package_path_bundle}" "${placeholder_operator_package_path_package}"

placeholder_operator_package_path_output="${tmp_dir}/placeholder-operator-package-path-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${placeholder_operator_package_path_package}" >"${placeholder_operator_package_path_output}" 2>&1; then
  fail "verifier accepted operator guide with a placeholder evidence package path"
fi

if ! rg -q "OPERATOR_GUIDE|operator guide|Evidence package|evidence package|tar\\.gz" "${placeholder_operator_package_path_output}"; then
  echo "Expected placeholder operator evidence package rejection output:" >&2
  cat "${placeholder_operator_package_path_output}" >&2
  fail "verifier did not explain the placeholder operator evidence package path"
fi

wrong_operator_report_path_bundle="${tmp_dir}/wrong-operator-report-path/riftbound-human-playtest-evidence"
write_evidence_bundle "${wrong_operator_report_path_bundle}" "${revision}"
python3 - "${wrong_operator_report_path_bundle}/OPERATOR_GUIDE.md" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
path.write_text(text.replace("- Playtest report: /tmp/riftbound-human-playtest/playtest-report.md", "- Playtest report: notes.txt"), encoding="utf-8")
PY
(
  cd "${wrong_operator_report_path_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
wrong_operator_report_path_package="${tmp_dir}/wrong-operator-report-path.tar.gz"
make_package "${wrong_operator_report_path_bundle}" "${wrong_operator_report_path_package}"

wrong_operator_report_path_output="${tmp_dir}/wrong-operator-report-path-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${wrong_operator_report_path_package}" >"${wrong_operator_report_path_output}" 2>&1; then
  fail "verifier accepted operator guide with a non-report playtest report path"
fi

if ! rg -q "OPERATOR_GUIDE|operator guide|Playtest report|playtest report|playtest-report\\.md" "${wrong_operator_report_path_output}"; then
  echo "Expected wrong operator playtest report rejection output:" >&2
  cat "${wrong_operator_report_path_output}" >&2
  fail "verifier did not explain the wrong operator playtest report path"
fi

extra_file_bundle="${tmp_dir}/extra-file/riftbound-human-playtest-evidence"
write_evidence_bundle "${extra_file_bundle}" "${revision}" "1" "0" "0" "1"
(
  cd "${extra_file_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
extra_file_package="${tmp_dir}/extra-file.tar.gz"
make_package "${extra_file_bundle}" "${extra_file_package}"

extra_file_output="${tmp_dir}/extra-file-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${extra_file_package}" >"${extra_file_output}" 2>&1; then
  fail "verifier accepted package with an unexpected extra file"
fi

if ! rg -q "unexpected file|extra file|secret\\.txt" "${extra_file_output}"; then
  echo "Expected unexpected-file rejection output:" >&2
  cat "${extra_file_output}" >&2
  fail "verifier did not explain the unexpected package file"
fi

missing_deck_ready_bundle="${tmp_dir}/missing-deck-ready/riftbound-human-playtest-evidence"
write_evidence_bundle "${missing_deck_ready_bundle}" "${revision}" "1" "0" "0" "0" "1"
(
  cd "${missing_deck_ready_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
missing_deck_ready_package="${tmp_dir}/missing-deck-ready.tar.gz"
make_package "${missing_deck_ready_bundle}" "${missing_deck_ready_package}"

missing_deck_ready_output="${tmp_dir}/missing-deck-ready-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${missing_deck_ready_package}" >"${missing_deck_ready_output}" 2>&1; then
  fail "verifier accepted package without preconstructed deck submit/ready evidence"
fi

if ! rg -q "Preconstructed|SubmitDeck|Ready" "${missing_deck_ready_output}"; then
  echo "Expected deck/ready rejection output:" >&2
  cat "${missing_deck_ready_output}" >&2
  fail "verifier did not explain the missing deck/ready evidence"
fi

missing_hidden_boundary_bundle="${tmp_dir}/missing-hidden-boundary/riftbound-human-playtest-evidence"
write_evidence_bundle "${missing_hidden_boundary_bundle}" "${revision}"
sed -i '' '/Hidden info boundary/d' "${missing_hidden_boundary_bundle}/player-a.log" "${missing_hidden_boundary_bundle}/player-b.log"
(
  cd "${missing_hidden_boundary_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
missing_hidden_boundary_package="${tmp_dir}/missing-hidden-boundary.tar.gz"
make_package "${missing_hidden_boundary_bundle}" "${missing_hidden_boundary_package}"

missing_hidden_boundary_output="${tmp_dir}/missing-hidden-boundary-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${missing_hidden_boundary_package}" >"${missing_hidden_boundary_output}" 2>&1; then
  fail "verifier accepted package without hidden information boundary evidence"
fi

if ! rg -q "Hidden info boundary|hidden information" "${missing_hidden_boundary_output}"; then
  echo "Expected hidden boundary rejection output:" >&2
  cat "${missing_hidden_boundary_output}" >&2
  fail "verifier did not explain the missing hidden information boundary evidence"
fi

hidden_leak_bundle="${tmp_dir}/hidden-leak/riftbound-human-playtest-evidence"
write_evidence_bundle "${hidden_leak_bundle}" "${revision}"
printf 'Hidden info boundary VIOLATION: opponentHandFaces=1 opponentHandBacks=4 opponentStandbyFaces=0 opponentStandbyBacks=0 hiddenCardIdentityLeaks=1\n' >>"${hidden_leak_bundle}/player-a.log"
(
  cd "${hidden_leak_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
hidden_leak_package="${tmp_dir}/hidden-leak.tar.gz"
make_package "${hidden_leak_bundle}" "${hidden_leak_package}"

hidden_leak_output="${tmp_dir}/hidden-leak-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${hidden_leak_package}" >"${hidden_leak_output}" 2>&1; then
  fail "verifier accepted package with hidden information boundary violation"
fi

if ! rg -q "Hidden info boundary|hidden information|identity leak" "${hidden_leak_output}"; then
  echo "Expected hidden leak rejection output:" >&2
  cat "${hidden_leak_output}" >&2
  fail "verifier did not explain the hidden information boundary violation"
fi

missing_inksteel_style_bundle="${tmp_dir}/missing-inksteel-style/riftbound-human-playtest-evidence"
write_evidence_bundle "${missing_inksteel_style_bundle}" "${revision}"
sed -i '' '/Inksteel style/d' "${missing_inksteel_style_bundle}/playtest-report.md"
(
  cd "${missing_inksteel_style_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
missing_inksteel_style_package="${tmp_dir}/missing-inksteel-style.tar.gz"
make_package "${missing_inksteel_style_bundle}" "${missing_inksteel_style_package}"

missing_inksteel_style_output="${tmp_dir}/missing-inksteel-style-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${missing_inksteel_style_package}" >"${missing_inksteel_style_output}" 2>&1; then
  fail "verifier accepted package without inksteel style evidence"
fi

if ! rg -q "Inksteel style|inksteel" "${missing_inksteel_style_output}"; then
  echo "Expected inksteel style rejection output:" >&2
  cat "${missing_inksteel_style_output}" >&2
  fail "verifier did not explain the missing inksteel style evidence"
fi

mismatched_screenshot_path_bundle="${tmp_dir}/mismatched-screenshot-path/riftbound-human-playtest-evidence"
write_evidence_bundle "${mismatched_screenshot_path_bundle}" "${revision}" "1" "0" "0" "0" "0" "full" "mismatch"
(
  cd "${mismatched_screenshot_path_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
mismatched_screenshot_path_package="${tmp_dir}/mismatched-screenshot-path.tar.gz"
make_package "${mismatched_screenshot_path_bundle}" "${mismatched_screenshot_path_package}"

mismatched_screenshot_path_output="${tmp_dir}/mismatched-screenshot-path-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${mismatched_screenshot_path_package}" >"${mismatched_screenshot_path_output}" 2>&1; then
  fail "verifier accepted package whose report and logs disagree on result screenshot paths"
fi

if ! rg -q "screenshot path|result screenshot log|Player A result screenshot|Player B result screenshot" "${mismatched_screenshot_path_output}"; then
  echo "Expected screenshot path mismatch rejection output:" >&2
  cat "${mismatched_screenshot_path_output}" >&2
  fail "verifier did not explain the mismatched result screenshot paths"
fi

small_screenshot_bundle="${tmp_dir}/small-screenshot/riftbound-human-playtest-evidence"
write_evidence_bundle "${small_screenshot_bundle}" "${revision}" "1" "0" "0" "0" "0" "small"
(
  cd "${small_screenshot_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
small_screenshot_package="${tmp_dir}/small-screenshot.tar.gz"
make_package "${small_screenshot_bundle}" "${small_screenshot_package}"

small_screenshot_output="${tmp_dir}/small-screenshot-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${small_screenshot_package}" >"${small_screenshot_output}" 2>&1; then
  fail "verifier accepted package with too-small result screenshots"
fi

if ! rg -q "screenshot.*too small|too small.*screenshot|minimum" "${small_screenshot_output}"; then
  echo "Expected small screenshot rejection output:" >&2
  cat "${small_screenshot_output}" >&2
  fail "verifier did not explain the too-small result screenshots"
fi

bright_style_bundle="${tmp_dir}/bright-style/riftbound-human-playtest-evidence"
write_evidence_bundle "${bright_style_bundle}" "${revision}" "1" "0" "0" "0" "0" "bright"
(
  cd "${bright_style_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
bright_style_package="${tmp_dir}/bright-style.tar.gz"
make_package "${bright_style_bundle}" "${bright_style_package}"

bright_style_output="${tmp_dir}/bright-style-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${bright_style_package}" >"${bright_style_output}" 2>&1; then
  fail "verifier accepted package whose result screenshots drift from inksteel style"
fi

if ! rg -q "inksteel|style|bright|screenshot" "${bright_style_output}"; then
  echo "Expected package screenshot style rejection output:" >&2
  cat "${bright_style_output}" >&2
  fail "verifier did not explain the inksteel screenshot style rejection"
fi

covered_bundle="${tmp_dir}/covered/riftbound-human-playtest-evidence"
write_evidence_bundle "${covered_bundle}" "${revision}" "1" "0" "0" "0" "0"
(
  cd "${covered_bundle}"
  shasum -a 256 README.md OPERATOR_GUIDE.md VISUAL_REVIEW.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md P5_HANDOFF.md > SHA256SUMS
)
covered_package="${tmp_dir}/covered-checksum.tar.gz"
make_package "${covered_bundle}" "${covered_package}"

"${script_dir}/verify-human-playtest-package.sh" "${covered_package}" >/dev/null

echo "Human playtest package integrity checks passed."
