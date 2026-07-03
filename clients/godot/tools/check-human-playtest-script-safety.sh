#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

fail() {
  echo "FAILED: $*" >&2
  exit 1
}

make_fake_git() {
  local fake_bin="$1"
  local log_path="$2"

  mkdir -p "${fake_bin}"
  cat >"${fake_bin}/git" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

printf 'git %s\n' "$*" >>"${RIFTBOUND_FAKE_GIT_LOG}"

if [[ "${1:-}" == "-C" ]]; then
  shift 2
fi

case "${1:-} ${2:-} ${3:-}" in
  "fetch origin main")
    exit 0
    ;;
  "worktree add --detach")
    destination="${4:?missing worktree destination}"
    mkdir -p "${destination}/clients/godot/tools"
    cat >"${destination}/clients/godot/tools/run-local-human-playtest-stack.sh" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail
echo "wrapped human stack ran"
echo "incomplete evidence marker: ${RIFTBOUND_INCOMPLETE_HUMAN_EVIDENCE:-<unset>}"
STUB
    chmod +x "${destination}/clients/godot/tools/run-local-human-playtest-stack.sh"
    cat >"${destination}/clients/godot/tools/verify-human-playtest-package.sh" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail
echo "verify evidence package ran"
STUB
    chmod +x "${destination}/clients/godot/tools/verify-human-playtest-package.sh"
    exit 0
    ;;
  "worktree remove --force")
    rm -rf "${4:?missing worktree destination}"
    exit 0
    ;;
esac

echo "unexpected fake git invocation: $*" >&2
exit 3
EOF
  chmod +x "${fake_bin}/git"
  : >"${log_path}"
}

make_fake_curl() {
  local fake_bin="$1"

  cat >"${fake_bin}/curl" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

if [[ "${RIFTBOUND_FAKE_CURL_HEALTHY:-0}" == "1" ]]; then
  exit 0
fi

exit 22
EOF
  chmod +x "${fake_bin}/curl"
}

tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/riftbound-human-script-safety.XXXXXX")"
trap 'rm -rf "${tmp_dir}"' EXIT

fake_bin="${tmp_dir}/bin"
fake_git_log="${tmp_dir}/git.log"
make_fake_git "${fake_bin}" "${fake_git_log}"
make_fake_curl "${fake_bin}"

make_fake_executable() {
  local path="$1"

  cat >"${path}" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
exit 0
EOF
  chmod +x "${path}"
}

fake_godot_bin="${tmp_dir}/Godot"
fake_dotnet_bin="${tmp_dir}/dotnet"
make_fake_executable "${fake_godot_bin}"
make_fake_executable "${fake_dotnet_bin}"

auto_output="${tmp_dir}/auto-output.log"
auto_worktree="${tmp_dir}/auto-worktree"
if PATH="${fake_bin}:${PATH}" \
  RIFTBOUND_FAKE_GIT_LOG="${fake_git_log}" \
  RIFTBOUND_CLEAN_WORKTREE_FETCH=0 \
  RIFTBOUND_CLEAN_WORKTREE_DIR="${auto_worktree}" \
  RIFTBOUND_KEEP_CLEAN_WORKTREE=1 \
  RIFTBOUND_EXTRA_GODOT_ARGS="--riftbound-smoke-auto-ready" \
  "${script_dir}/run-clean-main-human-playtest-stack.sh" >"${auto_output}" 2>&1; then
  fail "clean-main human playtest accepted automated smoke args"
fi

if ! rg -q "automated smoke" "${auto_output}"; then
  echo "Expected automated-smoke rejection output:" >&2
  cat "${auto_output}" >&2
  fail "clean-main human playtest did not explain the automated smoke rejection"
fi

expect_final_gate_rejection() {
  local env_name="$1"
  local output_path="${tmp_dir}/${env_name}-output.log"
  local worktree_path="${tmp_dir}/${env_name}-worktree"

  if env \
    PATH="${fake_bin}:${PATH}" \
    RIFTBOUND_FAKE_GIT_LOG="${fake_git_log}" \
    RIFTBOUND_CLEAN_WORKTREE_DIR="${worktree_path}" \
    RIFTBOUND_KEEP_CLEAN_WORKTREE=1 \
    "${env_name}=0" \
    "${script_dir}/run-clean-main-human-playtest-stack.sh" >"${output_path}" 2>&1; then
    fail "clean-main human playtest accepted ${env_name}=0"
  fi

  if ! rg -q "final P5 evidence|${env_name}" "${output_path}"; then
    echo "Expected final evidence gate rejection output for ${env_name}=0:" >&2
    cat "${output_path}" >&2
    fail "clean-main human playtest did not explain the ${env_name}=0 rejection"
  fi
}

