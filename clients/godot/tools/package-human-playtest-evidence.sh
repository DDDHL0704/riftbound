#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  clients/godot/tools/package-human-playtest-evidence.sh /path/to/evidence-dir [output.tar.gz]

Runs the Godot human playtest evidence checker, then packages the logs,
result screenshots, generated report, operator guide, visual review checklist,
handoff summary, and SHA-256 checksums into a tarball.

Set RIFTBOUND_CONFIRM_MANUAL=1 to have the checker prompt for the human-only
confirmations before packaging.
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

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
evidence_dir="$(cd "${evidence_dir}" && pwd)"
stamp="$(date -u +"%Y%m%dT%H%M%SZ")"
output_path="${2:-${evidence_dir}/riftbound-human-playtest-evidence-${stamp}.tar.gz}"
report_path="${RIFTBOUND_PLAYTEST_REPORT:-${evidence_dir}/playtest-report.md}"
output_dir="$(dirname "${output_path}")"
mkdir -p "${output_dir}"
output_path="$(cd "${output_dir}" && pwd)/$(basename "${output_path}")"

if [[ -s "${report_path}" && "${RIFTBOUND_CONFIRM_MANUAL:-0}" != "1" ]]; then
  machine_check_report="${report_path}.machine-check"
  machine_check_log="$(mktemp)"
  if RIFTBOUND_PLAYTEST_REPORT="${machine_check_report}" \
    "${script_dir}/check-human-playtest-evidence.sh" "${evidence_dir}" >"${machine_check_log}" 2>&1; then
    rm -f "${machine_check_report}" "${machine_check_log}"
    echo "Machine recheck passed; preserving existing report: ${report_path}"
  else
    cat "${machine_check_log}" >&2 || true
    rm -f "${machine_check_log}"
    exit 1
  fi
else
  "${script_dir}/check-human-playtest-evidence.sh" "${evidence_dir}"
fi

required_files=(
  "player-a.log"
  "player-b.log"
  "player-a-result.png"
  "player-b-result.png"
)

for file in "${required_files[@]}"; do
  if [[ ! -s "${evidence_dir}/${file}" ]]; then
    echo "Required evidence file missing after check: ${evidence_dir}/${file}" >&2
    exit 1
  fi
done

if [[ ! -s "${report_path}" ]]; then
  echo "Required evidence report missing after check: ${report_path}" >&2
  exit 1
fi

staging_dir="$(mktemp -d)"
cleanup() {
  rm -rf "${staging_dir}"
}
trap cleanup EXIT

bundle_name="riftbound-human-playtest-evidence"
bundle_dir="${staging_dir}/${bundle_name}"
mkdir -p "${bundle_dir}"

report_field() {
  local label="$1"
  local prefix="- ${label}: "

  awk -v prefix="${prefix}" '
    index($0, prefix) == 1 {
      print substr($0, length(prefix) + 1)
      exit
    }
  ' "${report_path}"
}

for file in "${required_files[@]}"; do
  cp "${evidence_dir}/${file}" "${bundle_dir}/${file}"
done
cp "${report_path}" "${bundle_dir}/playtest-report.md"

room="$(report_field "Room")"
player_a_handle="$(report_field "Player A handle")"
player_b_handle="$(report_field "Player B handle")"
git_revision="$(report_field "Git revision")"
manual_confirmation_mode="$(report_field "Manual confirmation mode")"
inksteel_style="$(report_field "Inksteel style")"
hidden_information_boundary="$(report_field "Hidden information boundary")"

cat >"${bundle_dir}/P5_HANDOFF.md" <<EOF
# Riftbound Godot P5 Handoff

- Packaged at: $(date -u +"%Y-%m-%dT%H:%M:%SZ")
- Git revision: ${git_revision}
- Room: ${room}
- Player A handle: ${player_a_handle}
- Player B handle: ${player_b_handle}
- Player A result screenshot: player-a-result.png
- Player B result screenshot: player-b-result.png
- Report: playtest-report.md
- Inksteel style: ${inksteel_style}
- Hidden information boundary: ${hidden_information_boundary}
- Manual confirmation mode: ${manual_confirmation_mode}

