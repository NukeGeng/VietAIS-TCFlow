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

- Feature create, update, delete, and paged-list routes are persisted and the
  feature view reloads their backend state after each mutation.
- Repository analysis exposes latest and request-specific status routes. The
  analysis view polls in-progress GitHub runs and displays supported,
  unsupported, awaiting-reasoning, failed, diagnostic, and task-count states.
- There is no standalone knowledge-graph query route. The impact view therefore
  renders only confirmed impact identities already projected onto visible
  source-aware tasks.
- The effective-permission endpoint requires `role.view`. When a member cannot
  inspect that endpoint, privileged controls remain disabled and explain the
  missing permission instead of assuming access.
