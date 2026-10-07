# Deploying a second application on the HMS LAN test server

**Scope:** host a new, separate application (same stack: .NET backend + Vite/nginx frontend + PostgreSQL, behind Nginx Proxy Manager) on the server that already runs the HMS `dev` / `uat` / `test` environments.
**Placeholder name used below:** `newapp` (replace it everywhere).
**Estimated effort:** about half a day, plus about 1 hour and a short HMS downtime if you also do the optional Step 0.

---

## 1. Summary

The current setup is already built so that several stacks can share one server. Adding a second application means adding another _tenant_ to the existing shared infrastructure. **No changes to the HMS stacks are required.**

What the new app needs:

- its own git checkout and Docker Compose project
- its own database(s) and Postgres role in the existing Postgres container
- its own hostnames, routed by the existing Nginx Proxy Manager (NPM)
- its own cron sync job, lock file and backup entries

---

## 2. Current server layout

```
                     clients (hosts file → <SERVER_IP>)
                                  │  :80 / :443
                    ┌─────────────▼─────────────┐
                    │  hts-npm  (NPM, :81 admin) │   compose project: lan
                    └─────────────┬─────────────┘   (/opt/apps/hts/deploy/lan)
                       lan_edge_net (bridge)
      ┌──────────────┬────────────┴───┬──────────────┬─────────────┐
 hts-frontend-dev  hts-backend-dev  hts-…-uat      hts-…-test     (newapp-* here)
                         │                │              │
                       lan_data_net (bridge)
                         └────────────────┼──────────────┘
                                ┌─────────▼─────────┐
                                │ hts-postgres (16) │   compose project: lan
                                └───────────────────┘
```

| Piece                                                               | Where it is defined                                                                | Notes                                                                                                             |
| ------------------------------------------------------------------- | ---------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `hts-postgres`, `hts-npm`, networks `lan_edge_net` / `lan_data_net` | `/opt/apps/hts/deploy/lan/docker-compose.yml`                                      | Shared infra. NPM is the **only** container that publishes host ports (80, 81, 443).                              |
| HMS environments                                                    | `/opt/apps/hts/deploy/lan-multi/docker-compose.yml`, run as project `hts-{env}`    | Containers `hts-backend-{env}` and `hts-frontend-{env}`, attached to both external networks with network aliases. |
| Source checkouts                                                    | `/opt/apps/hts` (dev), `/opt/apps/hts-uat`, `/opt/apps/hts-test`                   | Git worktrees sharing one object store.                                                                           |
| Routing                                                             | NPM proxy hosts: `{env}.hms.lan` → frontend:80, `api.{env}.hms.lan` → backend:8080 | Clients resolve names via their `hosts` file.                                                                     |
| Auto-deploy                                                         | cron `*/5` → `deploy-env.sh` via `sync-envs.sh`                                    | Lock file `/tmp/hts-sync.lock`. Rebuilds only when the branch has new commits.                                    |
| Databases                                                           | `hts_dotnet_dev`, `hts_dotnet_uat`, `hts_dotnet_test`                              | All HMS environments connect as the `postgres` superuser.                                                         |

---

## 3. Risks and constraints

| #   | Risk                                                                                                                                                                                                             | Mitigation                                                                                                               |
| --- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| 1   | **Name collisions on the shared network.** All containers on `lan_edge_net` share one DNS namespace.                                                                                                             | Prefix every container name and network alias with `newapp-`. Never use generic aliases such as `backend` or `frontend`. |
| 2   | **Port collisions.**                                                                                                                                                                                             | The new app must **not** publish host ports. Everything goes through NPM.                                                |
| 3   | **Shared infra is owned by the HMS repo.** Running `docker compose down` in `/opt/apps/hts/deploy/lan` stops Postgres and NPM for _both_ apps.                                                                   | Either document this clearly or move the shared infra to its own project (Step 0).                                       |
| 4   | **DB isolation.** HMS uses the `postgres` superuser.                                                                                                                                                             | Give `newapp` its own role that owns only its own databases.                                                             |
| 5   | **Postgres version is fixed at 16.**                                                                                                                                                                             | If `newapp` needs another major version, run a separate Postgres container on `lan_data_net` instead.                    |
| 6   | **Build contention.** .NET SDK and Vite builds are CPU/RAM-heavy. Two cron jobs building at the same time can starve the server.                                                                                 | Use a separate lock file and stagger the schedules, or share one `flock` lock between both sync scripts.                 |
| 7   | **Capacity.** The server already runs 6 app containers plus periodic builds.                                                                                                                                     | Check before you start (see Step 1).                                                                                     |
| 8   | **Backups.** New databases and upload folders are not covered automatically. Note that the backup section of `docs/13_MANUAL_OPERACION.md` still references `supabase-db`, which looks outdated for this server. | Add `newapp` to the backup job, and verify that HMS backups actually run.                                                |

