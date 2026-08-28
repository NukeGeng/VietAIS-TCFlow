# P14 Quality Benchmark Report

Status: `CONFIRMED` for the checked-in deterministic fixture and this machine.

Measured on 2026-08-27 with .NET SDK 9.0.120 on macOS arm64. The executable
benchmark is
`EndToEndQualityBenchmarkTests.SupportedVerticalSliceMeetsP14QualityTargets`.

| Metric | Result | Required target | Result |
| --- | ---: | ---: | --- |
| Precision | 100% (22/22 predicted positives correct) | >= 95% | Pass |
| Recall | 100% (22/22 expected positives found) | >= 95% | Pass |
| False-positive rate | 0% (0/5 negative probes) | <= 5% | Pass |
| False-negative rate | 0% (0/22 expected positives) | <= 5% | Pass |
| Task duplication rate | 0% after 10 identical proposals | 0% | Pass |
| Task reconciliation accuracy | 100% (7/7 canonical actions) | 100% | Pass |
| Deterministic fast-path p95 | 1.78 ms for 20 changed files | < 2,000 ms | Pass |

The 27 labeled facts cover direct technology signals, Vue request/response
intent, ASP.NET endpoint/request/response/permission facts, Marten documents,
frontend-to-endpoint and endpoint-to-document edges, matched contracts,
retrieval exclusion, phantom records, and mismatch noise. Ground truth and
thresholds are versioned in
`samples/end-to-end-acceptance/expected/quality-targets.json`.

The benchmark measures the deterministic reference fixture, not arbitrary
repositories. React/Next.js remains explicitly outside the initial scope in
`GOAL.md` section 71; an unsupported repository must report that status and
must not invent source facts or tasks.

Run:

```bash
/opt/homebrew/opt/dotnet@9/bin/dotnet test \
  src/analyzers/tests/monitoring/VietAIS.TCFlow.Analyzers.Monitoring.Tests.csproj \
  --filter FullyQualifiedName~EndToEndQualityBenchmarkTests
```
