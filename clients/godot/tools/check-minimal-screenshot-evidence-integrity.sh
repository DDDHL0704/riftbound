#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/riftbound-minimal-shot.XXXXXX")"
trap 'rm -rf "${tmp_dir}"' EXIT

fail() {
  echo "FAILED: $*" >&2
  exit 1
}

python3 - "${tmp_dir}" <<'PY'
import sys
from pathlib import Path
from PIL import Image, ImageDraw

out = Path(sys.argv[1])

def table_image(with_result=True):
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
    colors = [(45, 111, 168), (153, 68, 58), (55, 130, 92), (126, 78, 162), (183, 121, 42)]
    for row_y, offset in ((126, 0), (426, 2)):
        draw.rounded_rectangle((54, row_y - 34, 1386, row_y + 224), radius=12, fill=(23, 29, 34), outline=(55, 65, 72), width=2)
        for index in range(7):
            x0 = 118 + index * 176
            color = colors[(index + offset) % len(colors)]
            draw.rounded_rectangle((x0, row_y, x0 + 112, row_y + 170), radius=8, fill=color, outline=(135, 146, 152), width=2)
    if with_result:
        image = Image.blend(image, Image.new("RGB", image.size, (0, 0, 0)), 0.22)
        draw = ImageDraw.Draw(image)
        draw.rounded_rectangle((480, 270, 960, 630), radius=14, fill=(68, 73, 77), outline=(198, 205, 208), width=3)
        draw.rectangle((520, 330, 920, 334), fill=(151, 163, 169))
        draw.rounded_rectangle((590, 516, 850, 574), radius=8, fill=(29, 35, 39), outline=(171, 183, 188), width=2)
    return image

table_image(with_result=True).save(out / "valid-result.png")
table_image(with_result=False).save(out / "missing-result.png")
Image.new("RGB", (1440, 900), (188, 188, 188)).save(out / "blank.png")
PY

"${script_dir}/check-official-card-table-screenshot.sh" "${tmp_dir}/valid-result.png" >/dev/null
"${script_dir}/check-centered-result-overlay-screenshot.sh" "${tmp_dir}/valid-result.png" >/dev/null
"${script_dir}/check-official-card-table-screenshot.sh" "${tmp_dir}/missing-result.png" >/dev/null

if "${script_dir}/check-official-card-table-screenshot.sh" "${tmp_dir}/blank.png" >/dev/null 2>&1; then
  fail "official-card table checker accepted a blank screenshot"
fi

if "${script_dir}/check-centered-result-overlay-screenshot.sh" "${tmp_dir}/missing-result.png" >/dev/null 2>&1; then
  fail "centered result checker accepted a screenshot without the result overlay"
fi

echo "Minimal screenshot evidence integrity checks passed."
