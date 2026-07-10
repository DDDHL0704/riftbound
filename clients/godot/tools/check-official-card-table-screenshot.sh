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
    print(f"FAILED official-card table check: Pillow/PIL is required ({exc})", file=sys.stderr)
    raise SystemExit(2)

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

    pixels = list(image.getdata())
    luminance = [0.2126 * r + 0.7152 * g + 0.0722 * b for r, g, b in pixels]
    dark_ratio = sum(value < 80 for value in luminance) / len(luminance)
    bright_ratio = sum(value > 210 for value in luminance) / len(luminance)
    saturated_ratio = sum(max(pixel) - min(pixel) > 38 for pixel in pixels) / len(pixels)
    mean_stddev = sum(ImageStat.Stat(image).stddev) / 3
    sample = image.resize((160, 100))
    sampled_colors = len(sample.getcolors(160 * 100) or [])

    if dark_ratio < 0.72:
        failures.append(f"{path}: dark table coverage is too low ({dark_ratio:.3f})")
    if bright_ratio > 0.18:
        failures.append(f"{path}: screenshot contains too much bright/blank surface ({bright_ratio:.3f})")
    if saturated_ratio < 0.0005:
        failures.append(f"{path}: no meaningful official-card color content detected ({saturated_ratio:.5f})")
    if mean_stddev < 8.0 or sampled_colors < 180:
        failures.append(
            f"{path}: screenshot is visually blank or lacks table/card detail "
            f"(stddev={mean_stddev:.2f}, sampled_colors={sampled_colors})"
        )

if failures:
    print("FAILED official-card table screenshot check:", file=sys.stderr)
    for failure in failures:
        print(f"  - {failure}", file=sys.stderr)
    raise SystemExit(1)

print("Official-card table screenshot check passed.")
PY
