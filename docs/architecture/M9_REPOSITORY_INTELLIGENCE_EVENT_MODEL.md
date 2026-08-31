# M9 RepositoryIntelligence event model

Status: `CONFIRMED` for the vNext normalized-analysis reference slice.

Technical analyzers publish deterministic facts through contracts; the
`AnalysisRun` aggregate records source artifacts, source changes, evidence,
and completion. Stable keys make repeated observations idempotent at the
business boundary. The inline analysis view supports immediate traceability,
while async Knowledge Graph and Impact Graph views are derived state that can
be rebuilt from streams. AI reasoning is deliberately downstream of static
fact extraction and stores confidence/evidence instead of replacing facts.
