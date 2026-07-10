#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
scene="$root/clients/godot/scenes/screens/LobbyScreen.tscn"
script="$root/clients/godot/scripts/ui/LobbyScreen.cs"

test -f "$scene"
test -f "$script"
rg -q 'LobbyScreen.cs' "$scene"
rg -q 'class LobbyScreen' "$script"
rg -q 'ApplyTheme' "$script"
rg -q 'MinimalTheme.Apply' "$script"
rg -q 'name="PrimaryFlow"' "$scene"
rg -q 'name="DeckSelect"' "$scene"
rg -q 'name="SubmitDeckButton"' "$scene"
rg -q 'name="ReadyButton"' "$scene"
! rg -q 'name="(SnapshotScroll|PromptScroll|Log)"' "$scene"
