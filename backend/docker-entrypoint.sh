#!/bin/sh
set -eu

storage_root=/var/lib/kepler-talento/documents

# Anything that writes into the persistent volume without going through this entrypoint
# (`docker compose exec`, `docker cp`, `run --entrypoint`, an alpine tools container) writes
# as root, and the API running as app then fails to create candidate folders. Re-own only
# the entries not already owned by app on every start: cheap on a healthy volume, and it
# repairs root-owned writes that appear after the first start. su-exec drops privileges
# before any application code runs.
find "$storage_root" ! -user app -exec chown app:app {} +
rm -f "$storage_root/.app-ownership-v1"

exec su-exec app "$@"
