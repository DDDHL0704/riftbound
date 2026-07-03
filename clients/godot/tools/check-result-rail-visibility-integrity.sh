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
  "ApplyMatchResult must put the right rail into match-result mode"
require_pattern "SetRightRailMatchResultVisible\\(matchResultVisible: false\\)" \
  "ClearMatchResult must restore the right rail after leaving results"
require_pattern "_officialCardPreviewFrame\\.Visible = !matchResultVisible" \
  "match-result mode must hide the official card preview"
require_pattern "_promptFrame\\.Visible = !matchResultVisible" \
  "match-result mode must hide the prompt panel"
require_pattern "_resultFrame\\.Visible = matchResultVisible" \
  "match-result mode must own result-frame visibility"

echo "Result rail visibility integrity checks passed."
