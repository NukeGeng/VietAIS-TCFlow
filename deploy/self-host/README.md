# Self-host deployment

> **Default profile:** This bundle still deploys the current .NET 9 baseline.
> It remains the v0.1 rollback path until milestone M13 is confirmed. The
> optional `goal2` profile adds the .NET 10 vNext API, Marten async daemon, and
> RabbitMQ integration boundary without removing the rollback service.

This bundle runs VietAIS TCFlow on a single Docker host with the same service
topology used by local development: the ASP.NET API, Vue frontend, PostgreSQL,
and Redis. The compose file defaults to release `2.0.4-rc`; set
`TCFLOW_VERSION` when publishing or consuming another version.

## First install

From the repository root:

```bash
cd deploy/self-host
cp .env.example .env
```

Replace every `replace-with-...` value in `.env` with a unique secret. For
example, `openssl rand -base64 48 | tr -d '\n'` produces a suitable random
value. Do not commit `.env` or paste GitHub private keys into source control.

For GitHub integration, set the App ID, slug, client ID, client secret, and
the Base64-encoded PEM private key. The GitHub App OAuth callback must exactly
match `TCFLOW_PUBLIC_URL/github/callback`; a public HTTPS URL is required when
GitHub needs to reach a non-local installation. Webhooks remain optional.
When webhooks are enabled, set the GitHub App webhook URL to
`TCFLOW_PUBLIC_URL/api/v1/github/webhooks`, use the same random value as
`GITHUB_WEBHOOK_SECRET`, and subscribe to the Push and Pull request events.

Start the stack:

```bash
docker compose up -d --build
curl --fail http://localhost:${TCFLOW_HTTP_PORT:-8080}/health
```

## GOAL2 canary profile

After reviewing the migration evidence for the target environment, start the
vNext canary alongside the rollback API:

```bash
docker compose --profile goal2 up -d --build
curl --fail http://localhost:${TCFLOW_HTTP_PORT:-8080}/health
curl --fail http://localhost:${TCFLOW_HTTP_PORT:-8080}/api/vnext/health
```

The profile provisions RabbitMQ with credentials from `RABBITMQ_USER` and
`RABBITMQ_PASSWORD`, and routes `/api/vnext/` through the vNext container. It
does not switch existing `/api/` traffic or delete the v0.1 service. Verify
RabbitMQ publish/retry/dead-letter behavior and projection/rollback evidence
before making vNext the primary route.

The API applies the existing EF migrations and seeds the root tenant on first
startup. Repository Intelligence uses the `repository_intelligence` Marten
schema in the same PostgreSQL database. Follow logs with
`docker compose logs -f api`.
Uploaded images/files and the optional reasoning working directory are stored
in named Docker volumes so recreating the API container does not remove them.

## Upgrade

1. Back up PostgreSQL before changing versions.
2. Set `TCFLOW_VERSION` to the target image tag (or keep `--build` when using
   a checked-out source tree).
3. Pull or build the images and recreate the services:

```bash
docker compose pull
docker compose up -d
curl --fail http://localhost:${TCFLOW_HTTP_PORT:-8080}/health
```

Database migrations run during API startup. Review the release notes and keep
a database backup before applying a version with schema changes; rolling back
the application image does not automatically roll back database migrations.

## Backup and restore

Create a PostgreSQL custom-format backup from the running database container.
The command reads the database name and user from the container environment, so
the credentials are not written into the backup command or shell history:

```bash
mkdir -p backups
backup_file="backups/vietais-tcflow-$(date -u +%Y%m%dT%H%M%SZ).dump"
docker compose exec -T postgres sh -c \
  'pg_dump --format=custom --no-owner --no-privileges -U "$POSTGRES_USER" "$POSTGRES_DB"' \
  > "$backup_file"
```

To restore, stop the API and frontend first, then restore into the existing
PostgreSQL volume. This replaces the database contents; take a fresh backup
before running it:

```bash
docker compose stop api frontend
cat backups/vietais-tcflow-YYYYMMDDTHHMMSSZ.dump | \
  docker compose exec -T postgres sh -c \
  'pg_restore --clean --if-exists --no-owner --no-privileges -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
docker compose up -d api frontend
curl --fail http://localhost:${TCFLOW_HTTP_PORT:-8080}/health
```

Keep backup files outside the repository and protect them as production data.
The Redis AOF and uploaded files remain in named Docker volumes; include those
volumes in the host backup policy when they must survive host loss.

## Optional reasoning worker

`REPOSITORY_REASONING_ENABLED` is `false` by default. Enabling it requires a
custom API image that contains the configured reasoning executable and a
persisted working directory. A managed Codex worker is not part of this
self-host bundle. Leave `REPOSITORY_REASONING_MODEL` empty to use the managed
account default; TCFlow omits the model field in that case instead of sending
an empty model identifier.

## Live acceptance checklist

The deterministic fixture and CI smoke test do not prove a live GitHub push or
managed-account reasoning turn. Use this checklist only after the stack has a
public HTTPS address and the GitHub App is installed on a supported Vue +
ASP.NET Core + Marten repository.

1. Set `TCFLOW_PUBLIC_URL` to the public origin, set all GitHub App values in
   `.env`, and configure the App OAuth callback as
   `TCFLOW_PUBLIC_URL/github/callback`.
2. If testing incremental monitoring, configure the App webhook URL as
   `TCFLOW_PUBLIC_URL/api/v1/github/webhooks`, set a random
   `GITHUB_WEBHOOK_SECRET`, and subscribe to `Push` and `Pull request`.
3. Start the stack and verify both the API health response and the frontend:

   ```bash
   docker compose up -d --build
   curl --fail "${TCFLOW_PUBLIC_URL}/health"
   curl --fail "${TCFLOW_PUBLIC_URL}/"
   ```

4. Sign in as the project owner, connect the GitHub account, select the
   repository, and use **Analyze now**. The analysis detail must reach
   `Completed` (or `Unsupported` with `ANALYSIS001` for an intentionally
   unsupported repository); it must not remain pending.
5. For the supported-repository gate, commit a meaningful contract change and
   push it. Confirm the webhook response is accepted, the analysis request
   references the before/after revisions, and the resulting run records changed
   files, impacts, and a source-traceable task when the project AI policy
   permits it. Repeat the same delivery to confirm idempotency.
6. For the managed reasoning gate, set `REPOSITORY_REASONING_ENABLED=true`,
   point `REPOSITORY_REASONING_EXECUTABLE` at an authenticated managed Codex
   executable in the custom image, enable the Codex provider through the system
   configuration API, and grant the project only the intended AI permissions.
   Confirm the reasoning status progresses from `Pending`/`Processing` to
   `Completed`, the task contains evidence and confidence, and the audit trail
   records the AI action separately from human approval.
7. Save the analysis detail, task history, audit entries, and delivery id as
   acceptance evidence. Rotate webhook/client secrets after the test and do not
   include tokens, private keys, or raw source payloads in the evidence.
