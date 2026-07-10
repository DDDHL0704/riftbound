#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
main_scene="$root/clients/godot/scenes/Main.tscn"
main_script="$root/clients/godot/scripts/Main.cs"
builder_script="$root/clients/godot/scripts/SpecialPromptCommandBuilder.cs"
mulligan_scene="$root/clients/godot/scenes/overlays/MulliganOverlay.tscn"
trigger_scene="$root/clients/godot/scenes/overlays/TriggerOrderOverlay.tscn"
damage_scene="$root/clients/godot/scenes/overlays/DamageAssignmentOverlay.tscn"
mulligan_script="$root/clients/godot/scripts/ui/MulliganOverlay.cs"
trigger_script="$root/clients/godot/scripts/ui/TriggerOrderOverlay.cs"
damage_script="$root/clients/godot/scripts/ui/DamageAssignmentOverlay.cs"

for file in \
  "$main_scene" \
  "$main_script" \
  "$builder_script" \
  "$mulligan_scene" \
  "$trigger_scene" \
  "$damage_scene" \
  "$mulligan_script" \
  "$trigger_script" \
  "$damage_script"; do
  test -f "$file"
done

# These focused overlays must be self-contained modal controls, never legacy
# prompt forms or dropdowns. Each offers one cancellation route and one submit.
for scene in "$mulligan_scene" "$trigger_scene" "$damage_scene"; do
  rg -q '^anchors_preset = 15$' "$scene"
  rg -q 'name="CancelButton" type="Button"' "$scene"
  rg -q 'name="ConfirmButton" type="Button"' "$scene"
  ! rg -q 'OptionButton' "$scene"
done

rg -q 'OfficialCardView.tscn' "$mulligan_script"
rg -q 'name="MulliganCards"' "$mulligan_scene"
rg -q 'class MulliganOverlay : Control' "$mulligan_script"
rg -q 'public void ShowPrompt\(' "$mulligan_script"
rg -q 'minSelectionCount' "$mulligan_script"
rg -q 'maxSelectionCount' "$mulligan_script"
rg -q 'SelectedObjectIds' "$mulligan_script"
rg -q 'OfficialCardVisualState\.Selected' "$mulligan_script"
! rg -i -q 'promptId|snapshotTick|objectId|serverTick' "$mulligan_scene"

rg -q 'name="TriggerRows"' "$trigger_scene"
rg -q 'class TriggerOrderOverlay : Control' "$trigger_script"
rg -q 'public void ShowPrompt\(' "$trigger_script"
rg -q 'MoveUpButton' "$trigger_script"
rg -q 'MoveDownButton' "$trigger_script"
rg -q 'OrderedTriggerIds' "$trigger_script"
! rg -i -q 'promptId|snapshotTick|objectId|serverTick' "$trigger_scene"

rg -q 'name="DamageRows"' "$damage_scene"
rg -q 'class DamageAssignmentOverlay : Control' "$damage_script"
rg -q 'public void ShowPrompt\(' "$damage_script"
rg -q 'RemainingDamage' "$damage_script"
rg -q 'RequiredAssignments' "$damage_script"
rg -q 'assignmentChoices' "$damage_script"
! rg -i -q 'promptId|snapshotTick|objectId|serverTick' "$damage_scene"

# Main chooses overlays from only the current action dictionaries, preserves
# existing submission methods, and never reopens the retired fallback form.
for overlay in MulliganOverlay TriggerOrderOverlay DamageAssignmentOverlay; do
  rg -q "${overlay}\.tscn" "$main_scene"
  rg -q "GetNode<${overlay}>" "$main_script"
done
rg -q 'ShowSpecialPromptOverlays\(' "$main_script"
rg -q 'HideSpecialPromptOverlays\(' "$main_script"
rg -q 'SubmitMulliganAsync\(' "$main_script"
rg -q 'SubmitSpecialPromptAsync\(' "$main_script"
rg -q 'TryBuildOrderTriggersPayload' "$builder_script"
rg -q 'TryBuildDamageAssignmentPayload' "$builder_script"
! rg -U -q 'PromptMulliganActionNode\([\s\S]*?CheckBox' "$main_script"
! rg -U -q 'PromptSpecialActionNode\([\s\S]*?SubmitSpecialPromptAsync' "$main_script"

echo "Focused prompt overlay checks passed."
