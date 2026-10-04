#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
scene="$root/clients/godot/scenes/screens/MatchScreen.tscn"
screen="$root/clients/godot/scripts/ui/MatchScreen.cs"
renderer="$root/clients/godot/scripts/ui/MatchTableRenderer.cs"
layout="$root/clients/godot/scripts/ui/MatchTableLayout.cs"
main_scene="$root/clients/godot/scenes/Main.tscn"
main_script="$root/clients/godot/scripts/Main.cs"

test -f "$scene"
test -f "$screen"
test -f "$renderer"
test -f "$main_scene"
test -f "$main_script"

rg -q 'MatchScreen.cs' "$scene"
rg -q 'class MatchScreen : AppScreen' "$screen"
rg -q 'event Action<CardDictionary>.*CardActivated' "$screen"
rg -q 'RenderSections\(' "$screen"
rg -q 'SetTurnStatus\(' "$screen"
rg -q 'ClearPromptStates\(' "$screen"
rg -q 'SetObjectState\(' "$screen"

# The new native table is built through typed layout references, not fragile scene paths.
for member in TurnHeadline TurnDetail OpponentHand OpponentPublicZones Battlefields SelfPublicZones SelfHand Composer Chain History; do
  rg -q "$member" "$layout"
done
rg -q 'ScrollContainer' "$layout"
rg -q 'Instantiate<ActionBar>' "$layout"
rg -q 'DestinationActivated' "$screen"
rg -q 'CardInspectionRequested' "$screen"

# Every card face or back is an OfficialCardView. Hidden cards are normalized
# without copying identity or imagePath before they reach the component.
rg -q 'OfficialCardView' "$renderer"
rg -q 'Instantiate<OfficialCardView>' "$renderer"
rg -q '\.Activated \+=' "$renderer"
rg -q 'visible.*faceDown|faceDown.*visible' "$renderer"
rg -q 'NeutralHiddenCard' "$renderer"
rg -q 'nodes.Site' "$renderer"
if sed -n '/NeutralHiddenCard(/,/^    }/p' "$renderer" | rg -q 'imagePath|cardName|cardNo'; then
  exit 1
fi

# Player-facing labels are localized and never read/display raw identity fields.
rg -q '"对手"' "$renderer"
rg -q '"我方"' "$renderer"
! rg -q 'promptId|snapshotTick|serverTick' "$scene" "$screen" "$renderer"

# The player-facing rail contains only card details, server chain and battle events.
! rg -q 'SnapshotScroll|PromptScroll|RawLog' "$scene" "$screen" "$renderer"
! rg -q '(^|[^0-9])820([^0-9]|$)|(^|[^0-9])320([^0-9]|$)|(^|[^0-9])336([^0-9]|$)' "$scene" "$screen" "$renderer"
! rg -q 'Riftbound\.Engine|EngineLegality|IsLegal(Action|Target|Choice)?' "$screen" "$renderer"

# Main mounts one match screen beside the lobby. No fallback renderer remains.
rg -q 'MatchScreen.tscn' "$main_scene"
rg -q '\[node name="MatchScreen" parent="\." instance=ExtResource' "$main_scene"
rg -q 'GetNode<MatchScreen>\("MatchScreen"\)' "$main_script"
rg -q '_matchScreen\.RenderSections\(sections\)' "$main_script"
! rg -q 'UseLegacyCardTableFallback|CardControlRenderer|legacyBattleVisible|_controls\.OffsetRight' "$main_script"

# Hidden standby faces are a boundary violation, and shutdown releases all
# official-card texture references before disconnecting.
rg -A4 'var status = opponentHandFaces == 0' "$main_script" \
  | rg -q 'opponentStandbyFaces == 0'
rg -q 'ReleaseTextureReferences\(this\)' "$main_script"
! rg -q 'PromptActionNode|PromptCard|PromptSelectionStepNode|OptionButton' "$main_script"
