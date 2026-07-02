#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../../.." && pwd)"

fail() {
  echo "FAILED: $*" >&2
  exit 1
}

write_png() {
  local path="$1"
  # 1x1 transparent PNG.
  if printf '%s' 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=' \
    | base64 -d >"${path}" 2>/dev/null; then
    return 0
  fi

  printf '%s' 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=' \
    | base64 -D >"${path}"
}

write_evidence_bundle() {
  local bundle_dir="$1"
  local revision="$2"

  mkdir -p "${bundle_dir}"
  cat >"${bundle_dir}/README.md" <<'EOF'
# Riftbound Godot Human Playtest Evidence

Package integrity fixture.
EOF

  cat >"${bundle_dir}/player-a.log" <<'EOF'
MATCH_STARTED
Match result rendered
Visual screenshot saved: /tmp/player-a-result.png
EOF

  cat >"${bundle_dir}/player-b.log" <<'EOF'
MATCH_STARTED
MATCH_WON
Visual screenshot saved: /tmp/player-b-result.png
EOF

  write_png "${bundle_dir}/player-a-result.png"
  write_png "${bundle_dir}/player-b-result.png"

  cat >"${bundle_dir}/playtest-report.md" <<EOF
# Riftbound Godot Human Playtest Report

- Git revision: ${revision}
- Git worktree: clean
- Require clean git: 1

## Machine Check

- Status: passed

## Manual Confirmations

- [x] Two human players operated the two Godot clients.
- [x] Player A final screenshot shows the server result panel.
- [x] Player B final screenshot shows the server result panel.
- [x] Player A sees opponent hand/hidden cards only as card backs and counts.
- [x] Player B sees opponent hand/hidden cards only as card backs and counts.
EOF
}

make_package() {
  local bundle_dir="$1"
  local package_path="$2"
  local staging_dir

  staging_dir="$(dirname "${bundle_dir}")"
  tar -czf "${package_path}" -C "${staging_dir}" riftbound-human-playtest-evidence
}

tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/riftbound-package-integrity.XXXXXX")"
trap 'rm -rf "${tmp_dir}"' EXIT

revision="$(git -C "${repo_root}" rev-parse --short HEAD)"

missing_checksum_bundle="${tmp_dir}/missing/riftbound-human-playtest-evidence"
write_evidence_bundle "${missing_checksum_bundle}" "${revision}"
(
  cd "${missing_checksum_bundle}"
  shasum -a 256 player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md > SHA256SUMS
)
missing_checksum_package="${tmp_dir}/missing-readme-checksum.tar.gz"
make_package "${missing_checksum_bundle}" "${missing_checksum_package}"

missing_output="${tmp_dir}/missing-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${missing_checksum_package}" >"${missing_output}" 2>&1; then
  fail "verifier accepted package whose SHA256SUMS did not cover README.md"
fi

if ! rg -q "SHA256SUMS.*README\\.md|README\\.md.*SHA256SUMS" "${missing_output}"; then
  echo "Expected checksum coverage rejection output:" >&2
  cat "${missing_output}" >&2
  fail "verifier did not explain the missing README.md checksum coverage"
fi

covered_bundle="${tmp_dir}/covered/riftbound-human-playtest-evidence"
write_evidence_bundle "${covered_bundle}" "${revision}"
(
  cd "${covered_bundle}"
  shasum -a 256 README.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md > SHA256SUMS
)
covered_package="${tmp_dir}/covered-checksum.tar.gz"
make_package "${covered_bundle}" "${covered_package}"

"${script_dir}/verify-human-playtest-package.sh" "${covered_package}" >/dev/null

echo "Human playtest package integrity checks passed."
