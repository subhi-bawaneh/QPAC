#!/usr/bin/env bash
# Deletes the local SQLite database so the next run recreates it from the current model.
#
# The local schema comes from EnsureCreated, not from migrations (the Init migration is
# Npgsql SQL), so after any change to the entities or their configuration the file has
# to go. Drive is re-mirrored and the workbooks re-imported on the next start.
set -euo pipefail

cd "$(dirname "$0")/.."

app_data="src/Dip.Api/App_Data"
rm -fv "$app_data"/dip-local.db "$app_data"/dip-local.db-shm "$app_data"/dip-local.db-wal

echo "Local database removed. It is recreated on the next 'dotnet run'."
echo "Kept: $app_data/dev-secrets.json (JWT key + SuperAdmin password)."
