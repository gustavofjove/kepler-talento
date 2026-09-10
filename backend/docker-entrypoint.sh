#!/bin/sh
set -eu

storage_root=/var/lib/kepler-talento/documents
ownership_marker="$storage_root/.app-ownership-v1"

# Existing installations may contain directories created by an older root-running
# container. su-exec is used solely to repair the persistent volume once and then
# drop privileges before any application code runs.
if [ ! -f "$ownership_marker" ]; then
  chown -R app:app "$storage_root"
  touch "$ownership_marker"
  chown app:app "$ownership_marker"
fi

exec su-exec app "$@"