expect_final_gate_value_rejection() {
  local env_name="$1"
  local env_value="$2"
  local value_key="${env_value//[^A-Za-z0-9_.-]/_}"
  local output_path="${tmp_dir}/${env_name}-${value_key}-output.log"
  local worktree_path="${tmp_dir}/${env_name}-${value_key}-worktree"

  if env \
    PATH="${fake_bin}:${PATH}" \
    RIFTBOUND_FAKE_GIT_LOG="${fake_git_log}" \
    RIFTBOUND_CLEAN_WORKTREE_DIR="${worktree_path}" \
    RIFTBOUND_KEEP_CLEAN_WORKTREE=1 \
    "${env_name}=${env_value}" \
    "${script_dir}/run-clean-main-human-playtest-stack.sh" >"${output_path}" 2>&1; then
    fail "clean-main human playtest accepted ${env_name}=${env_value}"
  fi

  if ! rg -q "final P5 evidence|${env_name}" "${output_path}"; then
    echo "Expected final evidence gate rejection output for ${env_name}=${env_value}:" >&2
    cat "${output_path}" >&2
    fail "clean-main human playtest did not explain the ${env_name}=${env_value} rejection"
  fi
}

expect_final_gate_rejection "RIFTBOUND_CONFIRM_MANUAL"
expect_final_gate_rejection "RIFTBOUND_REQUIRE_CLEAN_GIT"
expect_final_gate_rejection "RIFTBOUND_CHECK_EVIDENCE"
expect_final_gate_rejection "RIFTBOUND_PACKAGE_EVIDENCE"
expect_final_gate_rejection "RIFTBOUND_VERIFY_EVIDENCE_PACKAGE"
expect_final_gate_rejection "RIFTBOUND_BUILD_GODOT"
expect_final_gate_rejection "RIFTBOUND_WAIT"
expect_final_gate_rejection "RIFTBOUND_CLEAN_WORKTREE_FETCH"
expect_final_gate_value_rejection "RIFTBOUND_CLEAN_WORKTREE_REF" "HEAD"
expect_final_gate_value_rejection "RIFTBOUND_QUIT_AFTER" "10"

stale_screenshot_dir="${tmp_dir}/stale-screenshots"
mkdir -p "${stale_screenshot_dir}"
printf 'old evidence\n' >"${stale_screenshot_dir}/player-a.log"
expect_final_gate_value_rejection "RIFTBOUND_SCREENSHOT_DIR" "${stale_screenshot_dir}"

existing_evidence_package="${tmp_dir}/existing-human-playtest.tar.gz"
printf 'old package\n' >"${existing_evidence_package}"
expect_final_gate_value_rejection "RIFTBOUND_EVIDENCE_PACKAGE" "${existing_evidence_package}"

external_playtest_report="${tmp_dir}/external-playtest-report.md"
expect_final_gate_value_rejection "RIFTBOUND_PLAYTEST_REPORT" "${external_playtest_report}"
expect_final_gate_value_rejection "RIFTBOUND_EXTRA_GODOT_ARGS" "--windowed"

duplicate_handle_output="${tmp_dir}/duplicate-handle-output.log"
duplicate_handle_worktree="${tmp_dir}/duplicate-handle-worktree"
if env \
  PATH="${fake_bin}:${PATH}" \
  RIFTBOUND_FAKE_GIT_LOG="${fake_git_log}" \
  RIFTBOUND_CLEAN_WORKTREE_DIR="${duplicate_handle_worktree}" \
  RIFTBOUND_KEEP_CLEAN_WORKTREE=1 \
  RIFTBOUND_HANDLE_A="same-human-handle" \
  RIFTBOUND_HANDLE_B="same-human-handle" \
  "${script_dir}/run-clean-main-human-playtest-stack.sh" >"${duplicate_handle_output}" 2>&1; then
  fail "clean-main human playtest accepted duplicate player handles"
fi

if ! rg -q "final P5 evidence|RIFTBOUND_HANDLE_A|RIFTBOUND_HANDLE_B|duplicate.*handle|distinct.*handle" "${duplicate_handle_output}"; then
  echo "Expected duplicate handle rejection output:" >&2
  cat "${duplicate_handle_output}" >&2
  fail "clean-main human playtest did not explain the duplicate handle rejection"
fi

