#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
script="$root/clients/godot/scripts/ui/OfficialCardView.cs"
scene="$root/clients/godot/scenes/components/OfficialCardView.tscn"
states="$root/clients/godot/scripts/ui/OfficialCardVisualState.cs"
texture_loader="$root/clients/godot/scripts/ui/CardTextureLoader.cs"

test -f "$script"
test -f "$scene"
test -f "$states"
test -f "$texture_loader"
rg -q 'class OfficialCardView' "$script"
rg -q 'PreserveOfficialAspect' "$script"
rg -q 'visible.*faceDown|faceDown.*visible' "$script"
rg -q 'OfficialCardView.cs' "$scene"
rg -q 'TextureRect' "$scene"
rg -q 'stretch_mode = 5' "$scene"
rg -q 'KeepAspectCentered' "$script"
rg -q 'ReadBool\(_card, "rotated", false\)' "$script"
rg -q 'ClockDirection.Counterclockwise' "$texture_loader"
rg -q 'CardTextureLoader.Load' "$script"
rg -q 'enum OfficialCardVisualState' "$states"
