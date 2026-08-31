# M8 Architecture event model

Status: `CONFIRMED` for the vNext living-architecture reference slice.

`ArchitectureModel` records bounded-context modules, module dependencies, data
entities, data relationships, and explainable drift findings as an immutable
event stream. The inline `ArchitectureCurrent` projection drives the model
editor and ERD, while the async `ArchitectureOverview` view provides
rebuildable counts for dashboards. Drift includes a stable key and evidence so
confirmed source facts, inferred structure, and proposed design can remain
traceable without direct writes across modules.
