#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  clients/godot/tools/check-battle-layout-screenshot.sh /path/to/result.png [...]

Runs a lightweight geometry sanity check for the Godot black/ivory wire-table
layout. This does not replace human screenshot review; it catches screenshots
where the battle table is clipped, the bottom hand band falls off-screen, or the
right result rail disappears even though the palette still looks inksteel.
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
from pathlib import Path

try:
    from PIL import Image
except Exception as exc:  # pragma: no cover - diagnostic path for operator machines.
    print(f"FAILED battle layout screenshot check: Python Pillow/PIL is required ({exc})", file=sys.stderr)
    sys.exit(2)

MIN_WIDTH = 1200
MIN_HEIGHT = 800
MIN_MAJOR_HORIZONTAL_CLUSTERS = 6
MIN_BOTTOM_LINE_Y_RATIO = 0.93
MIN_RIGHT_RAIL_LINE_RATIO = 0.012
MIN_TABLE_VERTICAL_SPAN_RATIO = 0.82
TARGET_COLUMN_BUDGET = 700


def is_wire_pixel(pixel):
    r, g, b = pixel
    luma = (0.2126 * r) + (0.7152 * g) + (0.0722 * b)
    mx = max(r, g, b)
    mn = min(r, g, b)
    sat = 0.0 if mx == 0 else (mx - mn) / mx
    return 70 <= luma <= 225 and sat <= 0.38


def scan_row_clusters(image, x0, x1):
    width, height = image.size
    step = max(1, math.ceil((x1 - x0) / TARGET_COLUMN_BUDGET))
    sample_count = max(1, len(range(x0, x1, step)))
    row_scores = []

    for y in range(height):
        wire = 0
        for x in range(x0, x1, step):
            if is_wire_pixel(image.getpixel((x, y))):
                wire += 1
        row_scores.append(wire / sample_count)

    clusters = []
    in_cluster = False
    start = 0
    values = []
    threshold = 0.18

    for y, score in enumerate(row_scores):
        if score >= threshold and not in_cluster:
            start = y
            values = [score]
            in_cluster = True
        elif score >= threshold:
            values.append(score)
        elif in_cluster:
            if y - start >= 1:
                clusters.append((start, y - 1, max(values), sum(values) / len(values)))
            in_cluster = False

    if in_cluster:
        clusters.append((start, height - 1, max(values), sum(values) / len(values)))

    return clusters


def right_rail_line_ratio(image):
    width, height = image.size
    x0 = max(0, width - 292)
    x1 = max(x0 + 1, width - 14)
    y0 = int(height * 0.43)
    y1 = int(height * 0.72)
    total = 0
    wire = 0

    for y in range(y0, y1, 2):
        for x in range(x0, x1, 2):
            total += 1
            if is_wire_pixel(image.getpixel((x, y))):
                wire += 1

    return 0.0 if total == 0 else wire / total


def metrics_for(path):
    image = Image.open(path).convert("RGB")
    width, height = image.size
    table_x0 = 10
    table_x1 = min(max(table_x0 + 1, width - 320), int(width * 0.80))
    clusters = scan_row_clusters(image, table_x0, table_x1)
    major = [cluster for cluster in clusters if cluster[2] >= 0.45]
    top_y = min((cluster[0] for cluster in major), default=-1)
    bottom_y = max((cluster[1] for cluster in major), default=-1)
    vertical_span = 0 if top_y < 0 or bottom_y < 0 else bottom_y - top_y

    return {
        "path": str(path),
        "width": width,
        "height": height,
        "table_x1": table_x1,
        "cluster_count": len(clusters),
        "major_cluster_count": len(major),
        "top_y": top_y,
        "bottom_y": bottom_y,
        "vertical_span_ratio": 0.0 if height == 0 else vertical_span / height,
        "right_rail_line_ratio": right_rail_line_ratio(image),
    }


def format_metrics(metrics):
    return (
        f"{metrics['path']}: {metrics['width']}x{metrics['height']} "
        f"table_right={metrics['table_x1']} "
        f"row_clusters={metrics['cluster_count']} "
        f"major_rows={metrics['major_cluster_count']} "
        f"top_y={metrics['top_y']} bottom_y={metrics['bottom_y']} "
        f"vertical_span={metrics['vertical_span_ratio']:.3f} "
        f"right_rail_line={metrics['right_rail_line_ratio']:.3f}"
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

    print(f"Battle layout screenshot metrics: {format_metrics(metrics)}")

    if metrics["width"] < MIN_WIDTH or metrics["height"] < MIN_HEIGHT:
        failures.append(
            f"{path}: battle layout screenshot too small "
            f"({metrics['width']}x{metrics['height']}, minimum {MIN_WIDTH}x{MIN_HEIGHT})"
        )
    if metrics["major_cluster_count"] < MIN_MAJOR_HORIZONTAL_CLUSTERS:
        failures.append(
            f"{path}: wire table has too few major horizontal bands "
            f"({metrics['major_cluster_count']} < {MIN_MAJOR_HORIZONTAL_CLUSTERS})"
        )
    if metrics["bottom_y"] < int(metrics["height"] * MIN_BOTTOM_LINE_Y_RATIO):
        failures.append(
            f"{path}: bottom hand/table border is clipped or too high "
            f"(bottom_y={metrics['bottom_y']}, expected >= {int(metrics['height'] * MIN_BOTTOM_LINE_Y_RATIO)})"
        )
    if metrics["vertical_span_ratio"] < MIN_TABLE_VERTICAL_SPAN_RATIO:
        failures.append(
            f"{path}: wire table vertical span too small "
            f"({metrics['vertical_span_ratio']:.3f} < {MIN_TABLE_VERTICAL_SPAN_RATIO:.3f})"
        )
    if metrics["right_rail_line_ratio"] < MIN_RIGHT_RAIL_LINE_RATIO:
        failures.append(
            f"{path}: right rail/result panel linework missing or too weak "
            f"({metrics['right_rail_line_ratio']:.3f} < {MIN_RIGHT_RAIL_LINE_RATIO:.3f})"
        )

if failures:
    print("FAILED battle layout screenshot check:", file=sys.stderr)
    for failure in failures:
        print(f"  - {failure}", file=sys.stderr)
    sys.exit(1)

print("Battle layout screenshot checks passed.")
PY
