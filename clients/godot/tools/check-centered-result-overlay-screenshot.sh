#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -lt 1 ]]; then
  echo "Usage: $0 /path/to/result.png [...]" >&2
  exit 2
fi

python3 - "$@" <<'PY'
import sys
from pathlib import Path

try:
    from PIL import Image, ImageStat
except ImportError as exc:
    print(f"FAILED centered result overlay check: Pillow/PIL is required ({exc})", file=sys.stderr)
    raise SystemExit(2)

def mean_luminance(image):
    stat = ImageStat.Stat(image)
    r, g, b = stat.mean
    return 0.2126 * r + 0.7152 * g + 0.0722 * b

failures = []
for raw_path in sys.argv[1:]:
    path = Path(raw_path)
    try:
        image = Image.open(path).convert("RGB")
    except Exception as exc:
        failures.append(f"{path}: unreadable PNG ({exc})")
        continue

    width, height = image.size
    if width < 800 or height < 600:
        failures.append(f"{path}: screenshot is too small ({width}x{height})")
        continue

    center = image.crop((int(width * 0.34), int(height * 0.30), int(width * 0.66), int(height * 0.70)))
    corner_size = (int(width * 0.18), int(height * 0.18))
    corners = [
        image.crop((0, 0, *corner_size)),
        image.crop((width - corner_size[0], 0, width, corner_size[1])),
        image.crop((0, height - corner_size[1], corner_size[0], height)),
        image.crop((width - corner_size[0], height - corner_size[1], width, height)),
    ]
    center_luma = mean_luminance(center)
    outer_luma = sum(mean_luminance(corner) for corner in corners) / len(corners)
    center_pixels = list(center.getdata())
    neutral_panel_ratio = sum(
        35 <= (0.2126 * r + 0.7152 * g + 0.0722 * b) <= 115
        and max(r, g, b) - min(r, g, b) <= 34
        for r, g, b in center_pixels
    ) / len(center_pixels)

    if center_luma - outer_luma < 18:
        failures.append(
            f"{path}: centered result panel contrast is missing "
            f"(center={center_luma:.1f}, outer={outer_luma:.1f})"
        )
    if neutral_panel_ratio < 0.52:
        failures.append(
            f"{path}: center is not dominated by the neutral result panel "
            f"({neutral_panel_ratio:.3f})"
        )

if failures:
    print("FAILED centered result overlay screenshot check:", file=sys.stderr)
    for failure in failures:
        print(f"  - {failure}", file=sys.stderr)
    raise SystemExit(1)

print("Centered result overlay screenshot check passed.")
PY