duplicate_key_output="${tmp_dir}/duplicate-key-output.log"
duplicate_key_worktree="${tmp_dir}/duplicate-key-worktree"
if env \
  PATH="${fake_bin}:${PATH}" \
  RIFTBOUND_FAKE_GIT_LOG="${fake_git_log}" \
  RIFTBOUND_CLEAN_WORKTREE_DIR="${duplicate_key_worktree}" \
  RIFTBOUND_KEEP_CLEAN_WORKTREE=1 \
  RIFTBOUND_PLAYER_KEY_A="pk_same_human_key" \
  RIFTBOUND_PLAYER_KEY_B="pk_same_human_key" \
  "${script_dir}/run-clean-main-human-playtest-stack.sh" >"${duplicate_key_output}" 2>&1; then
  fail "clean-main human playtest accepted duplicate player keys"
fi

if ! rg -q "final P5 evidence|RIFTBOUND_PLAYER_KEY_A|RIFTBOUND_PLAYER_KEY_B|duplicate.*key|distinct.*key" "${duplicate_key_output}"; then
  echo "Expected duplicate player key rejection output:" >&2
  cat "${duplicate_key_output}" >&2
  fail "clean-main human playtest did not explain the duplicate player key rejection"
fi

incomplete_output="${tmp_dir}/incomplete-output.log"
incomplete_worktree="${tmp_dir}/incomplete-worktree"
PATH="${fake_bin}:${PATH}" \
  RIFTBOUND_FAKE_GIT_LOG="${fake_git_log}" \
  RIFTBOUND_CLEAN_WORKTREE_DIR="${incomplete_worktree}" \
  RIFTBOUND_KEEP_CLEAN_WORKTREE=1 \
  RIFTBOUND_CONFIRM_MANUAL=0 \
  RIFTBOUND_ALLOW_INCOMPLETE_HUMAN_EVIDENCE=1 \
  "${script_dir}/run-clean-main-human-playtest-stack.sh" >"${incomplete_output}" 2>&1

rg -q "incomplete evidence marker: 1" "${incomplete_output}" \
  || fail "clean-main human playtest did not propagate the incomplete evidence marker"

safe_output="${tmp_dir}/safe-output.log"
safe_worktree="${tmp_dir}/safe-worktree"
safe_evidence_dir="${tmp_dir}/safe-evidence"
safe_evidence_package="${tmp_dir}/safe-evidence.tar.gz"
PATH="${fake_bin}:${PATH}" \
  RIFTBOUND_FAKE_GIT_LOG="${fake_git_log}" \
  RIFTBOUND_CLEAN_WORKTREE_DIR="${safe_worktree}" \
  RIFTBOUND_KEEP_CLEAN_WORKTREE=1 \
  RIFTBOUND_SCREENSHOT_DIR="${safe_evidence_dir}" \
  RIFTBOUND_EVIDENCE_PACKAGE="${safe_evidence_package}" \
  "${script_dir}/run-clean-main-human-playtest-stack.sh" >"${safe_output}" 2>&1

rg -q "wrapped human stack ran" "${safe_output}" \
  || fail "clean-main human playtest did not continue with final evidence gates enabled"

rg -q "Final P5 operator checklist" "${safe_output}" \
  || fail "clean-main human playtest did not print the final P5 operator checklist"

rg -q "two human players|Two human players" "${safe_output}" \
  || fail "clean-main human playtest checklist did not mention two human players"

rg -q "hidden cards|card backs|backs/counts" "${safe_output}" \
  || fail "clean-main human playtest checklist did not mention hidden-information verification"

safe_operator_guide="${safe_evidence_dir}/OPERATOR_GUIDE.md"
[[ -s "${safe_operator_guide}" ]] \
  || fail "clean-main human playtest did not write OPERATOR_GUIDE.md into the evidence directory"

rg -q "Riftbound Godot P5 Operator Guide|Final P5 operator checklist" "${safe_operator_guide}" \
  || fail "OPERATOR_GUIDE.md does not identify the final P5 operator checklist"

rg -q "Room:|Player A handle:|Player B handle:" "${safe_operator_guide}" \
  || fail "OPERATOR_GUIDE.md does not record room and player handles"

rg -q "Player A key fingerprint: .*\\.\\.\\..*|Player B key fingerprint: .*\\.\\.\\..*" "${safe_operator_guide}" \
  || fail "OPERATOR_GUIDE.md does not record redacted player key fingerprints"

if rg -q "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa|bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" "${safe_operator_guide}"; then
  fail "OPERATOR_GUIDE.md leaked a full player key instead of a fingerprint"
fi

rg -q "preconstructed decks|server result panel|hidden cards.*card backs/counts|Evidence package:" "${safe_operator_guide}" \
  || fail "OPERATOR_GUIDE.md does not record the required P5 operator instructions"

