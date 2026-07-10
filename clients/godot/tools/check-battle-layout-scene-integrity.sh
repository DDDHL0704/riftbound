#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
main_scene="${repo_root}/clients/godot/scenes/Main.tscn"
main_script="${repo_root}/clients/godot/scripts/Main.cs"

# Kept under the historical filename because external evidence tooling calls it.
# The product contract is now the single minimal MatchScreen, not wire geometry.
"${repo_root}/clients/godot/tools/check-minimal-match-scene.sh"

for path in \
  "${repo_root}/clients/godot/scripts/CardControlRenderer.cs" \
  "${repo_root}/clients/godot/scripts/RunestoneTheme.cs" \
  "${repo_root}/clients/godot/scripts/RunestoneBackdrop.cs" \
  "${repo_root}/clients/godot/scripts/RunestoneSurface.cs"; do
  test ! -e "${path}"
done

! rg -q 'SnapshotScroll|HandScroll|PromptFrame|ResultFrame|OfficialCardPreviewFrame' "${main_scene}"
! rg -q 'UseLegacyCardTableFallback|CardControlRenderer|Runestone' "${main_script}"

echo "Battle layout scene integrity checks passed for the minimal MatchScreen."
