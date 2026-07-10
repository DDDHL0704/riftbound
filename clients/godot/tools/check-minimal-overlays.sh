#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
inspect_scene="$root/clients/godot/scenes/overlays/CardInspectOverlay.tscn"
inspect_script="$root/clients/godot/scripts/ui/CardInspectOverlay.cs"
result_scene="$root/clients/godot/scenes/overlays/ResultOverlay.tscn"
result_script="$root/clients/godot/scripts/ui/ResultOverlay.cs"
main_scene="$root/clients/godot/scenes/Main.tscn"
main_script="$root/clients/godot/scripts/Main.cs"

for file in \
  "$inspect_scene" \
  "$inspect_script" \
  "$result_scene" \
  "$result_script" \
  "$main_scene" \
  "$main_script"; do
  test -f "$file"
done

# Both overlays own the whole viewport and center one compact panel. They do
# not recreate the retired permanent right rail or nest decorative cards.
for overlay in \
  "CardInspectOverlay:$inspect_scene:InspectCenter:InspectPanel" \
  "ResultOverlay:$result_scene:ResultCenter:ResultPanel"; do
  IFS=: read -r root_node scene center_node panel_node <<<"$overlay"
  root_block="$(sed -n "/\[node name=\"${root_node}\" type=\"Control\"\]/,/^\[node /p" "$scene")"
  printf '%s\n' "$root_block" | rg -q '^anchors_preset = 15$'
  printf '%s\n' "$root_block" | rg -q '^anchor_right = 1\.0$'
  printf '%s\n' "$root_block" | rg -q '^anchor_bottom = 1\.0$'
  rg -q "name=\"${center_node}\" type=\"CenterContainer\"" "$scene"
  rg -q "name=\"${panel_node}\" type=\"PanelContainer\"" "$scene"
  test "$(rg -c 'type="PanelContainer"' "$scene")" -eq 1
done

rg -q 'CardInspectOverlay.cs' "$inspect_scene"
rg -q 'OfficialCardView.tscn' "$inspect_scene"
rg -q 'name="InspectCard".*instance=ExtResource' "$inspect_scene"
rg -q 'name="CloseButton" type="Button"' "$inspect_scene"
rg -q 'class CardInspectOverlay : Control' "$inspect_script"
rg -q 'public void ShowCard\(Godot\.Collections\.Dictionary card\)' "$inspect_script"
rg -q 'public void HideCard\(\)' "$inspect_script"
rg -q '_UnhandledInput\(' "$inspect_script"
rg -q '"ui_cancel"' "$inspect_script"
rg -q 'GuiGetFocusOwner\(' "$inspect_script"
rg -q 'GrabFocus\(' "$inspect_script"
rg -q 'ReadBool\(card, "visible", false\)' "$inspect_script"
rg -q 'ReadBool\(card, "faceDown", true\)' "$inspect_script"

# The visibility guard must execute before the card reaches OfficialCardView;
# this also keeps hidden image paths out of overlay-owned loading code.
show_card="$(sed -n '/public void ShowCard(/,/^    }/p' "$inspect_script")"
guard_line="$(printf '%s\n' "$show_card" | rg -n 'visible.*faceDown|faceDown.*visible' | head -1 | cut -d: -f1)"
display_line="$(printf '%s\n' "$show_card" | rg -n '_cardView\.Display' | head -1 | cut -d: -f1)"
test -n "$guard_line"
test -n "$display_line"
test "$guard_line" -lt "$display_line"
! rg -q 'imagePath|LoadTexture' "$inspect_script"

rg -q 'ResultOverlay.cs' "$result_scene"
rg -q 'name="ReturnButton" type="Button"' "$result_scene"
rg -q 'class ResultOverlay : Control' "$result_script"
rg -q 'event Action\? ReturnLobbyRequested' "$result_script"
rg -q 'public void ShowResult\(Godot\.Collections\.Dictionary result\)' "$result_script"
rg -q 'public void HideResult\(\)' "$result_script"
rg -q 'ReturnLobbyRequested\?\.Invoke\(\)' "$result_script"
rg -q '胜利' "$result_script" "$result_scene"
rg -q '失败' "$result_script" "$result_scene"
rg -q '你' "$result_script" "$result_scene"
rg -q '对手' "$result_script" "$result_scene"