---

## 4. Step-by-step plan

### Step 0 (optional, recommended): separate the shared infra from HMS

Move the `postgres` and `npm` services, their volumes and the two networks out of the HMS repo into a neutral project, for example `/opt/infra/docker-compose.yml`. Keep the same container names (`hts-postgres`, `hts-npm`), volume names and network names (`lan_edge_net`, `lan_data_net`) so that neither app's compose file has to change.

- Declare the existing volumes as `external: true` with their current names, which are prefixed with the project name `lan_`. Check the exact names with `docker volume ls`. This preserves the data.
- Requires a short maintenance window: stop the services in the `lan` project, then start them in the `infra` project.
- If you skip this step, the risk in row 3 of the table above remains.

### Step 1: check capacity

```bash
free -h
df -h /opt /var/lib/docker
docker system df
docker stats --no-stream
```

Make sure there is enough free RAM to run a .NET build (~2–3 GB peak) on top of the running containers, and several GB of free disk for images.

### Step 2: choose hostnames

Use a dedicated domain rather than `*.hms.lan`:

| Environment | Frontend          | API                   |
| ----------- | ----------------- | --------------------- |
| dev         | `dev.newapp.lan`  | `api.dev.newapp.lan`  |
| uat         | `uat.newapp.lan`  | `api.uat.newapp.lan`  |
| test        | `test.newapp.lan` | `api.test.newapp.lan` |

If only one environment is needed, use just `newapp.lan` and `api.newapp.lan`.

### Step 3: create the database role and databases

```bash
docker exec -i hts-postgres psql -U postgres <<'SQL'
CREATE ROLE newapp LOGIN PASSWORD '<strong-password>';
CREATE DATABASE newapp_dev  OWNER newapp;
CREATE DATABASE newapp_uat  OWNER newapp;
CREATE DATABASE newapp_test OWNER newapp;
REVOKE CONNECT ON DATABASE newapp_dev, newapp_uat, newapp_test FROM PUBLIC;
SQL
```

### Step 4: get the code onto the server

```bash
git clone <newapp-repo-url> /opt/apps/newapp
```

If the app has several environments, follow the HMS pattern. `/opt/apps/newapp` tracks the dev branch, and the deploy script creates worktrees at `/opt/apps/newapp-uat` and `/opt/apps/newapp-test` on first run.

### Step 5: add deployment files to the new repo

Copy `deploy/lan-multi/` from the HMS repo into the new repo and adapt it:

**`docker-compose.yml`**

```yaml
services:
  backend:
    build:
      context: ${WORKTREE_PATH:?WORKTREE_PATH is required}
      dockerfile: backend/Dockerfile
    image: newapp-backend-${ENV:?ENV is required}
    container_name: newapp-backend-${ENV}
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: Host=hts-postgres;Port=5432;Database=${DB_NAME};Username=newapp;Password=${DB_PASSWORD}
      Jwt__Secret: ${JWT_SECRET}
    volumes:
      - /opt/apps/newapp/uploads-${ENV}:/app/App_Data/uploads
    networks:
      edge_net:
        aliases: [newapp-backend-${ENV}]
      data_net:

  frontend:
    build:
      context: ${WORKTREE_PATH}/frontend
      args:
        VITE_API_URL: ${VITE_API_URL}
    image: newapp-frontend-${ENV}
    container_name: newapp-frontend-${ENV}
    restart: unless-stopped
    depends_on: [backend]
    networks:
      edge_net:
        aliases: [newapp-frontend-${ENV}]

  # migrator: copy from the HMS file, with the same connection-string change

networks:
  edge_net:
    external: true
    name: lan_edge_net
  data_net:
    external: true
    name: lan_data_net
```

No `ports:` section anywhere in this file.

**`envs/dev.env`** (one file per environment)

