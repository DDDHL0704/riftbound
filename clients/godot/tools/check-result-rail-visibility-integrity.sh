#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
main_cs="${repo_root}/clients/godot/scripts/Main.cs"
main_scene="${repo_root}/clients/godot/scenes/Main.tscn"
result_scene="${repo_root}/clients/godot/scenes/overlays/ResultOverlay.tscn"

# Historical entry point, now guarding the centered authoritative result overlay.
rg -q 'ResultOverlay.tscn' "${main_scene}"
rg -q 'name="ResultCenter" type="CenterContainer"' "${result_scene}"
rg -U -q 'public void ApplyMatchResult\([\s\S]*?_matchFinished = true[\s\S]*?_resultOverlay\.ShowResult\(_lastViewerResult\)' "${main_cs}"
rg -q '_matchFinished \|\| battleActive' "${main_cs}"
rg -q '_matchFinished && !battleActive' "${main_cs}"
rg -q 'CaptureResultScreenshot\(' "${main_cs}"
rg -q 'ForceResultScreenshotChrome\(' "${main_cs}"
rg -q 'ResultScreenshotFrameDelay' "${main_cs}"
! rg -q 'ResultFrame|SetRightRailMatchResultVisible|UseLegacyCardTableFallback' "${main_cs}" "${main_scene}"

echo "Centered result overlay visibility checks passed."