precheck_output="${tmp_dir}/precheck-output.log"
precheck_worktree="${tmp_dir}/precheck-worktree"
: >"${fake_git_log}"
PATH="${fake_bin}:${PATH}" \
  RIFTBOUND_FAKE_GIT_LOG="${fake_git_log}" \
  RIFTBOUND_CLEAN_WORKTREE_DIR="${precheck_worktree}" \
  RIFTBOUND_KEEP_CLEAN_WORKTREE=1 \
  RIFTBOUND_GODOT_BIN="${fake_godot_bin}" \
  RIFTBOUND_DOTNET_BIN="${fake_dotnet_bin}" \
  "${script_dir}/run-clean-main-human-playtest-stack.sh" --precheck >"${precheck_output}" 2>&1

rg -q "Final P5 precheck passed" "${precheck_output}" \
  || fail "clean-main human playtest precheck did not report success"

if rg -q "wrapped human stack ran" "${precheck_output}"; then
  fail "clean-main human playtest precheck launched the wrapped human stack"
fi

if rg -q "worktree add" "${fake_git_log}"; then
  fail "clean-main human playtest precheck created a clean worktree"
fi

rg -q "fetch origin main" "${fake_git_log}" \
  || fail "clean-main human playtest precheck did not fetch origin/main"

missing_godot_output="${tmp_dir}/missing-godot-precheck-output.log"
if PATH="${fake_bin}:${PATH}" \
  RIFTBOUND_FAKE_GIT_LOG="${fake_git_log}" \
  RIFTBOUND_CLEAN_WORKTREE_DIR="${tmp_dir}/missing-godot-worktree" \
  RIFTBOUND_KEEP_CLEAN_WORKTREE=1 \
  RIFTBOUND_GODOT_BIN="${tmp_dir}/missing-godot" \
  RIFTBOUND_DOTNET_BIN="${fake_dotnet_bin}" \
  "${script_dir}/run-clean-main-human-playtest-stack.sh" --precheck >"${missing_godot_output}" 2>&1; then
  fail "clean-main human playtest precheck accepted a missing Godot executable"
fi

rg -q "Godot executable" "${missing_godot_output}" \
  || fail "clean-main human playtest precheck did not explain the missing Godot executable"

missing_dotnet_output="${tmp_dir}/missing-dotnet-precheck-output.log"
if PATH="${fake_bin}:${PATH}" \
  RIFTBOUND_FAKE_GIT_LOG="${fake_git_log}" \
  RIFTBOUND_CLEAN_WORKTREE_DIR="${tmp_dir}/missing-dotnet-worktree" \
  RIFTBOUND_KEEP_CLEAN_WORKTREE=1 \
  RIFTBOUND_GODOT_BIN="${fake_godot_bin}" \
  RIFTBOUND_DOTNET_BIN="${tmp_dir}/missing-dotnet" \
  "${script_dir}/run-clean-main-human-playtest-stack.sh" --precheck >"${missing_dotnet_output}" 2>&1; then
  fail "clean-main human playtest precheck accepted a missing .NET executable for local API auto-start"
fi

rg -q "\\.NET executable|dotnet" "${missing_dotnet_output}" \
  || fail "clean-main human playtest precheck did not explain the missing .NET executable"

blocked_parent_file="${tmp_dir}/blocked-parent"
printf 'not a directory\n' >"${blocked_parent_file}"

blocked_screenshot_parent_output="${tmp_dir}/blocked-screenshot-parent-precheck-output.log"
if PATH="${fake_bin}:${PATH}" \
  RIFTBOUND_FAKE_GIT_LOG="${fake_git_log}" \
  RIFTBOUND_CLEAN_WORKTREE_DIR="${tmp_dir}/blocked-screenshot-parent-worktree" \
  RIFTBOUND_KEEP_CLEAN_WORKTREE=1 \
  RIFTBOUND_GODOT_BIN="${fake_godot_bin}" \
  RIFTBOUND_DOTNET_BIN="${fake_dotnet_bin}" \
  RIFTBOUND_SCREENSHOT_DIR="${blocked_parent_file}/screens" \
  "${script_dir}/run-clean-main-human-playtest-stack.sh" --precheck >"${blocked_screenshot_parent_output}" 2>&1; then
  fail "clean-main human playtest precheck accepted a screenshot directory under a file parent"
fi

rg -q "RIFTBOUND_SCREENSHOT_DIR|evidence dir|parent" "${blocked_screenshot_parent_output}" \
  || fail "clean-main human playtest precheck did not explain the blocked screenshot parent"

