#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
project="$root/clients/godot/project.godot"
main_scene="$root/clients/godot/scenes/Main.tscn"
main_script="$root/clients/godot/scripts/Main.cs"
action_bar="$root/clients/godot/scripts/ui/ActionBar.cs"
lobby_screen="$root/clients/godot/scripts/ui/LobbyScreen.cs"
card_view="$root/clients/godot/scripts/ui/OfficialCardView.cs"
texture_loader="$root/clients/godot/scripts/ui/CardTextureLoader.cs"
evidence_dir="$root/clients/godot/screenshots/units"

for file in "$project" "$main_scene" "$main_script" "$action_bar" "$lobby_screen" "$card_view"; do
  test -f "$file"
done

# One responsive product path: no hidden wire table, right rail, custom card
# frame renderer, or rejected inksteel theme remains in the runtime project.
for token in \
  SnapshotScroll SnapshotRows HandScroll HandRow PromptFrame PromptScroll \
  PromptActions ResultFrame OfficialCardPreviewFrame; do
  ! rg -q "name=\"${token}\"" "$main_scene"
done

! rg -q 'UseLegacyCardTableFallback|CardControlRenderer|RunestoneTheme|RunestoneBackdrop|RunestoneSurface' \
  "$main_script" "$card_view"
! rg -q 'PromptActionNode|PromptSelectionStepNode|PromptSelector|OptionButton' "$main_script"
! test -e "$root/clients/godot/scripts/CardControlRenderer.cs"
! test -e "$root/clients/godot/scripts/RunestoneTheme.cs"
! test -e "$root/clients/godot/scripts/RunestoneBackdrop.cs"
! test -e "$root/clients/godot/scripts/RunestoneSurface.cs"

test -f "$texture_loader"
rg -q 'class CardTextureLoader' "$texture_loader"
rg -q 'CardTextureLoader.Load' "$card_view"
rg -q 'GetWindow\(\)\.MinSize = new Vector2I\(1280, 720\)' "$main_script"
rg -q 'window/size/viewport_width=1440' "$project"
rg -q 'window/size/viewport_height=900' "$project"
rg -q 'window/stretch/aspect="expand"' "$project"

for action in \
  ui_inspect_card ui_cancel_selection ui_confirm_action \
  ui_action_previous ui_action_next; do
  rg -q "${action}" "$project"
done

rg -q 'FocusAdjacentAction\(' "$action_bar"
rg -q 'ConfirmCurrent\(' "$action_bar"
rg -q 'CancelCurrent\(' "$action_bar"
rg -q 'HandleKeyboardAction\(' "$main_script"
rg -q 'ConfigureFocusLoop\(' "$lobby_screen" "$action_bar"
test -f "$evidence_dir/m7-keyboard-lobby-focus-before.png"
test -f "$evidence_dir/m7-keyboard-lobby-focus-next.png"

# Captures are part of the gate, not optional documentation. Verify exact PNG
# dimensions without trusting filenames.
for size in 1280x720 1440x900 1920x1080; do
  image="$evidence_dir/m7-minimal-runtime-${size}.png"
  test -f "$image"
  actual="$(sips -g pixelWidth -g pixelHeight "$image" 2>/dev/null \
    | awk '/pixelWidth/ { width=$2 } /pixelHeight/ { height=$2 } END { print width "x" height }')"
  test "$actual" = "$size"
done

echo "Minimal responsive runtime checks passed."
