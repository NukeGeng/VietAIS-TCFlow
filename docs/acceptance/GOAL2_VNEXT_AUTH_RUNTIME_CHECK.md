# GOAL2 vNext isolated API runtime check

Status: `CONFIRMED` for the checks below; this is a local, isolated runtime
artifact and is not production evidence.

## Environment

- API: `VietAIS.TCFlow.Api` from the M13 runtime-gates worktree
- Database: fresh PostgreSQL database `vnext_runtime` on the existing local
  PostgreSQL container; FullStackHero migrations applied before startup
- Transport: Wolverine PostgreSQL durable storage; no RabbitMQ claim is made
- Credentials: short-lived local seed identity; no token, password, or private
  key is recorded in this document

## Redacted transcript (2026-08-31, Asia/Ho_Chi_Minh)

| Check | Result |
| --- | ---: |
| `GET /health` | `200` |
| `GET /health/live` | `200` |
| `GET /health/ready` | `200` |
| `GET /openapi/v1.json` | `200` |
| Unauthenticated vNext project query | `401` |
| Identity token issue with local seed user | `200` (token omitted) |
| Authenticated project create | `201` |
| Authenticated project query (inline projection) | `200` |
| Authenticated portfolio summary (async projection) | `200` |
| Authenticated command with a spoofed `OwnerId` | `403` |

The `403` result comes from the endpoint filter that requires a command's
`ActorId`/`OwnerId` to match the authenticated `NameIdentifier` claim. The
GitHub webhook remains explicitly anonymous and is authenticated by its
signature processor instead.

## Not covered by this artifact

This check does not prove RabbitMQ retry/dead-letter behavior, a restored v0.1
backup, model-level migration apply/reconciliation, rollback, private GitHub
ingestion, or the Vue browser workflow. Those remain M13/M14 gates.
