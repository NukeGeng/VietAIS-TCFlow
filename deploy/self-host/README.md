# Self-host deployment

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

## Optional reasoning worker

`REPOSITORY_REASONING_ENABLED` is `false` by default. Enabling it requires a
custom API image that contains the configured reasoning executable and a
persisted working directory. A managed Codex worker is not part of this
self-host bundle.
