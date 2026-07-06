#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
scene_path="${repo_root}/clients/godot/scenes/Main.tscn"
renderer_path="${repo_root}/clients/godot/scripts/CardControlRenderer.cs"
main_path="${repo_root}/clients/godot/scripts/Main.cs"

fail() {
  echo "FAILED: $*" >&2
  exit 1
}

python3 - "${scene_path}" <<'PY'
import re
import sys
from pathlib import Path

scene = Path(sys.argv[1])
text = scene.read_text(encoding="utf-8")

node_pattern = re.compile(r'^\[node name="(?P<name>[^"]+)"[^\]]*parent="(?P<parent>[^"]*)"[^\]]*\]$', re.MULTILINE)
matches = list(node_pattern.finditer(text))
nodes = {}
for index, match in enumerate(matches):
    start = match.end()
    end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
    nodes[(match.group("parent"), match.group("name"))] = text[start:end]

def value(block, key):
    match = re.search(rf'^{re.escape(key)} = (?P<value>.+)$', block, re.MULTILINE)
    if not match:
        raise AssertionError(f"missing {key}")
    return match.group("value").strip()

def number(block, key):
    return float(value(block, key))

result = nodes.get((".", "ResultFrame"))
if result is None:
    raise AssertionError("ResultFrame node is missing from Main.tscn")

official = nodes.get((".", "OfficialCardPreviewFrame"))
if official is None:
    raise AssertionError("OfficialCardPreviewFrame node is missing from Main.tscn")

official_summary = nodes.get(("OfficialCardPreviewFrame/OfficialPreviewBox", "OfficialCardPreviewSummary"))
if official_summary is None:
    raise AssertionError("OfficialCardPreviewSummary node is missing from Main.tscn")

official_image = nodes.get(("OfficialCardPreviewFrame/OfficialPreviewBox", "OfficialCardPreview"))
if official_image is None:
    raise AssertionError("OfficialCardPreview node is missing from Main.tscn")

prompt = nodes.get((".", "PromptFrame"))
if prompt is None:
    raise AssertionError("PromptFrame node is missing from Main.tscn")

hand_scroll = nodes.get(("Controls", "HandScroll"))
if hand_scroll is None:
    raise AssertionError("HandScroll node is missing from Main.tscn")

viewport_width = 1440.0
table_right_edge = viewport_width - 336.0
result_left = viewport_width + number(result, "offset_left")
result_right = viewport_width + number(result, "offset_right")
result_top = number(result, "offset_top")
result_bottom = number(result, "offset_bottom")
official_bottom = number(official, "offset_bottom")
prompt_top = 900.0 + number(prompt, "offset_top")

assert number(result, "anchor_left") == 1.0, "ResultFrame must be anchored to the right rail"
assert number(result, "anchor_right") == 1.0, "ResultFrame must be anchored to the right rail"
assert value(official, "clip_contents") == "true", (
    "OfficialCardPreviewFrame must clip overflowing preview content so it cannot cover the result panel"
)
assert value(official_summary, "max_lines_visible") == "5", (
    "OfficialCardPreviewSummary must stay compact inside the right preview rail"
)
assert value(official_image, "custom_minimum_size") == "Vector2(268, 240)", (
    "OfficialCardPreview image must stay compact enough for the preview/result/prompt rail"
)
assert result_left >= table_right_edge + 16.0, (
    f"ResultFrame overlaps the main battle table: left={result_left:.0f}, table_right={table_right_edge:.0f}"
)
assert result_right <= viewport_width - 12.0, (
    f"ResultFrame extends outside the right rail: right={result_right:.0f}"
)
assert result_top >= official_bottom + 8.0, (
    f"ResultFrame should sit below the official preview: top={result_top:.0f}, preview_bottom={official_bottom:.0f}"
)
assert result_bottom <= prompt_top - 8.0, (
    f"ResultFrame should sit above the prompt panel: bottom={result_bottom:.0f}, prompt_top={prompt_top:.0f}"
)
assert result_bottom > result_top + 72.0, "ResultFrame is too short for result text and lobby button"
assert value(hand_scroll, "custom_minimum_size") == "Vector2(0, 0)", (
    "Legacy HandScroll must not reserve vertical space now that the wire table owns the hand band"
)

print("Battle layout scene integrity checks passed.")
PY
PY_STATUS=$?

if [[ ${PY_STATUS} -ne 0 ]]; then
  fail "battle layout scene integrity check failed"
