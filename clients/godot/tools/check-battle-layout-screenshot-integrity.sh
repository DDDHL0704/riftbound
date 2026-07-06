#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

fail() {
  echo "FAILED: $*" >&2
  exit 1
}

tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/riftbound-battle-layout-shot.XXXXXX")"
trap 'rm -rf "${tmp_dir}"' EXIT

python3 - "${tmp_dir}" <<'PY'
import sys
from pathlib import Path
from PIL import Image, ImageDraw

out = Path(sys.argv[1])

def draw_wire(
    path: Path,
    include_bottom: bool = True,
    include_right_result: bool = True,
    result_line = (178, 171, 145),
    neutral_right_chrome: bool = True,
):
    image = Image.new("RGB", (1440, 900), (4, 4, 4))
    draw = ImageDraw.Draw(image)
    line = (178, 171, 145)
    dim = (78, 76, 66)
    fill = (17, 17, 15)
    table = (22, 58, 1128, 872 if include_bottom else 780)
    draw.rectangle(table, outline=line, width=2, fill=fill)
    for y in (170, 310, 582, 720):
        if y < table[3]:
            draw.line((table[0], y, table[2], y), fill=line, width=2)
    for x in (122, 456, 792, 960):
        draw.line((x, table[1], x, table[3]), fill=dim, width=2)
    for lane_x0, lane_x1 in ((128, 620), (628, 1120)):
        draw.rectangle((lane_x0, 342, lane_x1, 578), outline=line, width=2, fill=(61, 59, 52))
        draw.rectangle((lane_x0 + 80, 418, lane_x1 - 8, 502), outline=line, width=2, fill=(16, 16, 14))
        for y in (406, 512):
            draw.line((lane_x0, y, lane_x1, y), fill=line, width=2)
    if include_right_result:
        chrome_line = line if neutral_right_chrome else (12, 12, 10)
        draw.rectangle((1152, 16, 1424, 396), outline=chrome_line, width=2, fill=(6, 6, 5))
        draw.rectangle((1152, 406, 1424, 630), outline=result_line, width=2, fill=(11, 10, 9))
        draw.rectangle((1152, 640, 1424, 884), outline=chrome_line, width=2, fill=(6, 6, 5))
    image.save(path)

draw_wire(out / "wire-layout.png")
draw_wire(out / "brass-result-rail.png", result_line=(174, 132, 44), neutral_right_chrome=False)
draw_wire(out / "cropped-bottom-hand.png", include_bottom=False)
draw_wire(out / "missing-right-result.png", include_right_result=False)
PY

"${script_dir}/check-battle-layout-screenshot.sh" "${tmp_dir}/wire-layout.png" >"${tmp_dir}/wire-layout.log" 2>&1 \
  || {
    cat "${tmp_dir}/wire-layout.log" >&2
    fail "wire-layout fixture was rejected"
  }

"${script_dir}/check-battle-layout-screenshot.sh" "${tmp_dir}/brass-result-rail.png" >"${tmp_dir}/brass-result-rail.log" 2>&1 \
  || {
    cat "${tmp_dir}/brass-result-rail.log" >&2
    fail "brass-result-rail fixture was rejected"
  }

expect_rejection() {
  local image="$1"
  local label="$2"
  local output="${tmp_dir}/${label}.log"

  if "${script_dir}/check-battle-layout-screenshot.sh" "${image}" >"${output}" 2>&1; then
    fail "${label} fixture was accepted"
  fi

  if ! rg -q "battle layout|wire table|bottom|right rail|result" "${output}"; then
    echo "Expected battle-layout rejection output for ${label}:" >&2
    cat "${output}" >&2
    fail "${label} fixture rejection did not explain the layout metric"
  fi
}

expect_rejection "${tmp_dir}/cropped-bottom-hand.png" "cropped-bottom-hand"
expect_rejection "${tmp_dir}/missing-right-result.png" "missing-right-result"

echo "Battle layout screenshot integrity checks passed."
