# Riftbound Deployment

This guide describes a reproducible production-style deployment package. It does not require Codex or this repository to create cloud resources, domains, certificates, databases, or paid services.

## Local Docker Engine

Make sure a Docker daemon is running before building. Docker Desktop works, and on macOS a local Colima daemon also works:

```bash
colima start
docker context use colima
```

If `docker build` reports that it cannot connect to `/var/run/docker.sock`, start one of those local Docker engines and retry.

## Build The Image

Build from the repository root:

```bash
docker build -t riftbound-api:local .
```

The image builds the React Dev UI with `npm run build`, publishes `Riftbound.Api`, and serves the static frontend from the API container.

## Run Locally In Memory Mode

Use this mode for smoke tests without Postgres or Redis:

```bash
docker run --rm -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://+:8080 \
  -e ConnectionStrings__Riftbound= \
  -e Riftbound__DevUiOrigins__0=http://127.0.0.1:8080 \
  riftbound-api:local
```

Verify:

```bash
curl -fsS http://127.0.0.1:8080/health
curl -fsS http://127.0.0.1:8080/metrics
```

With no `ConnectionStrings__Riftbound`, identity and match result state use in-memory stores and reset when the process exits.

## Local Verification Record

Latest validated local deployment check:

- Date: 2026-07-06.
- Source revision: `origin/main@5c36f78ddb5a50a363b80276b3fa35515e0edd01`.
- Image tag: `riftbound-api:p4-docker-152848`.
- Build: `docker build -t riftbound-api:p4-docker-152848 .` from a clean `origin/main` worktree.
- Runtime: container started in Production memory mode with configuration supplied through environment variables only.
- Checks:
  - `GET /health` returned `status=ok`, `persistenceMode=memory`, `signalRScaleMode=single-instance`, and `configuredCorsOriginCount=1`.
  - Docker `HEALTHCHECK` transitioned from `starting` to `healthy`.
  - `GET /metrics` exposed process/configuration gauges without match, player, card, or room identifiers.
  - `GET /` served the bundled Dev UI index page.

No cloud resources were created for this check. Public deployment still requires the target platform, registry, domain, and any managed Postgres or Redis credentials to be supplied out of band.

## Run With Postgres

Set `ConnectionStrings__Riftbound` to the Postgres connection string supplied by your hosting environment. The API runs the idempotent SQL files from `src/Riftbound.Persistence/Sql` during startup through `PostgresSchemaInitializer`.

Required production variables:

```bash
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__Riftbound=<postgres connection string from your platform>
Riftbound__DevUiOrigins__0=https://your-public-origin.example
Riftbound__Metrics__Enabled=true
Riftbound__Logging__JsonConsole=true
```

Do not pass connection strings in URLs or logs. Use your platform's environment-variable or managed-configuration mechanism.

## SignalR Scaling

A single container needs no backplane. Multiple API instances need a shared SignalR backplane so room broadcasts and per-player matchmaking messages reach connections on other instances.

Enable Redis backplane by setting one of these:

```bash
ConnectionStrings__SignalRRedis=<redis connection string from your platform>
```

or:

```bash
Riftbound__SignalR__Redis__ConnectionString=<redis connection string from your platform>
Riftbound__SignalR__Redis__ChannelPrefix=riftbound
```

When Redis is not configured, the server stays in single-instance mode. `/health` reports `signalRScaleMode`, and `/metrics` exposes `riftbound_redis_backplane_configured`.

## Health And Metrics

`GET /health` returns service status, .NET version, persistence mode, CORS origin count, metrics state, and SignalR scale mode. It does not include connection strings, player keys, reconnect tokens, hand contents, deck order, or hidden card identity.

`GET /metrics` returns a compact Prometheus-compatible text surface with process health and configuration gauges. It intentionally avoids match, player, card, and room identifiers.

## Rollout

1. Build and push the image with your registry tooling.
2. Configure the environment variables above in the target runtime.
3. Start one instance, check `/health`, then check the Dev UI loads from `/`.
4. If Postgres is configured, inspect startup logs for migration completion and confirm profile/history endpoints work after a test match.
5. For multi-instance rollout, configure Redis before increasing replicas above one.

## Rollback

Keep the previous image tag available. To rollback, redeploy the previous image with the same environment variables. SQL migrations are written to be idempotent and additive; do not manually delete tables as part of an application rollback.

## Cloud Handoff

The remaining platform-specific work is outside this repository: creating managed Postgres or Redis, assigning domains, issuing certificates, choosing a registry, and configuring provider-specific autoscaling or backups.
