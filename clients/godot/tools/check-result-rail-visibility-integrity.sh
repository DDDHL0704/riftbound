#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
main_cs="${repo_root}/clients/godot/scripts/Main.cs"

fail() {
  echo "FAILED: $*" >&2
  exit 1
}

require_pattern() {
  local pattern="$1"
  local message="$2"

  if ! rg -q "${pattern}" "${main_cs}"; then
    fail "${message}"
  fi
}

require_pattern "SetRightRailMatchResultVisible\\(matchResultVisible: true\\)" \
  "ApplyMatchResult must show the right-rail result panel"
require_pattern "SetRightRailMatchResultVisible\\(matchResultVisible: false\\)" \
  "ClearMatchResult must hide the right-rail result panel after leaving results"
require_pattern "_resultFrame\\.Visible = matchResultVisible" \
  "match-result mode must own result-frame visibility"
require_pattern "_matchFinished \\|\\| battleActive" \
  "match-result mode must keep battle chrome locked even if a stale room snapshot applies later"
require_pattern "_matchFinished && !battleActive" \
  "match-result mode must ignore stale non-battle snapshot sections after the result is shown"

if rg -q "_officialCardPreviewFrame\\.Visible = !matchResultVisible|_promptFrame\\.Visible = !matchResultVisible" "${main_cs}"; then
  fail "match-result mode must preserve the black/ivory right preview-prompt rail instead of blanking it"
fi

echo "Result rail visibility integrity checks passed."
