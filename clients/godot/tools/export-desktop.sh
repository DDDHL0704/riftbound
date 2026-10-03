#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "$0")/.." && pwd)"
godot_bin="${RIFTBOUND_GODOT_BIN:-godot}"
target="${1:-macOS}"
case "$target" in
  macOS) output="exports/macos/Riftbound.zip" ;;
  windows) target="Windows Desktop"; output="exports/windows/Riftbound.exe" ;;
  *) echo 'Usage: export-desktop.sh [macOS|windows]' >&2; exit 2 ;;
esac
mkdir -p "$(dirname "$project_dir/$output")"
log_file="$project_dir/exports/export.log"
"$godot_bin" --headless --path "$project_dir" --export-release "$target" "$output" > "$log_file" 2>&1
# Godot may exit successfully even when a C# export plugin reported an error.
if grep -q '^ERROR:' "$log_file"; then
  cat "$log_file" >&2
  exit 1
fi
test -s "$project_dir/$output"
if [[ "$target" == "Windows Desktop" ]]; then
  test -s "$project_dir/exports/windows/data_Riftbound.GodotClient_windows_x86_64/Riftbound.GodotClient.dll"
  python3 - "$project_dir/exports/windows" <<'PY'
import sys, zipfile
from pathlib import Path
root = Path(sys.argv[1])
with zipfile.ZipFile(root / 'Riftbound-windows-x64.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
    archive.write(root / 'Riftbound.exe', 'Riftbound.exe')
    for path in sorted((root / 'data_Riftbound.GodotClient_windows_x86_64').rglob('*')):
        if path.is_file():
            archive.write(path, path.relative_to(root))
PY
fi
echo "Exported $project_dir/$output"
