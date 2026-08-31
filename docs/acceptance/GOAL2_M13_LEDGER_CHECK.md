# GOAL2 M13 migration ledger check

Status: `CONFIRMED` for the operational checkpoint only. This artifact does
not claim that legacy records have been appended to business event streams.

## Scope

The versioned `Goal2Migration` tool was run against the redacted fixture
`docs/migration/examples/goal2-export.v1.json` from the M13 branch. The ledger
was created in an isolated temporary directory and removed after the check.

## Results

| Check | Result |
| --- | ---: |
| Schema-versioned dry run | 3 operations written |
| First `--ledger --apply` run | 3 appended, 0 skipped, 3 ledger entries |
| Repeated `--ledger --apply` run | 0 appended, 3 skipped, 3 ledger entries |
| Duplicate source with changed payload hash | rejected before write |
| Ledger output | contains source references, hashes, target metadata; no payloads or secrets |

The ledger write uses a same-directory temporary file followed by an atomic
replace. A hash conflict for an existing source reference fails closed, so an
operator cannot accidentally treat changed legacy data as an idempotent retry.

## Remaining M13 work

The ledger is only the resumable cutover checkpoint. Typed model-level writers,
the redacted Integrations operational-document writer, and a read-only Marten
source-marker/hash reconciler now cover the bounded contexts listed above, but
the M13 gate remains open: semantic pre/post count and invariant
reconciliation, isolated backup/restore, and rollback rehearsal are still
required before the milestone can become `CONFIRMED`. See
[`GOAL2_M13_RECONCILIATION_CHECK.md`](GOAL2_M13_RECONCILIATION_CHECK.md) for
the narrower stream-level evidence.
