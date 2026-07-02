#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  clients/godot/tools/package-human-playtest-evidence.sh /path/to/evidence-dir [output.tar.gz]

Runs the Godot human playtest evidence checker, then packages the logs,
result screenshots, generated report, and SHA-256 checksums into a tarball.

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
  RIFTBOUND_PLAYTEST_REPORT="${report_path}.machine-check" \
    "${script_dir}/check-human-playtest-evidence.sh" "${evidence_dir}"
  rm -f "${report_path}.machine-check"
else
  "${script_dir}/check-human-playtest-evidence.sh" "${evidence_dir}"
fi

required_files=(
  "player-a.log"
  "player-b.log"
  "player-a-result.png"
  "player-b-result.png"
  "playtest-report.md"
)

for file in "${required_files[@]}"; do
  if [[ ! -s "${evidence_dir}/${file}" ]]; then
    echo "Required evidence file missing after check: ${evidence_dir}/${file}" >&2
    exit 1
  fi
done

staging_dir="$(mktemp -d)"
cleanup() {
  rm -rf "${staging_dir}"
}
trap cleanup EXIT

bundle_name="riftbound-human-playtest-evidence"
bundle_dir="${staging_dir}/${bundle_name}"
mkdir -p "${bundle_dir}"

for file in "${required_files[@]}"; do
  cp "${evidence_dir}/${file}" "${bundle_dir}/${file}"
done

if [[ -s "${evidence_dir}/api.log" ]]; then
  cp "${evidence_dir}/api.log" "${bundle_dir}/api.log"
fi

(
  cd "${bundle_dir}"
  shasum -a 256 ./* > SHA256SUMS
)

cat >"${bundle_dir}/README.md" <<EOF
# Riftbound Godot Human Playtest Evidence

- Packaged at: $(date -u +"%Y-%m-%dT%H:%M:%SZ")
- Source evidence directory: ${evidence_dir}
- Report: playtest-report.md
- Checksums: SHA256SUMS

This package is evidence material only. It does not prove the human-only
confirmations unless playtest-report.md contains checked manual confirmation
boxes produced after the real two-player playtest.
EOF

tar -czf "${output_path}" -C "${staging_dir}" "${bundle_name}"

echo "Evidence package written: ${output_path}"
