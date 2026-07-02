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
  local suffix="${2:-}"

  # 1x1 transparent PNG.
  if printf '%s' 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=' \
    | base64 -d >"${path}" 2>/dev/null; then
    printf '%s' "${suffix}" >>"${path}"
    return 0
  fi

  printf '%s' 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=' \
    | base64 -D >"${path}"
  printf '%s' "${suffix}" >>"${path}"
}

write_evidence_bundle() {
  local bundle_dir="$1"
  local revision="$2"
  local manual_confirmation_mode="${3:-1}"
  local duplicate_screenshots="${4:-0}"
  local duplicate_logs="${5:-0}"
  local extra_file="${6:-0}"

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

  if [[ "${duplicate_logs}" == "1" ]]; then
    cat >"${bundle_dir}/player-a.log" <<'EOF'
MATCH_STARTED
MATCH_WON
Visual screenshot saved: /tmp/player-a-result.png
Visual screenshot saved: /tmp/player-b-result.png
EOF
    cp "${bundle_dir}/player-a.log" "${bundle_dir}/player-b.log"
  fi

  if [[ "${duplicate_screenshots}" == "1" ]]; then
    write_png "${bundle_dir}/player-a-result.png"
    write_png "${bundle_dir}/player-b-result.png"
  else
    write_png "${bundle_dir}/player-a-result.png" "player-a"
    write_png "${bundle_dir}/player-b-result.png" "player-b"
  fi

  cat >"${bundle_dir}/playtest-report.md" <<EOF
# Riftbound Godot Human Playtest Report

- Git revision: ${revision}
- Git worktree: clean
- Require clean git: 1

## Machine Check

- Status: passed
- Manual confirmation mode: ${manual_confirmation_mode}

## Manual Confirmations

- [x] Two human players operated the two Godot clients.
- [x] Player A final screenshot shows the server result panel.
- [x] Player B final screenshot shows the server result panel.
- [x] Player A sees opponent hand/hidden cards only as card backs and counts.
- [x] Player B sees opponent hand/hidden cards only as card backs and counts.
EOF

  if [[ "${extra_file}" == "1" ]]; then
    printf 'unexpected package payload\n' >"${bundle_dir}/secret.txt"
  fi
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

manual_mode_bundle="${tmp_dir}/manual-mode/riftbound-human-playtest-evidence"
write_evidence_bundle "${manual_mode_bundle}" "${revision}" "0"
(
  cd "${manual_mode_bundle}"
  shasum -a 256 README.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md > SHA256SUMS
)
manual_mode_package="${tmp_dir}/manual-mode-zero.tar.gz"
make_package "${manual_mode_bundle}" "${manual_mode_package}"

manual_mode_output="${tmp_dir}/manual-mode-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${manual_mode_package}" >"${manual_mode_output}" 2>&1; then
  fail "verifier accepted package whose report was not produced with manual confirmation mode"
fi

if ! rg -q "Manual confirmation mode" "${manual_mode_output}"; then
  echo "Expected manual confirmation mode rejection output:" >&2
  cat "${manual_mode_output}" >&2
  fail "verifier did not explain the missing manual confirmation mode"
fi

duplicate_screenshot_bundle="${tmp_dir}/duplicate-screenshot/riftbound-human-playtest-evidence"
write_evidence_bundle "${duplicate_screenshot_bundle}" "${revision}" "1" "1"
(
  cd "${duplicate_screenshot_bundle}"
  shasum -a 256 README.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md > SHA256SUMS
)
duplicate_screenshot_package="${tmp_dir}/duplicate-screenshot.tar.gz"
make_package "${duplicate_screenshot_bundle}" "${duplicate_screenshot_package}"

duplicate_screenshot_output="${tmp_dir}/duplicate-screenshot-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${duplicate_screenshot_package}" >"${duplicate_screenshot_output}" 2>&1; then
  fail "verifier accepted package with identical player A/B result screenshots"
fi

if ! rg -q "result screenshots.*identical|identical.*result screenshots" "${duplicate_screenshot_output}"; then
  echo "Expected duplicate screenshot rejection output:" >&2
  cat "${duplicate_screenshot_output}" >&2
  fail "verifier did not explain the duplicate result screenshots"
fi

duplicate_log_bundle="${tmp_dir}/duplicate-log/riftbound-human-playtest-evidence"
write_evidence_bundle "${duplicate_log_bundle}" "${revision}" "1" "0" "1"
(
  cd "${duplicate_log_bundle}"
  shasum -a 256 README.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md > SHA256SUMS
)
duplicate_log_package="${tmp_dir}/duplicate-log.tar.gz"
make_package "${duplicate_log_bundle}" "${duplicate_log_package}"

duplicate_log_output="${tmp_dir}/duplicate-log-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${duplicate_log_package}" >"${duplicate_log_output}" 2>&1; then
  fail "verifier accepted package with identical player A/B logs"
fi

if ! rg -q "player A and player B logs are identical|logs.*identical|identical.*logs" "${duplicate_log_output}"; then
  echo "Expected duplicate log rejection output:" >&2
  cat "${duplicate_log_output}" >&2
  fail "verifier did not explain the duplicate player logs"
fi

extra_file_bundle="${tmp_dir}/extra-file/riftbound-human-playtest-evidence"
write_evidence_bundle "${extra_file_bundle}" "${revision}" "1" "0" "0" "1"
(
  cd "${extra_file_bundle}"
  shasum -a 256 README.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md > SHA256SUMS
)
extra_file_package="${tmp_dir}/extra-file.tar.gz"
make_package "${extra_file_bundle}" "${extra_file_package}"

extra_file_output="${tmp_dir}/extra-file-output.log"
if "${script_dir}/verify-human-playtest-package.sh" "${extra_file_package}" >"${extra_file_output}" 2>&1; then
  fail "verifier accepted package with an unexpected extra file"
fi

if ! rg -q "unexpected file|extra file|secret\\.txt" "${extra_file_output}"; then
  echo "Expected unexpected-file rejection output:" >&2
  cat "${extra_file_output}" >&2
  fail "verifier did not explain the unexpected package file"
fi

covered_bundle="${tmp_dir}/covered/riftbound-human-playtest-evidence"
write_evidence_bundle "${covered_bundle}" "${revision}" "1" "0" "0" "0"
(
  cd "${covered_bundle}"
  shasum -a 256 README.md player-a.log player-b.log player-a-result.png player-b-result.png playtest-report.md > SHA256SUMS
)
covered_package="${tmp_dir}/covered-checksum.tar.gz"
make_package "${covered_bundle}" "${covered_package}"

"${script_dir}/verify-human-playtest-package.sh" "${covered_package}" >/dev/null

echo "Human playtest package integrity checks passed."