# Overlay presentation consumes only viewer-safe fields. Transport metadata,
# object/player identifiers, prompt UI, and right-rail geometry stay outside.
if rg -i -q \
  'serverTick|"source"|prompt(Id|Frame|Panel|Scroll|Actions)?|playerId|objectId|transport|RightRail' \
  "$inspect_scene" "$inspect_script" "$result_scene" "$result_script"; then
  exit 1
fi
! rg -q 'anchor_left = 1\.0|offset_left = -(320|336)\.0' "$inspect_scene" "$result_scene"

# Main routes safe visible cards and authoritative results into the overlays.
rg -q 'CardInspectOverlay.tscn' "$main_scene"
rg -q 'ResultOverlay.tscn' "$main_scene"
rg -q '\[node name="CardInspectOverlay" parent="\." instance=ExtResource' "$main_scene"
rg -q '\[node name="ResultOverlay" parent="\." instance=ExtResource' "$main_scene"
rg -q 'GetNode<CardInspectOverlay>\("CardInspectOverlay"\)' "$main_script"
rg -q 'GetNode<ResultOverlay>\("ResultOverlay"\)' "$main_script"
rg -q '_matchScreen!\.CardActivated \+= ApplyCardPreview' "$main_script"
rg -q '_cardInspectOverlay\.ShowCard\(card\)' "$main_script"
rg -U -q 'public void ApplyMatchResult\([\s\S]*?_lastViewerResult = BuildViewerResult\(result\)[\s\S]*?_resultOverlay\.ShowResult\(_lastViewerResult\)' "$main_script"
rg -q '_resultOverlay!?\.ReturnLobbyRequested \+=' "$main_script"

# Keep the Task 3 result latch and delayed screenshot path, but force the
# centered result overlay on every delayed frame in the normal match path.
rg -q '_matchFinished \|\| battleActive' "$main_script"
rg -q '_matchFinished && !battleActive' "$main_script"
rg -q 'ResultScreenshotFrameDelay' "$main_script"
rg -q 'ForceResultScreenshotChrome\(' "$main_script"
rg -U -q 'ForceResultScreenshotChrome\([\s\S]*?_resultOverlay\.ShowResult\(_lastViewerResult\)' "$main_script"

# Viewport textures are only authoritative after the render server completes a
# draw. This prevents the second visible window from capturing a partial panel.
capture_method="$(sed -n '/private async Task CaptureVisualScreenshotAsync(/,/^    }/p' "$main_script")"
post_draw_line="$(printf '%s\n' "$capture_method" | rg -n 'RenderingServer\.SignalName\.FramePostDraw' | head -1 | cut -d: -f1)"
force_draw_line="$(printf '%s\n' "$capture_method" | rg -n 'RenderingServer\.ForceDraw\(\)' | head -1 | cut -d: -f1)"
force_sync_line="$(printf '%s\n' "$capture_method" | rg -n 'RenderingServer\.ForceSync\(\)' | head -1 | cut -d: -f1)"
image_read_line="$(printf '%s\n' "$capture_method" | rg -n 'GetTexture\(\)\.GetImage\(\)' | head -1 | cut -d: -f1)"
test -n "$post_draw_line"
test -n "$force_draw_line"
test -n "$force_sync_line"
test -n "$image_read_line"
test "$post_draw_line" -lt "$image_read_line"
test "$post_draw_line" -lt "$force_draw_line"
test "$force_draw_line" -lt "$force_sync_line"
test "$post_draw_line" -lt "$force_sync_line"
test "$force_sync_line" -lt "$image_read_line"

# Automated result evidence must not surrender before the official-card table
# has rendered once, otherwise the centered overlay would cover an empty shell.
rg -q '_battleTableRendered' "$main_script"
rg -q '_lastAppliedPromptView' "$main_script"
rg -U -q '_autoSmokeSurrender[\s\S]*?_battleTableRendered[\s\S]*?SURRENDER' "$main_script"
rg -q 'RunAutoSmokePromptAsync\(_lastAppliedPromptView\)' "$main_script"
snapshot_method="$(sed -n '/public void ApplySnapshotSections(/,/^    }/p' "$main_script")"
legacy_render_line="$(printf '%s\n' "$snapshot_method" | rg -n 'RenderSnapshotSections\(' | head -1 | cut -d: -f1)"
legacy_gate_line="$(printf '%s\n' "$snapshot_method" | rg -n '_battleTableRendered = true' | tail -1 | cut -d: -f1)"
test -n "$legacy_render_line"
test -n "$legacy_gate_line"
test "$legacy_render_line" -lt "$legacy_gate_line"

echo "Minimal overlay checks passed."
