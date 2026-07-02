#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../../.." && pwd)"

godot_bin="${RIFTBOUND_GODOT_BIN:-/Applications/Godot_dotnet.app/Contents/MacOS/Godot}"
server="${RIFTBOUND_SERVER:-http://127.0.0.1:5088}"
room="${RIFTBOUND_ROOM:-human-local-$(date +%H%M%S)}"
resolution="${RIFTBOUND_RESOLUTION:-1440x900}"
screenshot_dir="${RIFTBOUND_SCREENSHOT_DIR:-/tmp/riftbound-human-playtest-${room}}"
wait_for_windows="${RIFTBOUND_WAIT:-1}"
min_table_cards="${RIFTBOUND_SCREENSHOT_MIN_TABLE_CARDS:-0}"
quit_after="${RIFTBOUND_QUIT_AFTER:-}"

handle_a="${RIFTBOUND_HANDLE_A:-player-a-${room}}"
handle_b="${RIFTBOUND_HANDLE_B:-player-b-${room}}"
player_key_a="${RIFTBOUND_PLAYER_KEY_A:-pk_${room}_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa}"
player_key_b="${RIFTBOUND_PLAYER_KEY_B:-pk_${room}_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb}"

if [[ ! -x "${godot_bin}" ]]; then
  echo "Godot executable not found: ${godot_bin}" >&2
  echo "Set RIFTBOUND_GODOT_BIN to the Godot 4 .NET binary." >&2
  exit 1
fi

if ! curl -fsS "${server}/health" >/dev/null; then
  echo "Riftbound API is not reachable at ${server}/health." >&2
  echo "Start the API first, or set RIFTBOUND_SERVER to the LAN/public URL." >&2
  exit 1
fi

mkdir -p "${screenshot_dir}"
launched_pid=""

launch_player() {
  local seat="$1"
  local handle="$2"
  local player_key="$3"
  local position="$4"
  local shot="${screenshot_dir}/${seat}.png"
  local log="${screenshot_dir}/${seat}.log"
  local godot_window_args=(
    --windowed --resolution "${resolution}" --position "${position}"
    --scene res://scenes/Main.tscn
    --path "${repo_root}/clients/godot"
  )

  if [[ -n "${quit_after}" ]]; then
    godot_window_args+=(--quit-after "${quit_after}")
  fi

  local launcher=("${godot_bin}")
  if [[ "${wait_for_windows}" == "0" ]]; then
    launcher=(nohup "${godot_bin}")
  fi

  "${launcher[@]}" "${godot_window_args[@]}" -- \
    --riftbound-server="${server}" \
    --riftbound-ephemeral-session \
    --riftbound-ignore-reconnect \
    --riftbound-room="${room}" \
    --riftbound-handle="${handle}" \
    --riftbound-player-key="${player_key}" \
    --riftbound-visual-screenshot="${shot}" \
    --riftbound-visual-screenshot-min-table-cards="${min_table_cards}" \
    >"${log}" 2>&1 &
  launched_pid="$!"
}

launch_player player-a "${handle_a}" "${player_key_a}" "20,60"
pid_a="${launched_pid}"
launch_player player-b "${handle_b}" "${player_key_b}" "240,120"
pid_b="${launched_pid}"

cat <<EOF
Started Riftbound Godot human playtest.
  server: ${server}
  room: ${room}
  player A: ${handle_a} pid=${pid_a}
  player B: ${handle_b} pid=${pid_b}
  evidence dir: ${screenshot_dir}

Manual flow:
  1. Each player chooses a preconstructed deck.
  2. Each player clicks Submit Deck, then Ready.
  3. Complete mulligan and play to the server result panel.
  4. Keep both final result screenshots and verify opponent hands show only card backs/counts.
EOF

if [[ "${wait_for_windows}" != "0" ]]; then
  wait "${pid_a}" "${pid_b}"
  cat <<EOF

Both Godot windows exited.
Check evidence with:
  clients/godot/tools/check-human-playtest-evidence.sh "${screenshot_dir}"
EOF
fi
