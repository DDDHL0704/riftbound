#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
interaction_dir="$root/clients/godot/scripts/interaction"
choice_script="$interaction_dir/PromptChoice.cs"
state_script="$interaction_dir/PromptSelectionState.cs"
controller_script="$interaction_dir/PromptInteractionController.cs"
action_bar_script="$root/clients/godot/scripts/ui/ActionBar.cs"
action_bar_scene="$root/clients/godot/scenes/components/ActionBar.tscn"
match_script="$root/clients/godot/scripts/ui/MatchScreen.cs"
match_scene="$root/clients/godot/scenes/screens/MatchScreen.tscn"
main_script="$root/clients/godot/scripts/Main.cs"

for file in \
  "$choice_script" \
  "$state_script" \
  "$controller_script" \
  "$action_bar_script" \
  "$action_bar_scene" \
  "$match_script" \
  "$match_scene" \
  "$main_script"; do
  test -f "$file"
done

# Selection state is prompt-owned. It stores the exact server identity and only
# choice IDs accepted from the current candidate.
rg -q 'record PromptChoice\(' "$choice_script"
rg -q 'IReadOnlyList<string> ObjectIds' "$choice_script"
rg -q 'MatchesObject\(string objectId\)' "$choice_script"
rg -q 'record PromptSelectionState\(' "$state_script"
rg -q 'string PromptId' "$state_script"
rg -q 'long SnapshotTick' "$state_script"
rg -q 'bool CanSubmit' "$state_script"

rg -q 'class PromptInteractionController' "$controller_script"
rg -q 'public void Load\(Godot\.Collections\.Dictionary promptView\)' "$controller_script"
rg -q 'public bool SelectAction\(string actionName\)' "$controller_script"
rg -q 'public bool TrySelectObject\(string objectId\)' "$controller_script"
rg -q 'public bool TrySelectChoice\(string role, string choiceId\)' "$controller_script"
rg -q 'public void ClearSelection\(\)' "$controller_script"
rg -q 'SelectionChanged' "$controller_script"

# Load must compare both prompt identity fields, reject disabled candidates, and
# revalidate retained selections against replacement server choices.
rg -q '_promptId' "$controller_script"
rg -q '_snapshotTick' "$controller_script"
rg -U -q 'identityChanged[\s\S]*?_promptId[\s\S]*?_snapshotTick' "$controller_script"
rg -U -q 'SelectAction\([\s\S]*?enabled' "$controller_script"
rg -q 'RevalidateSelection\(' "$controller_script"
rg -q 'MatchesObject\(objectId\)' "$controller_script"
rg -q '已选择.*项' "$controller_script"
! rg -q 'string\.Join.*selectedLabels' "$controller_script"
for action in \
  ACTIVATE_ABILITY \
  ASSEMBLE_EQUIPMENT \
  CHOOSE_HAND_CARDS \
  HIDE_CARD \
  LEGEND_ACT \
  PAY_COST \
  REVEAL_CARD; do
  rg -q "\"${action}\" =>" "$controller_script"
done

# The pure interaction layer must never scan table placement, hidden card data,
# or engine legality. It consumes prompt dictionaries and nothing else.
if rg -i -q \
  'MatchTableRenderer|CardControlRenderer|SnapshotCardRef|Riftbound\.Engine|IsLegal|faceDown|handHidden|zones\b|objectIndex' \
  "$choice_script" "$state_script" "$controller_script"; then
  exit 1
fi

# Prompt mapping preserves server-provided object aliases so synthetic choices
# such as BATTLEFIELD:<id> can still be selected by clicking the visible site.
rg -U -q 'PromptChoice\(JsonElement choice\)[\s\S]*?"objectIds"' "$main_script"
rg -q '\["promptId"\] = promptId' "$main_script"
rg -q '\["snapshotTick"\] = snapshotTick' "$main_script"

# The action bar replaces the placeholder host row. It exposes friendly actions,
# safe non-spatial choices, cancel, and submit without rendering prompt IDs.
rg -q 'class ActionBar : Control' "$action_bar_script"
rg -q 'event Action<string>\? ActionSelected' "$action_bar_script"
rg -q 'event Action<string, string>\? ChoiceSelected' "$action_bar_script"
rg -q 'event Action\? CancelRequested' "$action_bar_script"
rg -q 'event Action<PromptSelectionState>\? SubmitRequested' "$action_bar_script"
rg -q 'ActionBar.cs' "$action_bar_scene"
rg -q 'name="ActionChoices"' "$action_bar_scene"
rg -q 'name="SelectionSummary"' "$action_bar_scene"
rg -q 'name="CancelButton"' "$action_bar_scene"
rg -q 'name="SubmitButton"' "$action_bar_scene"
if rg -i -q 'promptId|snapshotTick|objectId|serverTick' "$action_bar_script" "$action_bar_scene"; then
  exit 1
fi

rg -q 'ActionBar.tscn' "$match_scene"
rg -q 'name="ActionBar".*instance=ExtResource' "$match_scene"
! rg -q 'name="ActionBarRow"' "$match_scene"
rg -q 'public ActionBar ActionBar' "$match_script"
rg -q 'PromptInteractionController' "$main_script"
rg -q 'TrySelectObject\(' "$main_script"
rg -q 'SubmitRequested' "$main_script"

# Visible diagnostics stage and optionally submit through the same controller and
# action-bar submission path; they must not build a separate command payload.
rg -q -- '--riftbound-smoke-ui-action=' "$main_script"
rg -q 'TryStageAutoSmokeUiAction\(' "$main_script"
rg -U -q 'TryStageAutoSmokeUiAction\([\s\S]*?SelectAction\([\s\S]*?TrySelectChoice\([\s\S]*?SubmitPromptSelectionAsync' "$main_script"

echo "Prompt interaction controller checks passed."
