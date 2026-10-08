#!/bin/sh
# Rebuilds and restarts Kepler Talento on the LAN test server when the tracked branch has new
# commits. Cron runs it every five minutes; `--force` redeploys without new commits. Migrations
# need no flag: the migrator runs on every deploy and applies only what is pending.
# See docs/deploy-lan-server.md.
set -eu

# The checkout this script lives in, so it deploys the right app from whatever directory it is
# started (cron, or `./deploy.sh` in deploy/lan).
REPO_DIR=${KTL_REPO_DIR:-$(cd "$(dirname "$0")/../.." && pwd)}
BRANCH=${KTL_BRANCH:-main}
ENV_FILE=${KTL_ENV_FILE:-/opt/apps/ktl-secrets/ktl.env}
LOCK_FILE=${KTL_LOCK_FILE:-/tmp/ktl-sync.lock}

log() {
  echo "$(date -Is) $*"
}

# One deploy at a time: a cron tick that lands during a long build skips instead of piling up.
exec 9>"$LOCK_FILE"
if ! flock -n 9; then
  log "another deploy is still running; skipping"
  exit 0
fi

if [ ! -r "$ENV_FILE" ]; then
  log "missing $ENV_FILE; see docs/deploy-lan-server.md"
  exit 1
fi

cd "$REPO_DIR"
git fetch --quiet origin "$BRANCH"
current=$(git rev-parse HEAD)
target=$(git rev-parse "origin/$BRANCH")
if [ "$current" = "$target" ] && [ "${1:-}" != "--force" ]; then
  log "up to date at $(git rev-parse --short HEAD)"
  exit 0
fi

git checkout --quiet "$BRANCH"
git merge --ff-only --quiet "origin/$BRANCH"
version=$(git rev-parse --short HEAD)
log "deploying $version"

# The migrator runs as part of `up` and must finish before the API starts.
APP_VERSION="$version" docker compose -p ktl --env-file "$ENV_FILE" \
  -f docker-compose.yml -f deploy/lan/docker-compose.lan.yml \
  up -d --build --remove-orphans

# Each build leaves the previous images dangling; drop them so the disk does not fill up.
docker image prune -f >/dev/null
log "deployed $version"
