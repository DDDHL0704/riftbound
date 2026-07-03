#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

fail() {
  echo "FAILED: $*" >&2
  exit 1
}

tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/riftbound-inksteel-style.XXXXXX")"
trap 'rm -rf "${tmp_dir}"' EXIT

python3 - "${tmp_dir}" <<'PY'
import sys
from pathlib import Path
from PIL import Image, ImageDraw

out = Path(sys.argv[1])

ink = Image.new("RGB", (320, 200), (5, 6, 6))
draw = ImageDraw.Draw(ink)
for x in range(10, 320, 34):
    draw.line((x, 8, x, 192), fill=(169, 163, 140), width=2)
for y in range(12, 200, 32):
    draw.line((8, y, 312, y), fill=(154, 150, 128), width=2)
draw.rectangle((18, 20, 150, 84), outline=(192, 185, 152), width=2, fill=(24, 25, 22))
draw.rectangle((170, 96, 298, 176), outline=(178, 171, 142), width=2, fill=(30, 29, 25))
draw.rectangle((256, 14, 294, 34), outline=(156, 31, 25), width=2)
draw.rectangle((22, 164, 54, 188), fill=(137, 107, 52))
ink.save(out / "inksteel.png")

bright = Image.new("RGB", (320, 200), (185, 185, 185))
draw = ImageDraw.Draw(bright)
for x in range(20, 320, 60):
    draw.rectangle((x, 22, x + 42, 170), outline=(235, 235, 235), width=3, fill=(145, 145, 145))
bright.save(out / "bright-gray-controls.png")

orange = Image.new("RGB", (320, 200), (190, 105, 24))
draw = ImageDraw.Draw(orange)
for y in range(0, 200, 16):
    draw.line((0, y, 320, y), fill=(242, 174, 62), width=8)
orange.save(out / "orange-dominant.png")

blank = Image.new("RGB", (320, 200), (3, 3, 3))
blank.save(out / "blank-dark.png")
PY

"${script_dir}/check-inksteel-screenshot-style.sh" "${tmp_dir}/inksteel.png" >"${tmp_dir}/inksteel.log" 2>&1 \
  || {
    cat "${tmp_dir}/inksteel.log" >&2
    fail "inksteel fixture was rejected"
  }

expect_rejection() {
  local image="$1"
  local label="$2"
  local output="${tmp_dir}/${label}.log"

  if "${script_dir}/check-inksteel-screenshot-style.sh" "${image}" >"${output}" 2>&1; then
    fail "${label} fixture was accepted"
  fi

  if ! rg -q "inksteel|style|dark|line|warm|saturation|bright" "${output}"; then
    echo "Expected style rejection output for ${label}:" >&2
    cat "${output}" >&2
    fail "${label} fixture rejection did not explain the style metric"
  fi
}

expect_rejection "${tmp_dir}/bright-gray-controls.png" "bright-gray-controls"
expect_rejection "${tmp_dir}/orange-dominant.png" "orange-dominant"
expect_rejection "${tmp_dir}/blank-dark.png" "blank-dark"

echo "Inksteel screenshot style integrity checks passed."
