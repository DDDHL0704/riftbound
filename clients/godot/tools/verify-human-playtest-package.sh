#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  clients/godot/tools/verify-human-playtest-package.sh /path/to/evidence.tar.gz

Verifies a packaged Godot two-human playtest evidence tarball for the final
Playable v1 handoff. This checks only machine-readable package gates; the
human confirmations must already be recorded in playtest-report.md.
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

package_path="${1:-}"
if [[ -z "${package_path}" ]]; then
  usage >&2
  exit 2
fi

if [[ ! -s "${package_path}" ]]; then
  echo "Evidence package not found: ${package_path}" >&2
  exit 2
fi

failures=()
required_files=(
  "README.md"
  "SHA256SUMS"
  "player-a.log"
  "player-b.log"
  "player-a-result.png"
  "player-b-result.png"
  "playtest-report.md"
)

staging_dir="$(mktemp -d)"
cleanup() {
  rm -rf "${staging_dir}"
}
trap cleanup EXIT

if ! tar -xzf "${package_path}" -C "${staging_dir}"; then
  echo "Unable to extract evidence package: ${package_path}" >&2
  exit 1
fi

bundle_dir="${staging_dir}/riftbound-human-playtest-evidence"
if [[ ! -d "${bundle_dir}" ]]; then
  echo "Evidence bundle directory missing: riftbound-human-playtest-evidence" >&2
  exit 1
fi

for file in "${required_files[@]}"; do
  if [[ ! -s "${bundle_dir}/${file}" ]]; then
    failures+=("missing ${file}")
  fi
done

if (( ${#failures[@]} == 0 )); then
  if ! (cd "${bundle_dir}" && shasum -a 256 -c SHA256SUMS >/dev/null); then
    failures+=("SHA256SUMS verification failed")
  fi
fi

report="${bundle_dir}/playtest-report.md"
player_a_log="${bundle_dir}/player-a.log"
player_b_log="${bundle_dir}/player-b.log"
player_a_result="${bundle_dir}/player-a-result.png"
player_b_result="${bundle_dir}/player-b-result.png"

require_report_line() {
  local line="$1"
  local label="$2"
  if [[ -s "${report}" ]] && ! grep -Fxq -- "${line}" "${report}"; then
    failures+=("${label} missing from playtest-report.md")
  fi
}

require_log_match() {
  local pattern="$1"
  local path="$2"
  local label="$3"
  if [[ -s "${path}" ]] && ! rg -q "${pattern}" "${path}"; then
    failures+=("${label} missing from $(basename "${path}")")
  fi
}

require_png_screenshot() {
  local path="$1"
  local label="$2"
  local header=""
  local signature=""
  local ihdr_length=""
  local ihdr_type=""
  local width_hex=""
  local height_hex=""
  local sips_output=""
  local width=0
  local height=0

  if [[ ! -s "${path}" ]]; then
    return
  fi

  header="$(od -An -tx1 -N24 "${path}" | tr -d ' \n')"
  signature="${header:0:16}"
  ihdr_length="${header:16:8}"
  ihdr_type="${header:24:8}"
  width_hex="${header:32:8}"
  height_hex="${header:40:8}"

  if [[ "${signature}" != "89504e470d0a1a0a" || "${ihdr_length}" != "0000000d" || "${ihdr_type}" != "49484452" ]]; then
    failures+=("${label} is not a PNG screenshot")
    return
  fi

  if command -v sips >/dev/null 2>&1; then
    if ! sips_output="$(sips -g pixelWidth -g pixelHeight "${path}" 2>/dev/null)"; then
      failures+=("${label} is not a readable PNG screenshot")
      return
    fi

    width="$(awk '/pixelWidth:/ {print $2}' <<<"${sips_output}")"
    height="$(awk '/pixelHeight:/ {print $2}' <<<"${sips_output}")"
    if [[ ! "${width}" =~ ^[0-9]+$ || ! "${height}" =~ ^[0-9]+$ || "${width}" == "0" || "${height}" == "0" ]]; then
      failures+=("${label} has invalid PNG dimensions")
    fi
    return
  fi

  width=$((16#${width_hex}))
  height=$((16#${height_hex}))
  if (( width <= 0 || height <= 0 )); then
    failures+=("${label} has invalid PNG dimensions")
  fi
}

require_report_line "- Git worktree: clean" "clean git worktree"
require_report_line "- Require clean git: 1" "required clean git marker"
require_report_line "- [x] Two human players operated the two Godot clients." "two-human confirmation"
require_report_line "- [x] Player A final screenshot shows the server result panel." "player A result confirmation"
require_report_line "- [x] Player B final screenshot shows the server result panel." "player B result confirmation"
require_report_line "- [x] Player A sees opponent hand/hidden cards only as card backs and counts." "player A hidden-information confirmation"
require_report_line "- [x] Player B sees opponent hand/hidden cards only as card backs and counts." "player B hidden-information confirmation"

if [[ -s "${report}" ]] && grep -Eq '^- \[ \]' "${report}"; then
  failures+=("unchecked manual confirmation found in playtest-report.md")
fi

require_log_match "MATCH_STARTED" "${player_a_log}" "MATCH_STARTED"
require_log_match "MATCH_WON|Match result rendered" "${player_a_log}" "match result"
require_log_match "Visual screenshot saved: .*player-a-result\\.png" "${player_a_log}" "player A result screenshot log"
require_log_match "MATCH_STARTED" "${player_b_log}" "MATCH_STARTED"
require_log_match "MATCH_WON|Match result rendered" "${player_b_log}" "match result"
require_log_match "Visual screenshot saved: .*player-b-result\\.png" "${player_b_log}" "player B result screenshot log"
require_png_screenshot "${player_a_result}" "player A result screenshot"
require_png_screenshot "${player_b_result}" "player B result screenshot"

if compgen -G "${bundle_dir}/*.log" >/dev/null; then
  if rg -n "Message queue out of memory|handle_crash|Exception|ERROR|FATAL|REJECTED|rejected|sharing violation" "${bundle_dir}"/*.log >/dev/null; then
    failures+=("error/rejection pattern found in logs")
  fi

  if rg -q "Auto smoke:" "${bundle_dir}"/*.log; then
    failures+=("auto smoke entries found in logs")
  fi
fi

if [[ -s "${report}" ]] && rg -qi "auto smoke" "${report}"; then
  failures+=("auto smoke note found in playtest-report.md")
fi

if (( ${#failures[@]} > 0 )); then
  printf 'FAILED evidence package verification:\n' >&2
  printf '  - %s\n' "${failures[@]}" >&2
  exit 1
fi

cat <<EOF
P5 evidence package passed machine verification:
  package: ${package_path}
  required files: present
  checksums: valid
  report: clean git, clean-git required, all human confirmations checked
  screenshots: valid PNG result screenshots
  logs: match lifecycle/result screenshots present, no crash/error/rejection patterns
EOF
