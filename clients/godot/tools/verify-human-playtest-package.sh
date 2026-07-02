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

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../../.." && pwd)"
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
checksum_required_files=(
  "README.md"
  "player-a.log"
  "player-b.log"
  "player-a-result.png"
  "player-b-result.png"
  "playtest-report.md"
)
allowed_files=(
  "README.md"
  "SHA256SUMS"
  "api.log"
  "player-a.log"
  "player-b.log"
  "player-a-result.png"
  "player-b-result.png"
  "playtest-report.md"
)
min_result_screenshot_width=800
min_result_screenshot_height=600

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

while IFS= read -r file; do
  relative_file="${file#${bundle_dir}/}"
  allowed=0
  for allowed_file in "${allowed_files[@]}"; do
    if [[ "${relative_file}" == "${allowed_file}" ]]; then
      allowed=1
      break
    fi
  done

  if [[ "${allowed}" != "1" ]]; then
    failures+=("unexpected file in evidence package: ${relative_file}")
  fi
done < <(find "${bundle_dir}" -type f -print)

if (( ${#failures[@]} == 0 )); then
  if ! (cd "${bundle_dir}" && shasum -a 256 -c SHA256SUMS >/dev/null); then
    failures+=("SHA256SUMS verification failed")
  fi

  checksum_covers_file() {
    local file="$1"
    awk -v want="${file}" '
      {
        name = $2
        sub(/^\*/, "", name)
        sub(/^\.\//, "", name)
        if (name == want) {
          found = 1
        }
      }
      END { exit found ? 0 : 1 }
    ' "${bundle_dir}/SHA256SUMS"
  }

  for file in "${checksum_required_files[@]}"; do
    if ! checksum_covers_file "${file}"; then
      failures+=("SHA256SUMS does not cover ${file}")
    fi
  done

  if [[ -s "${bundle_dir}/api.log" ]] && ! checksum_covers_file "api.log"; then
    failures+=("SHA256SUMS does not cover api.log")
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

require_log_literal_match() {
  local expected="$1"
  local path="$2"
  local label="$3"
  if [[ -s "${path}" ]] && ! grep -Fq -- "${expected}" "${path}"; then
    failures+=("${label} missing from $(basename "${path}")")
  fi
}

require_client_setup_log_matches() {
  local path="$1"
  local label="$2"

  require_log_match "Preconstructed decks loaded: [1-9][0-9]*\\." "${path}" "${label} preconstructed deck load"
  require_log_match "SubmitDeck receipt accepted=True" "${path}" "${label} SubmitDeck receipt"
  require_log_match "Ready receipt accepted=True" "${path}" "${label} Ready receipt"
}

require_minimum_png_dimensions() {
  local label="$1"
  local width="$2"
  local height="$3"

  if (( width < min_result_screenshot_width || height < min_result_screenshot_height )); then
    failures+=("${label} is too small for final evidence (${width}x${height}, minimum ${min_result_screenshot_width}x${min_result_screenshot_height})")
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
      return
    fi
    require_minimum_png_dimensions "${label}" "${width}" "${height}"
    return
  fi

  width=$((16#${width_hex}))
  height=$((16#${height_hex}))
  if (( width <= 0 || height <= 0 )); then
    failures+=("${label} has invalid PNG dimensions")
    return
  fi
  require_minimum_png_dimensions "${label}" "${width}" "${height}"
}

require_git_revision_on_main() {
  local revision=""
  local resolved_revision=""

  if [[ ! -s "${report}" ]]; then
    return
  fi

  revision="$(awk -F': ' '/^- Git revision:/ {print $2; exit}' "${report}")"
  if [[ -z "${revision}" || "${revision}" == "unknown" ]]; then
    failures+=("git revision missing or unknown in playtest-report.md")
    return
  fi

  if ! git -C "${repo_root}" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    failures+=("unable to verify git revision outside a git worktree")
    return
  fi

  if ! resolved_revision="$(git -C "${repo_root}" rev-parse --verify --quiet "${revision}^{commit}")"; then
    failures+=("git revision ${revision} not found in local repository")
    return
  fi

  if ! git -C "${repo_root}" rev-parse --verify --quiet origin/main^{commit} >/dev/null; then
    failures+=("origin/main not found for git revision verification")
    return
  fi

  if ! git -C "${repo_root}" merge-base --is-ancestor "${resolved_revision}" origin/main; then
    failures+=("git revision ${revision} is not contained in origin/main")
  fi
}

require_reported_screenshot_log_path() {
  local report_label="$1"
  local log_path="$2"
  local log_label="$3"
  local screenshot_path=""

  if [[ ! -s "${report}" || ! -s "${log_path}" ]]; then
    return
  fi

  screenshot_path="$(awk -F': ' -v label="${report_label}" '$0 ~ "^- " label ": " {print $2; exit}' "${report}")"
  if [[ -z "${screenshot_path}" ]]; then
    failures+=("${report_label} missing from playtest-report.md")
    return
  fi

  require_log_literal_match "Visual screenshot saved: ${screenshot_path}" "${log_path}" "${log_label} result screenshot log path"
}

require_git_revision_on_main
require_report_line "- Git worktree: clean" "clean git worktree"
require_report_line "- Require clean git: 1" "required clean git marker"
if [[ -s "${report}" ]] && grep -Fxq -- "- Incomplete human evidence: 1" "${report}"; then
  failures+=("Incomplete human evidence marker found in playtest-report.md")
else
  require_report_line "- Incomplete human evidence: 0" "complete human evidence marker"
fi
require_report_line "- Manual confirmation mode: 1" "Manual confirmation mode"
require_report_line "- [x] Two human players operated the two Godot clients." "two-human confirmation"
require_report_line "- [x] Player A final screenshot shows the server result panel." "player A result confirmation"
require_report_line "- [x] Player B final screenshot shows the server result panel." "player B result confirmation"
require_report_line "- [x] Player A sees opponent hand/hidden cards only as card backs and counts." "player A hidden-information confirmation"
require_report_line "- [x] Player B sees opponent hand/hidden cards only as card backs and counts." "player B hidden-information confirmation"

if [[ -s "${report}" ]] && grep -Eq '^- \[ \]' "${report}"; then
  failures+=("unchecked manual confirmation found in playtest-report.md")
fi

require_client_setup_log_matches "${player_a_log}" "player A"
require_client_setup_log_matches "${player_b_log}" "player B"
require_log_match "MATCH_STARTED" "${player_a_log}" "MATCH_STARTED"
require_log_match "MATCH_WON|Match result rendered" "${player_a_log}" "match result"
require_log_match "Visual screenshot saved: .*player-a-result\\.png" "${player_a_log}" "player A result screenshot log"
require_log_match "MATCH_STARTED" "${player_b_log}" "MATCH_STARTED"
require_log_match "MATCH_WON|Match result rendered" "${player_b_log}" "match result"
require_log_match "Visual screenshot saved: .*player-b-result\\.png" "${player_b_log}" "player B result screenshot log"
require_reported_screenshot_log_path "Player A result screenshot" "${player_a_log}" "player A"
require_reported_screenshot_log_path "Player B result screenshot" "${player_b_log}" "player B"

if [[ -s "${player_a_log}" && -s "${player_b_log}" ]] && cmp -s "${player_a_log}" "${player_b_log}"; then
  failures+=("player A and player B logs are identical")
fi

require_png_screenshot "${player_a_result}" "player A result screenshot"
require_png_screenshot "${player_b_result}" "player B result screenshot"

if [[ -s "${player_a_result}" && -s "${player_b_result}" ]] && cmp -s "${player_a_result}" "${player_b_result}"; then
  failures+=("player A and player B result screenshots are identical")
fi

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
  report: clean git, clean-git required, manual confirmation mode, git revision on origin/main, all human confirmations checked
  screenshots: valid PNG result screenshots at least ${min_result_screenshot_width}x${min_result_screenshot_height}
  logs: match lifecycle/result screenshots present, no crash/error/rejection patterns
EOF
