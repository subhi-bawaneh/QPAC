#!/usr/bin/env bash
# Runs the API against the local SQLite database — never Neon.
#
# Everything it needs is created on first run under src/Dip.Api/App_Data (git-ignored):
# the database file, a JWT key and a SuperAdmin password. The Qpac_1/ folder at the top
# of the repository stands in for Google Drive. See docs/local-dev.md.
set -euo pipefail

cd "$(dirname "$0")/.."

export ASPNETCORE_ENVIRONMENT=Development
export Database__Provider=Sqlite
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://localhost:5001}"

exec dotnet run --project src/Dip.Api "$@"
