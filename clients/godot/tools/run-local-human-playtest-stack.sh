#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../../.." && pwd)"

server="${RIFTBOUND_SERVER:-http://127.0.0.1:5088}"
room="${RIFTBOUND_ROOM:-human-local-$(date +%H%M%S)}"
screenshot_dir="${RIFTBOUND_SCREENSHOT_DIR:-/tmp/riftbound-human-playtest-${room}}"
api_log="${RIFTBOUND_API_LOG:-${screenshot_dir}/api.log}"
dotnet_bin="${RIFTBOUND_DOTNET_BIN:-${HOME}/.dotnet/dotnet}"
godot_bin="${RIFTBOUND_GODOT_BIN:-/Applications/Godot_dotnet.app/Contents/MacOS/Godot}"
keep_api="${RIFTBOUND_KEEP_API:-0}"
check_evidence="${RIFTBOUND_CHECK_EVIDENCE:-1}"
package_evidence="${RIFTBOUND_PACKAGE_EVIDENCE:-0}"
evidence_package="${RIFTBOUND_EVIDENCE_PACKAGE:-}"
build_godot="${RIFTBOUND_BUILD_GODOT:-1}"
refuse_existing_local_api="${RIFTBOUND_REFUSE_EXISTING_LOCAL_API:-0}"

started_api=0
api_pid=""

cleanup() {
  if [[ "${started_api}" == "1" && "${keep_api}" != "1" && -n "${api_pid}" ]]; then
    if kill -0 "${api_pid}" >/dev/null 2>&1; then
      kill "${api_pid}" >/dev/null 2>&1 || true
      wait "${api_pid}" >/dev/null 2>&1 || true
    fi
  fi
}
trap cleanup EXIT

health_url="${server%/}/health"

mkdir -p "${screenshot_dir}"

if curl -fsS "${health_url}" >/dev/null 2>&1; then
  if [[ "${refuse_existing_local_api}" == "1" ]]; then
    case "${server}" in
      http://127.0.0.1:5088|http://localhost:5088)
        echo "Refusing to reuse existing local Riftbound API at ${server}." >&2
        echo "Stop the server on port 5088 so the clean worktree can start its own API." >&2
        exit 2
        ;;
    esac
  fi

  echo "Using existing Riftbound API at ${server}."
else
  case "${server}" in
    http://127.0.0.1:5088|http://localhost:5088)
      if [[ ! -x "${dotnet_bin}" ]]; then
        echo ".NET executable not found: ${dotnet_bin}" >&2
        echo "Set RIFTBOUND_DOTNET_BIN to the dotnet executable." >&2
        exit 1
      fi

      echo "Starting local Riftbound API at ${server}."
      ASPNETCORE_ENVIRONMENT=Development \
      ASPNETCORE_URLS=http://127.0.0.1:5088 \
      ConnectionStrings__Riftbound="" \
        "${dotnet_bin}" run --project "${repo_root}/src/Riftbound.Api" \
        >"${api_log}" 2>&1 &
      api_pid="$!"
      started_api=1

      for i in $(seq 1 60); do
        if curl -fsS "${health_url}" >/dev/null 2>&1; then
          echo "Riftbound API is ready after ${i}s. log=${api_log}"
          break
        fi

        if ! kill -0 "${api_pid}" >/dev/null 2>&1; then
          echo "Riftbound API exited before becoming healthy. log=${api_log}" >&2
          tail -120 "${api_log}" >&2 || true
          exit 1
        fi

        sleep 1
        if [[ "${i}" == "60" ]]; then
          echo "Timed out waiting for Riftbound API health. log=${api_log}" >&2
          tail -120 "${api_log}" >&2 || true
          exit 1
        fi
      done
      ;;
    *)
      echo "Riftbound API is not reachable at ${health_url}." >&2
      echo "Start that server first, or use RIFTBOUND_SERVER=http://127.0.0.1:5088 for local auto-start." >&2
      exit 1
      ;;
  esac
fi

if [[ "${build_godot}" != "0" ]]; then
  if [[ ! -x "${godot_bin}" ]]; then
    echo "Godot executable not found: ${godot_bin}" >&2
    echo "Set RIFTBOUND_GODOT_BIN to the Godot 4 .NET binary." >&2
    exit 1
  fi

  echo "Building Riftbound Godot client."
  "${godot_bin}" --headless --build-solutions --path "${repo_root}/clients/godot" --quit
fi

export RIFTBOUND_SERVER="${server}"
export RIFTBOUND_ROOM="${room}"
export RIFTBOUND_SCREENSHOT_DIR="${screenshot_dir}"

"${script_dir}/run-local-human-playtest.sh"

if [[ "${check_evidence}" != "0" ]]; then
  "${script_dir}/check-human-playtest-evidence.sh" "${screenshot_dir}"
fi

if [[ "${package_evidence}" != "0" ]]; then
  package_args=("${screenshot_dir}")
  if [[ -n "${evidence_package}" ]]; then
    package_args+=("${evidence_package}")
  fi

  if [[ "${RIFTBOUND_CONFIRM_MANUAL:-0}" == "1" && -s "${screenshot_dir}/playtest-report.md" ]]; then
    RIFTBOUND_CONFIRM_MANUAL=0 "${script_dir}/package-human-playtest-evidence.sh" "${package_args[@]}"
  else
    "${script_dir}/package-human-playtest-evidence.sh" "${package_args[@]}"
  fi
fi