fi

if rg -q "new Vector2\\(1280, 560\\)" "${renderer_path}"; then
  fail "wire battle table must be responsive to the main battle column, not hard-coded to 1280px"
fi

if ! rg -q "WireFrame\\(rows, new Vector2\\(0, 820\\)" "${renderer_path}"; then
  fail "wire battle table root must fill the visible combat scroll area instead of floating above the bottom hand border"
fi

if rg -q "contentSize\\.X >= 58f && contentSize\\.Y >= 92f" "${renderer_path}"; then
  fail "compact tabletop card faces must not render effect text that stretches the black/ivory wire table"
fi

for expected in \
  "HandCardFrameSize = new\\(52, 72\\)" \
  "SignatureCardFrameSize = new\\(64, 86\\)" \
  "BattlefieldCardFrameSize = new\\(104, 72\\)" \
  "PileCardFrameSize = new\\(56, 78\\)"
do
  if ! rg -q "${expected}" "${renderer_path}"; then
    fail "wire battle table card sizes must stay compact enough to keep all five table bands visible"
  fi
done

if rg -q "row\\.AddChild\\(WireSite\\(lanes\\.Count" "${renderer_path}"; then
  fail "wire battlefield sites must live inside each lane column, not as detached side panels"
fi

if rg -q "ZoneStrip\\(" "${renderer_path}"; then
  fail "wire table must not use large left-side label strips; route C requires a centered tabletop grid"
fi

for expected in \
  "WireResourceRail\\(Player\\(table, \"opponent\"\\), \"opponent\"\\)" \
  "WirePlayBand\\(Player\\(table, \"opponent\"\\), \"opponent\", Lanes\\(table\\)\\)" \
  "WireSiteDivider\\(Lanes\\(table\\)\\)" \
  "WirePlayBand\\(Player\\(table, \"self\"\\), \"self\", Lanes\\(table\\)\\)" \
  "WireResourceRail\\(Player\\(table, \"self\"\\), \"self\"\\)"
do
  if ! rg -q "${expected}" "${renderer_path}"; then
    fail "wire table must follow the black/ivory reference order: resource rail, opponent play band, centered site divider, self play band, resource rail"
  fi
done

if ! rg -q "private Control WireSiteDivider" "${renderer_path}"; then
  fail "wire battlefield sites must be rendered in a centered divider band that matches the black/ivory reference layout"
fi

if ! rg -q "_boardSummary\\.Visible = lobbyVisible" "${main_path}"; then
  fail "battle snapshots must hide the legacy BoardSummary text so the five-band wire table fits the viewport"
fi

python3 - "${renderer_path}" <<'PY'
import re
import sys
from pathlib import Path

renderer = Path(sys.argv[1])
text = renderer.read_text(encoding="utf-8")

play_band = re.search(
    r"private Control WirePlayBand\(.*?\n    \}",
    text,
    re.DOTALL,
)
if play_band is None:
    raise AssertionError("WirePlayBand method is missing")

heights = [int(value) for value in re.findall(r"new Vector2\(0, ([0-9]+)\)", play_band.group(0))]
if not heights:
    raise AssertionError("WirePlayBand has no fixed vertical budget")
if max(heights) < 144 or max(heights) > 160:
    raise AssertionError(
        f"WirePlayBand vertical budget should fill the reference table without clipping: {max(heights)}"
    )

site_divider = re.search(
    r"private Control WireSiteDivider\(.*?\n    \}",
    text,
    re.DOTALL,
)
if site_divider is None:
    raise AssertionError("WireSiteDivider method is missing")

divider_heights = [int(value) for value in re.findall(r"new Vector2\(0, ([0-9]+)\)", site_divider.group(0))]
if not divider_heights:
    raise AssertionError("WireSiteDivider has no fixed vertical budget")
if max(divider_heights) < 96 or max(divider_heights) > 108:
    raise AssertionError(
        f"WireSiteDivider vertical budget should match the centered black/ivory divider: {max(divider_heights)}"
    )

unit_zone = re.search(
    r"private Control WireUnitZone\(.*?\n    \}",
    text,
    re.DOTALL,
)
if unit_zone is None:
    raise AssertionError("WireUnitZone method is missing")
if "LaneUnitCardFrameSize" not in unit_zone.group(0) or "LaneUnitCardContentSize" not in unit_zone.group(0):
    raise AssertionError("battlefield unit zones must use compact lane card sizes")
PY
