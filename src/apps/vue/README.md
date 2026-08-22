# VietAIS TCFlow web

Vue 3 + TypeScript + Vite product shell for VietAIS TCFlow.

The product workspace consumes the verified P2/P3 identity, project,
authorization, repository, and task contracts. It includes session refresh,
permission-aware navigation, project and repository management, analysis and
impact views, feature grouping, a task board, task review/history, and separate
project/system administration surfaces.

## Requirements

- Node.js `^22.18.0` or `>=24.12.0`; Node 24 LTS is recommended
- npm 11 or a compatible package manager

The repository uses `.nvmrc` to pin the verified Node 24 release.

## Commands

```sh
npm install
npm run dev
npm run type-check
npm run lint
npm run test:unit -- --run
npm run build
```

Vite reads `PORT` when started by Aspire and defaults to `5173` otherwise.
Aspire service discovery is used as the development API proxy target. For a
separately hosted API, configure `VITE_API_BASE_URL` at build time or
`VITE_API_PROXY_TARGET` while running Vite.

## Architecture baseline

- Vue Router owns application navigation.
- Pinia owns client-side state.
- The typed native-fetch client matches backend camel-case JSON and numeric enum contracts.
- Access and refresh tokens live in session storage and are cleared after an unrecoverable `401`.
- Vitest and Vue Test Utils verify component behavior.
- ESLint, Oxlint, and Prettier enforce source quality.

Frontend permission checks added later are user experience only. The ASP.NET
backend remains authoritative for authorization.

## Verified contract boundaries

- P3 exposes feature creation but no standalone feature-list endpoint. The
  feature view therefore displays newly created features and confirmed feature
  identities derived from task responses.
- P3 exposes source trace through tasks but no analysis/impact query endpoint.
  Analysis and impact views render only confirmed task trace data; P5 and later
  analyzer APIs can replace that source without changing the UI state model.
- The effective-permission endpoint requires `role.view`. When a member cannot
  inspect that endpoint, privileged controls remain disabled and explain the
  missing permission instead of assuming access.
