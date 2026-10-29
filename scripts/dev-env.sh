#!/usr/bin/env bash
set -euo pipefail

export DOTNET_ROOT="${DOTNET_ROOT:-${HOME}/.dotnet}"
export PATH="/opt/homebrew/opt/node@24/bin:/opt/homebrew/opt/postgresql@16/bin:${PATH}"
# Keep an existing dotnet wrapper ahead of a directly installed SDK.
if ! command -v dotnet >/dev/null 2>&1; then
    export PATH="${PATH}:${DOTNET_ROOT}"
fi
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export ConnectionStrings__Riftbound="${ConnectionStrings__Riftbound:-Host=localhost;Port=5432;Database=riftbound_dev}"

echo "DOTNET_ROOT=${DOTNET_ROOT}"
dotnet --version
node --version
psql --version
redis-cli ping
