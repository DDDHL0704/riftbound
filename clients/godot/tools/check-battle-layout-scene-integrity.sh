#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
scene_path="${repo_root}/clients/godot/scenes/Main.tscn"

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

prompt = nodes.get((".", "PromptFrame"))
if prompt is None:
    raise AssertionError("PromptFrame node is missing from Main.tscn")

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

print("Battle layout scene integrity checks passed.")
PY
PY_STATUS=$?

if [[ ${PY_STATUS} -ne 0 ]]; then
  fail "battle layout scene integrity check failed"
fi
