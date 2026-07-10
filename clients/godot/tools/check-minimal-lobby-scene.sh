#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
scene="$root/clients/godot/scenes/screens/LobbyScreen.tscn"
script="$root/clients/godot/scripts/ui/LobbyScreen.cs"
main_scene="$root/clients/godot/scenes/Main.tscn"
main_script="$root/clients/godot/scripts/Main.cs"

test -f "$scene"
test -f "$script"
test -f "$main_scene"
test -f "$main_script"
rg -q 'LobbyScreen.cs' "$scene"
rg -q 'class LobbyScreen' "$script"
rg -q 'ApplyTheme' "$script"
rg -q 'MinimalTheme.Apply' "$script"
rg -q 'name="PrimaryFlow"' "$scene"
rg -q 'name="DeckSelect"' "$scene"
rg -q 'name="SubmitDeckButton"' "$scene"
rg -q 'name="ReadyButton"' "$scene"
! rg -q 'name="(SnapshotScroll|PromptScroll|Log)"' "$scene"

# The focused lobby must not overlap the legacy match rail.
awk '
  /\[node name="(OfficialCardPreviewFrame|PromptFrame|ResultFrame)"/ { in_panel = 1; visible = 0; next }
  /^\[node / && in_panel { if (!visible) exit 1; in_panel = 0 }
  in_panel && /^visible = false$/ { visible = 1 }
  END { if (in_panel && !visible) exit 1 }
' "$main_scene"
rg -q '_officialCardPreviewFrame.Visible = battleActive' "$main_script"
rg -q '_promptFrame.Visible = battleActive' "$main_script"
rg -A4 '\[node name="LobbyScreen" parent="Controls"' "$main_scene" \
  | rg -q '^size_flags_vertical = 3$'

# Lobby affordances mirror server prompt candidates and never expose internal player IDs.
rg -q 'RefreshLobbySetupStateFromPrompt' "$main_script"
rg -q 'ResetLobbyPromptState' "$main_script"
rg -q 'SUBMIT_DECK' "$main_script"
rg -q 'READY' "$main_script"
! rg -q 'hostPlayerId' "$script"
! rg -q 'status.PlayerId|status.OpponentPlayerId' "$main_script"
! rg -q 'SetMatchmakingStatus\(.*HostPlayerId' "$main_script"

for method in ConnectAndRequestSnapshotAsync CreatePublicMatchAsync QueueMatchmakingAsync JoinPublicMatchAsync; do
  sed -n "/private .* ${method}(/,/^    }/p" "$main_script" \
    | rg -q 'ResetLobbyPromptState\(\);'
done

# Result presentation keeps transport and identity diagnostics out of the UI.
! rg -q '服务端 tick：|来源：' "$main_script"
! rg -q '胜者：\{winnerPlayerId\}|投降：\{surrenderedPlayerId\}' "$main_script"

# The minimal client no longer installs the legacy procedural table backdrop.
! rg -q '^        InstallRunestoneBackdrop\(\);$' "$main_script"