```
ENV=dev
BRANCH=main
DB_NAME=newapp_dev
VITE_API_URL=http://api.dev.newapp.lan/api
WORKTREE_PATH=/opt/apps/newapp
```

**`.env`** (secrets, not committed): `DB_PASSWORD=` and `JWT_SECRET=`. Use new values. **Do not reuse the HMS secrets.**

**`deploy-env.sh`**

- Compose project name: `-p hts-${ENV}` → `-p newapp-${ENV}`
- Final echo: `${ENV}.hms.lan` → `${ENV}.newapp.lan`

**`sync-envs.sh`**

- `LOCK_FILE="/tmp/hts-sync.lock"` → `/tmp/newapp-sync.lock`
- Alternatively, wrap both apps' sync scripts in `flock /tmp/lan-build.lock …` so that only one build runs on the server at a time.

### Step 6: first deploy

```bash
cd /opt/apps/newapp/deploy/lan-multi
cp .env.example .env   # fill in secrets
chmod +x deploy-env.sh deploy-all.sh sync-envs.sh
./deploy-all.sh --migrate

# Verify the containers joined the shared network next to the HMS ones
docker network inspect lan_edge_net --format '{{range .Containers}}{{.Name}} {{end}}'
```

### Step 7: configure NPM proxy hosts

In the NPM admin UI (`http://<SERVER_IP>:81`), add:

| Domain                | Forward host           | Port |
| --------------------- | ---------------------- | ---- |
| `dev.newapp.lan`      | `newapp-frontend-dev`  | 80   |
| `api.dev.newapp.lan`  | `newapp-backend-dev`   | 8080 |
| `uat.newapp.lan`      | `newapp-frontend-uat`  | 80   |
| `api.uat.newapp.lan`  | `newapp-backend-uat`   | 8080 |
| `test.newapp.lan`     | `newapp-frontend-test` | 80   |
| `api.test.newapp.lan` | `newapp-backend-test`  | 8080 |

No trailing spaces in the _Forward host_ field (NPM is sensitive to whitespace).

### Step 8: client name resolution

Add the entries to each client's `hosts` file (`C:\Windows\System32\drivers\etc\hosts`):

```
<SERVER_IP>  dev.newapp.lan api.dev.newapp.lan
<SERVER_IP>  uat.newapp.lan api.uat.newapp.lan
<SERVER_IP>  test.newapp.lan api.test.newapp.lan
```

If the LAN has a DNS server (router, Pi-hole, dnsmasq), one wildcard record per domain (`*.newapp.lan → <SERVER_IP>`) is better than editing every client.

### Step 9: auto-deploy cron, offset from HMS

```bash
crontab -e
```

```
2-59/5 * * * * /opt/apps/newapp/deploy/lan-multi/sync-envs.sh >> /var/log/newapp-sync.log 2>&1
```

HMS runs at minutes 0, 5, 10…; this job runs at minutes 2, 7, 12…

### Step 10: backups

Add the new databases and upload folders to the daily backup job:

```bash
for db in newapp_dev newapp_uat newapp_test; do
  docker exec hts-postgres pg_dump -U postgres -F c "$db" > /opt/backups/newapp/${db}_$(date +%F).dump
done
tar -czf /opt/backups/newapp/uploads_$(date +%F).tar.gz /opt/apps/newapp/uploads-*
```

While you are there, confirm that the HMS databases (`hts_dotnet_*`) are also being backed up. The documented job targets `supabase-db`.

### Step 11: smoke test

- [ ] Every `*.newapp.lan` hostname loads, and the API answers.
- [ ] All `*.hms.lan` hostnames still work (rules out alias or name collisions).
- [ ] `docker stats` during a `newapp` build shows acceptable CPU/RAM headroom.
- [ ] The first cron run appears in `/var/log/newapp-sync.log` and reports "up to date".

---

## 5. Rollback

The new app is fully additive. To remove it:

```bash
cd /opt/apps/newapp/deploy/lan-multi
for e in dev uat test; do docker compose -p newapp-$e --env-file .env --env-file envs/$e.env down; done
docker exec -i hts-postgres psql -U postgres \
  -c "DROP DATABASE newapp_dev" -c "DROP DATABASE newapp_uat" \
  -c "DROP DATABASE newapp_test" -c "DROP ROLE newapp"
```

Then delete the NPM proxy hosts and the cron line. HMS is not affected.