blocked_package_parent_output="${tmp_dir}/blocked-package-parent-precheck-output.log"
if PATH="${fake_bin}:${PATH}" \
  RIFTBOUND_FAKE_GIT_LOG="${fake_git_log}" \
  RIFTBOUND_CLEAN_WORKTREE_DIR="${tmp_dir}/blocked-package-parent-worktree" \
  RIFTBOUND_KEEP_CLEAN_WORKTREE=1 \
  RIFTBOUND_GODOT_BIN="${fake_godot_bin}" \
  RIFTBOUND_DOTNET_BIN="${fake_dotnet_bin}" \
  RIFTBOUND_EVIDENCE_PACKAGE="${blocked_parent_file}/human-playtest.tar.gz" \
  "${script_dir}/run-clean-main-human-playtest-stack.sh" --precheck >"${blocked_package_parent_output}" 2>&1; then
  fail "clean-main human playtest precheck accepted an evidence package under a file parent"
fi

rg -q "RIFTBOUND_EVIDENCE_PACKAGE|evidence package|parent" "${blocked_package_parent_output}" \
  || fail "clean-main human playtest precheck did not explain the blocked evidence package parent"

blocked_worktree_parent_output="${tmp_dir}/blocked-worktree-parent-precheck-output.log"
if PATH="${fake_bin}:${PATH}" \
  RIFTBOUND_FAKE_GIT_LOG="${fake_git_log}" \
  RIFTBOUND_CLEAN_WORKTREE_DIR="${blocked_parent_file}/clean-worktree" \
  RIFTBOUND_KEEP_CLEAN_WORKTREE=1 \
  RIFTBOUND_GODOT_BIN="${fake_godot_bin}" \
  RIFTBOUND_DOTNET_BIN="${fake_dotnet_bin}" \
  "${script_dir}/run-clean-main-human-playtest-stack.sh" --precheck >"${blocked_worktree_parent_output}" 2>&1; then
  fail "clean-main human playtest precheck accepted a clean worktree under a file parent"
fi

rg -q "RIFTBOUND_CLEAN_WORKTREE_DIR|clean worktree|parent" "${blocked_worktree_parent_output}" \
  || fail "clean-main human playtest precheck did not explain the blocked clean worktree parent"

nonempty_worktree_dir="${tmp_dir}/nonempty-clean-worktree"
mkdir -p "${nonempty_worktree_dir}"
printf 'old worktree content\n' >"${nonempty_worktree_dir}/stale.txt"

nonempty_worktree_output="${tmp_dir}/nonempty-worktree-precheck-output.log"
if PATH="${fake_bin}:${PATH}" \
  RIFTBOUND_FAKE_GIT_LOG="${fake_git_log}" \
  RIFTBOUND_CLEAN_WORKTREE_DIR="${nonempty_worktree_dir}" \
  RIFTBOUND_KEEP_CLEAN_WORKTREE=1 \
  RIFTBOUND_GODOT_BIN="${fake_godot_bin}" \
  RIFTBOUND_DOTNET_BIN="${fake_dotnet_bin}" \
  "${script_dir}/run-clean-main-human-playtest-stack.sh" --precheck >"${nonempty_worktree_output}" 2>&1; then
  fail "clean-main human playtest precheck accepted a non-empty clean worktree directory"
fi

rg -q "RIFTBOUND_CLEAN_WORKTREE_DIR|clean worktree|not empty" "${nonempty_worktree_output}" \
  || fail "clean-main human playtest precheck did not explain the non-empty clean worktree directory"

existing_local_api_output="${tmp_dir}/existing-local-api-precheck-output.log"
if PATH="${fake_bin}:${PATH}" \
  RIFTBOUND_FAKE_GIT_LOG="${fake_git_log}" \
  RIFTBOUND_FAKE_CURL_HEALTHY=1 \
  RIFTBOUND_CLEAN_WORKTREE_DIR="${tmp_dir}/existing-local-api-worktree" \
  RIFTBOUND_KEEP_CLEAN_WORKTREE=1 \
  RIFTBOUND_GODOT_BIN="${fake_godot_bin}" \
  RIFTBOUND_DOTNET_BIN="${fake_dotnet_bin}" \
  "${script_dir}/run-clean-main-human-playtest-stack.sh" --precheck >"${existing_local_api_output}" 2>&1; then
  fail "clean-main human playtest precheck accepted an existing default local API"
fi

rg -q "existing.*local.*API|RIFTBOUND_SERVER|127\\.0\\.0\\.1:5088|5088" "${existing_local_api_output}" \
  || fail "clean-main human playtest precheck did not explain the existing local API rejection"

echo "Human playtest script safety checks passed."
