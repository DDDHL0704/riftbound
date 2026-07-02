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
    cat >"${destination}/clients/godot/tools/run-local-simulated-playtest-stack.sh" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail
echo "wrapped simulated stack ran"
echo "room=${RIFTBOUND_ROOM:-}"
echo "evidence=${RIFTBOUND_SCREENSHOT_DIR:-}"
STUB
    chmod +x "${destination}/clients/godot/tools/run-local-simulated-playtest-stack.sh"
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

tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/riftbound-clean-sim-script.XXXXXX")"
trap 'rm -rf "${tmp_dir}"' EXIT

fake_bin="${tmp_dir}/bin"
fake_git_log="${tmp_dir}/git.log"
make_fake_git "${fake_bin}" "${fake_git_log}"

[[ -x "${script_dir}/run-clean-main-simulated-playtest-stack.sh" ]] \
  || fail "clean-main simulated wrapper is missing"

output="${tmp_dir}/output.log"
worktree="${tmp_dir}/clean-worktree"
PATH="${fake_bin}:${PATH}" \
  RIFTBOUND_FAKE_GIT_LOG="${fake_git_log}" \
  RIFTBOUND_CLEAN_WORKTREE_FETCH=0 \
  RIFTBOUND_CLEAN_WORKTREE_DIR="${worktree}" \
  RIFTBOUND_KEEP_CLEAN_WORKTREE=1 \
  RIFTBOUND_ROOM=clean-sim-test \
  RIFTBOUND_SCREENSHOT_DIR="${tmp_dir}/evidence" \
  "${script_dir}/run-clean-main-simulated-playtest-stack.sh" >"${output}" 2>&1

rg -q "wrapped simulated stack ran" "${output}" \
  || fail "clean-main simulated wrapper did not run the simulated stack from the clean worktree"
rg -q "not valid final two-human P5 evidence" "${output}" \
  || fail "clean-main simulated wrapper did not warn that output is not final P5 evidence"
rg -q "room=clean-sim-test" "${output}" \
  || fail "clean-main simulated wrapper did not preserve RIFTBOUND_ROOM"
rg -q "worktree add --detach" "${fake_git_log}" \
  || fail "clean-main simulated wrapper did not create a detached clean worktree"

echo "Clean-main simulated playtest script checks passed."
