#!/usr/bin/env bash
# Runs the API against the local SQLite database — never Neon.
#
# Everything it needs is created on first run under src/Dip.Api/App_Data (git-ignored):
set -euo pipefail

cd "$(dirname "$0")/.."

export ASPNETCORE_ENVIRONMENT=Development
export Database__Provider=Sqlite
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://localhost:5001}"

exec dotnet run --project src/Dip.Api "$@"
