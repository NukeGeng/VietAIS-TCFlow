# GOAL2 M13 Integrations operational migration check

Status: `CONFIRMED` for the redacted GitHub operational-document slice only.
This artifact does not claim live GitHub installation, broker cutover, or full
M13/M14 production readiness.

## Scope

`GitHubCredential` and `GitHubDelivery` are retained as operational documents,
not business-event streams. The writer stores deterministic identity, source
reference, payload hash, project scope, external identifier, and a whitelist of
non-secret metadata. It never stores raw export payloads, access/refresh
tokens, private keys, client/webhook secrets, passwords, or signatures.

The same source/hash identity is idempotent: an existing matching document is
skipped, a changed hash fails closed, and a ledger entry without a document is
rejected. The read-only `--reconcile-marten` command verifies the document's
source reference, kind, and payload hash.

## Verification

| Check | Result |
| --- | ---: |
| Redacted GitHub credential and delivery documents applied | 2 upserted |
| Repeat apply | 0 upserted, 2 skipped |
| Secret-bearing credential payload | rejected before write |
| Operational source/hash reconciliation | passed |
| `MartenOperationalMigrationApplierTests` | 2 passed, 0 failed |
| `MartenMigrationReconcilerTests` | 3 passed, 0 failed |
| Full `Migration.Tests` suite | 33 passed, 0 failed |

## Remaining M13/M14 obligations

This check does not prove live GitHub App OAuth/installation, private-repository
access, webhook delivery, RabbitMQ retry/dead-letter behavior, backup/restore,
rollback, or semantic pre/post business invariants. Those remain open in the
M13/M14 acceptance records.
