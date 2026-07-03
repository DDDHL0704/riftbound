#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  clients/godot/tools/check-inksteel-screenshot-style.sh /path/to/result.png [...]

Runs a lightweight palette sanity check for the Godot inksteel visual route.
This does not replace human screenshot review; it catches obvious regressions
back to bright gray controls, gold/orange dominance, or blank/dropped linework.
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

if (( $# == 0 )); then
  usage >&2
  exit 2
fi

python3 - "$@" <<'PY'
import math
import sys
from colorsys import rgb_to_hsv
from pathlib import Path

try:
    from PIL import Image
except Exception as exc:  # pragma: no cover - diagnostic path for operator machines.
    print(f"FAILED inksteel style check: Python Pillow/PIL is required ({exc})", file=sys.stderr)
    sys.exit(2)

MIN_DARK_RATIO = 0.55
MIN_NEUTRAL_LINE_RATIO = 0.018
MAX_BRIGHT_RATIO = 0.25
MAX_ORANGE_WARM_RATIO = 0.14
MAX_SATURATED_RATIO = 0.35
TARGET_SAMPLE_COUNT = 60000


def metrics_for(path: Path):
    image = Image.open(path).convert("RGB")
    width, height = image.size
    step = max(1, int(math.sqrt((width * height) / TARGET_SAMPLE_COUNT)))

    total = 0
    dark = 0
    neutral_line = 0
    bright = 0
    orange_warm = 0
    saturated = 0

    for y in range(0, height, step):
        for x in range(0, width, step):
            r, g, b = image.getpixel((x, y))
            luma = (0.2126 * r) + (0.7152 * g) + (0.0722 * b)
            mx = max(r, g, b)
            mn = min(r, g, b)
            sat = 0.0 if mx == 0 else (mx - mn) / mx
            hue = rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0)[0] * 360.0

            total += 1
            if luma < 90:
                dark += 1
            if 80 <= luma <= 225 and sat <= 0.32:
                neutral_line += 1
            if luma > 180:
                bright += 1
            if 18 <= hue <= 65 and sat > 0.35 and luma > 70:
                orange_warm += 1
            if sat > 0.55:
                saturated += 1

    return {
        "path": str(path),
        "width": width,
        "height": height,
        "samples": total,
        "dark_ratio": dark / total,
        "neutral_line_ratio": neutral_line / total,
        "bright_ratio": bright / total,
        "orange_warm_ratio": orange_warm / total,
        "saturated_ratio": saturated / total,
    }


def format_metrics(metrics):
    return (
        f"{metrics['path']}: {metrics['width']}x{metrics['height']} "
        f"samples={metrics['samples']} "
        f"dark={metrics['dark_ratio']:.3f} "
        f"neutral_line={metrics['neutral_line_ratio']:.3f} "
        f"bright={metrics['bright_ratio']:.3f} "
        f"orange_warm={metrics['orange_warm_ratio']:.3f} "
        f"saturation={metrics['saturated_ratio']:.3f}"
    )


failures = []
for raw_path in sys.argv[1:]:
    path = Path(raw_path)
    if not path.is_file():
        failures.append(f"{path}: screenshot file not found")
        continue

    try:
        metrics = metrics_for(path)
    except Exception as exc:
        failures.append(f"{path}: unreadable screenshot ({exc})")
        continue

    print(f"Inksteel style metrics: {format_metrics(metrics)}")

    if metrics["dark_ratio"] < MIN_DARK_RATIO:
        failures.append(
            f"{path}: inksteel dark table ratio too low "
            f"({metrics['dark_ratio']:.3f} < {MIN_DARK_RATIO:.3f})"
        )
    if metrics["neutral_line_ratio"] < MIN_NEUTRAL_LINE_RATIO:
        failures.append(
            f"{path}: inksteel neutral linework ratio too low "
            f"({metrics['neutral_line_ratio']:.3f} < {MIN_NEUTRAL_LINE_RATIO:.3f})"
        )
    if metrics["bright_ratio"] > MAX_BRIGHT_RATIO:
        failures.append(
            f"{path}: bright gray/control area ratio too high "
            f"({metrics['bright_ratio']:.3f} > {MAX_BRIGHT_RATIO:.3f})"
        )
    if metrics["orange_warm_ratio"] > MAX_ORANGE_WARM_RATIO:
        failures.append(
            f"{path}: orange/gold warm dominance too high "
            f"({metrics['orange_warm_ratio']:.3f} > {MAX_ORANGE_WARM_RATIO:.3f})"
        )
    if metrics["saturated_ratio"] > MAX_SATURATED_RATIO:
        failures.append(
            f"{path}: high saturation ratio too high "
            f"({metrics['saturated_ratio']:.3f} > {MAX_SATURATED_RATIO:.3f})"
        )

if failures:
    print("FAILED inksteel screenshot style check:", file=sys.stderr)
    for failure in failures:
        print(f"  - {failure}", file=sys.stderr)
    sys.exit(1)

print("Inksteel screenshot style checks passed.")
PY