This handoff summary is machine generated from playtest-report.md and is only
valid final P5 evidence when playtest-report.md has all manual confirmations
checked after a real two-human Godot match.
EOF

cat >"${bundle_dir}/VISUAL_REVIEW.md" <<EOF
# Riftbound Godot Visual Review

- Room: ${room}
- Player A handle: ${player_a_handle}
- Player B handle: ${player_b_handle}
- Player A result screenshot: player-a-result.png
- Player B result screenshot: player-b-result.png
- Report: playtest-report.md
- Machine inksteel style: ${inksteel_style}
- Machine hidden-information boundary: ${hidden_information_boundary}

Before accepting this package as final P5 evidence, inspect both result
screenshots and confirm:

- Both screenshots show the server result panel.
- Player A sees opponent hand and hidden cards only as card backs and counts.
- Player B sees opponent hand and hidden cards only as card backs and counts.
- No opponent hidden card face, name, text, or identity is visible in either
  screenshot.

The machine verifier can prove this checklist exists and is checksummed, but the
hidden-information review is a human visual confirmation recorded in
playtest-report.md.
EOF

if [[ -s "${evidence_dir}/OPERATOR_GUIDE.md" ]]; then
  cp "${evidence_dir}/OPERATOR_GUIDE.md" "${bundle_dir}/OPERATOR_GUIDE.md"
else
  cat >"${bundle_dir}/OPERATOR_GUIDE.md" <<EOF
# Riftbound Godot P5 Operator Guide

- Room: ${room}
- Player A handle: ${player_a_handle}
- Player B handle: ${player_b_handle}
- Evidence directory: ${evidence_dir}
- Evidence package: ${output_path}
- Playtest report: playtest-report.md

## Final P5 operator checklist

1. Two human players operate the two Godot clients.
2. Both players use preconstructed decks, submit decks, and ready up.
3. Play the match to the server result panel on both clients.
4. Confirm both final screenshots show the server result panel.
5. Confirm each player sees opponent hand and hidden cards only as card backs/counts.
6. Answer the manual confirmation prompts only after checking the final screenshots.

This fallback guide was generated during packaging because the source evidence
directory did not contain OPERATOR_GUIDE.md.
EOF
fi

cat >"${bundle_dir}/README.md" <<EOF
# Riftbound Godot Human Playtest Evidence

- Packaged at: $(date -u +"%Y-%m-%dT%H:%M:%SZ")
- Source evidence directory: ${evidence_dir}
- Report: playtest-report.md
- Operator guide: OPERATOR_GUIDE.md
- P5 handoff summary: P5_HANDOFF.md
- Visual review checklist: VISUAL_REVIEW.md
- Machine inksteel style: ${inksteel_style}
- Machine hidden-information boundary: ${hidden_information_boundary}
- Checksums: SHA256SUMS

This package is evidence material only. It does not prove the human-only
confirmations unless playtest-report.md contains checked manual confirmation
boxes produced after the real two-player playtest.
EOF

checksum_files=(
  "README.md"
  "OPERATOR_GUIDE.md"
  "P5_HANDOFF.md"
  "VISUAL_REVIEW.md"
  "player-a.log"
  "player-b.log"
  "player-a-result.png"
  "player-b-result.png"
  "playtest-report.md"
)

if [[ -s "${evidence_dir}/api.log" ]]; then
  cp "${evidence_dir}/api.log" "${bundle_dir}/api.log"
  checksum_files+=("api.log")
fi

(
  cd "${bundle_dir}"
  shasum -a 256 "${checksum_files[@]}" > SHA256SUMS
)

tar -czf "${output_path}" -C "${staging_dir}" "${bundle_name}"

echo "Evidence package written: ${output_path}"
