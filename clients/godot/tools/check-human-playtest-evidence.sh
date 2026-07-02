#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../../.." && pwd)"

usage() {
  cat <<'EOF'
Usage:
  clients/godot/tools/check-human-playtest-evidence.sh /path/to/evidence-dir

The evidence directory should contain the logs and screenshots produced by
run-local-human-playtest.sh after two human players finish a Godot match.
The script verifies machine-checkable gates only; the human operators must still
confirm that the match was played by two humans and that opponent hidden cards
are visible only as backs/counts in the final screenshots.
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

evidence_dir="${1:-}"
if [[ -z "${evidence_dir}" ]]; then
  usage >&2
  exit 2
fi

if [[ ! -d "${evidence_dir}" ]]; then
  echo "Evidence directory not found: ${evidence_dir}" >&2
  exit 2
fi

failures=()
notes=()
report_path="${RIFTBOUND_PLAYTEST_REPORT:-${evidence_dir}/playtest-report.md}"

require_file() {
  local path="$1"
  local label="$2"
  if [[ ! -s "${path}" ]]; then
    failures+=("missing ${label}: ${path}")
  fi
}

require_match() {
  local pattern="$1"
  local path="$2"
  local label="$3"
  if ! rg -q "${pattern}" "${path}"; then
    failures+=("${label} not found in ${path}")
  fi
}

player_a_log="${evidence_dir}/player-a.log"
player_b_log="${evidence_dir}/player-b.log"
player_a_result="${evidence_dir}/player-a-result.png"
player_b_result="${evidence_dir}/player-b-result.png"

require_file "${player_a_log}" "player A log"
require_file "${player_b_log}" "player B log"
require_file "${player_a_result}" "player A result screenshot"
require_file "${player_b_result}" "player B result screenshot"

if [[ -s "${player_a_log}" ]]; then
  require_match "MATCH_STARTED" "${player_a_log}" "MATCH_STARTED"
  require_match "MATCH_WON|Match result rendered" "${player_a_log}" "match result"
  require_match "Visual screenshot saved: .*player-a-result\\.png" "${player_a_log}" "player A result screenshot log"
fi

if [[ -s "${player_b_log}" ]]; then
  require_match "MATCH_STARTED" "${player_b_log}" "MATCH_STARTED"
  require_match "MATCH_WON|Match result rendered" "${player_b_log}" "match result"
  require_match "Visual screenshot saved: .*player-b-result\\.png" "${player_b_log}" "player B result screenshot log"
fi

if compgen -G "${evidence_dir}/*.log" >/dev/null; then
  if rg -n "Message queue out of memory|handle_crash|Exception|ERROR|FATAL|REJECTED|rejected|sharing violation" "${evidence_dir}"/*.log; then
    failures+=("error/rejection pattern found in logs")
  fi

  if rg -q "Auto smoke:" "${evidence_dir}"/*.log; then
    notes+=("auto smoke entries found; this evidence is not sufficient for the two-human P5 gate")
  fi
fi

cat <<EOF
Riftbound Godot human playtest evidence check
  evidence_dir: ${evidence_dir}
  player_a_log: ${player_a_log}
  player_b_log: ${player_b_log}
  player_a_result: ${player_a_result}
  player_b_result: ${player_b_result}
EOF

if (( ${#notes[@]} > 0 )); then
  printf '\nNotes:\n'
  printf '  - %s\n' "${notes[@]}"
fi

if (( ${#failures[@]} > 0 )); then
  printf '\nFAILED:\n' >&2
  printf '  - %s\n' "${failures[@]}" >&2
  exit 1
fi

checked_at="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
git_revision="$(git -C "${repo_root}" rev-parse --short HEAD 2>/dev/null || printf 'unknown')"
{
  cat <<EOF
# Riftbound Godot Human Playtest Report

- Checked at: ${checked_at}
- Git revision: ${git_revision}
- Evidence directory: ${evidence_dir}
- Player A log: ${player_a_log}
- Player B log: ${player_b_log}
- Player A result screenshot: ${player_a_result}
- Player B result screenshot: ${player_b_result}

## Machine Check

- Status: passed
- Required logs: present
- Required result screenshots: present
- Match lifecycle: MATCH_STARTED and MATCH_WON/result rendering observed
- Error scan: no crash/error/rejection patterns found

## Notes
EOF

  if (( ${#notes[@]} > 0 )); then
    printf '\n'
    printf -- '- %s\n' "${notes[@]}"
  else
    printf '\n- None\n'
  fi

  cat <<'EOF'

## Manual Confirmations

- [ ] Two human players operated the two Godot clients.
- [ ] Player A final screenshot shows the server result panel.
- [ ] Player B final screenshot shows the server result panel.
- [ ] Player A sees opponent hand/hidden cards only as card backs and counts.
- [ ] Player B sees opponent hand/hidden cards only as card backs and counts.
EOF
} >"${report_path}"

cat <<'EOF'

Machine-checkable gates passed.
Manual confirmations still required:
  - Two human players operated the two Godot clients.
  - Both final screenshots show the result panel.
  - Each player sees opponent hand/hidden cards only as card backs and counts.
EOF
printf 'Report written: %s\n' "${report_path}"
