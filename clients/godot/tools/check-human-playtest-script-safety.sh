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

tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/riftbound-human-script-safety.XXXXXX")"
trap 'rm -rf "${tmp_dir}"' EXIT

fake_bin="${tmp_dir}/bin"
fake_git_log="${tmp_dir}/git.log"
make_fake_git "${fake_bin}" "${fake_git_log}"

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

safe_output="${tmp_dir}/safe-output.log"
safe_worktree="${tmp_dir}/safe-worktree"
PATH="${fake_bin}:${PATH}" \
  RIFTBOUND_FAKE_GIT_LOG="${fake_git_log}" \
  RIFTBOUND_CLEAN_WORKTREE_FETCH=0 \
  RIFTBOUND_CLEAN_WORKTREE_DIR="${safe_worktree}" \
  RIFTBOUND_KEEP_CLEAN_WORKTREE=1 \
  RIFTBOUND_EXTRA_GODOT_ARGS="--windowed" \
  "${script_dir}/run-clean-main-human-playtest-stack.sh" >"${safe_output}" 2>&1

rg -q "wrapped human stack ran" "${safe_output}" \
  || fail "clean-main human playtest did not continue for non-smoke extra args"

echo "Human playtest script safety checks passed."
