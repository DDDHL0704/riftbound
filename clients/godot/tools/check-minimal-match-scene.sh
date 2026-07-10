#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
scene="$root/clients/godot/scenes/screens/MatchScreen.tscn"
screen="$root/clients/godot/scripts/ui/MatchScreen.cs"
renderer="$root/clients/godot/scripts/ui/MatchTableRenderer.cs"
main_scene="$root/clients/godot/scenes/Main.tscn"
main_script="$root/clients/godot/scripts/Main.cs"

test -f "$scene"
test -f "$screen"
test -f "$renderer"
test -f "$main_scene"
test -f "$main_script"

rg -q 'MatchScreen.cs' "$scene"
rg -q 'class MatchScreen : AppScreen' "$screen"
rg -q 'event Action<Godot.Collections.Dictionary>.*CardActivated' "$screen"
rg -q 'RenderSections\(' "$screen"
rg -q 'SetTurnStatus\(' "$screen"
rg -q 'ClearPromptStates\(' "$screen"
rg -q 'SetObjectState\(' "$screen"

for node in \
  TurnHeadline TurnDetail \
  OpponentArea OpponentHand OpponentPublicZones \
  Battlefields BattlefieldOne BattlefieldTwo \
  SelfArea SelfPublicZones HandArea SelfHand ActionBarHost; do
  rg -q "name=\"${node}\"" "$scene"
done

for lane in BattlefieldOne BattlefieldTwo; do
  rg -q "name=\"OpponentUnits\".*parent=\"MatchLayout/Battlefields/${lane}/LaneContent\"" "$scene"
  rg -q "name=\"OfficialSite\".*parent=\"MatchLayout/Battlefields/${lane}/LaneContent/CenterRow\"" "$scene"
  rg -q "name=\"SelfUnits\".*parent=\"MatchLayout/Battlefields/${lane}/LaneContent\"" "$scene"
  rg -q "name=\"Standby\".*parent=\"MatchLayout/Battlefields/${lane}/LaneContent/CenterRow\"" "$scene"
done

# The hand scrolls horizontally and the bottom action host remains a primary target.
sed -n '/\[node name="SelfHandScroll"/,/^\[node /p' "$scene" \
  | rg -q '^horizontal_scroll_mode = 2$'
sed -n '/\[node name="ActionBarHost"/,/^\[node /p' "$scene" \
  | rg -q '^custom_minimum_size = Vector2\(0, ([4-9][0-9]|[1-9][0-9]{2,})\)$'

# Every card face or back is an OfficialCardView. Hidden cards are normalized
# without copying identity or imagePath before they reach the component.
rg -q 'OfficialCardView' "$renderer"
rg -q 'Instantiate<OfficialCardView>' "$renderer"
rg -q '\.Activated \+=' "$renderer"
rg -q 'visible.*faceDown|faceDown.*visible' "$renderer"
rg -q 'NeutralHiddenCard' "$renderer"
rg -q 'SiteCardSize' "$renderer"
rg -q 'name="OfficialSite" type="CenterContainer"' "$scene"
if sed -n '/NeutralHiddenCard(/,/^    }/p' "$renderer" | rg -q 'imagePath|cardName|cardNo'; then
  exit 1
fi

# Player-facing labels are localized and never read/display raw identity fields.
rg -q '"对手"' "$renderer"
rg -q '"我方"' "$renderer"
! rg -q 'playerId|promptId|snapshotTick|serverTick' "$scene" "$screen" "$renderer"

# The focused match view has no debug scroll, fixed wire-table height, or rail.
! rg -q 'SnapshotScroll|PromptScroll|RawLog|RightRail' "$scene" "$screen" "$renderer"
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
