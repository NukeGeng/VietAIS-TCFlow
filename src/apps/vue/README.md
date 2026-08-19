# VietAIS TCFlow web

Vue 3 + TypeScript + Vite product shell for VietAIS TCFlow.

This milestone intentionally provides navigation, delivery status, and planned
route placeholders only. Login, project, repository, task, and administration
flows will be implemented after the P2/P3 backend contracts are verified.

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

## Architecture baseline

- Vue Router owns application navigation.
- Pinia owns client-side state.
- Vitest and Vue Test Utils verify component behavior.
- ESLint, Oxlint, and Prettier enforce source quality.

Frontend permission checks added later are user experience only. The ASP.NET
backend remains authoritative for authorization.
