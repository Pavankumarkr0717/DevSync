# ADR 0001: Repository layout

**Status**: Accepted | **Date**: 2026-10-30

## Context

Feature 001 started with `backend/` and `frontend/` folders at the root, tests next to their
code, `DevSync.slnx` and `compose.yaml`. The project owner chose a layout that separates
production code (`src/`), tests (`tests/`), documentation (`docs/`) and CI (`.github/`).

## Decision

```text
.github/workflows/ci.yml       CI: backend build+tests, frontend checks, image builds
docs/{architecture,adr,benchmarks}
specs/NNN-feature/             Spec Kit artifacts, created only when a feature is approved
src/backend/                   DevSync.sln + Api, Application, Domain, Infrastructure
src/frontend/                  npm package (package.json, Vite, source)
tests/backend/                 DevSync.UnitTests, DevSync.IntegrationTests
tests/frontend/                component tests and test support (MSW handlers, render helpers)
Directory.Build.props, Directory.Packages.props, global.json, .config/  (repo root, shared by src and tests)
docker-compose.yml, .env.example, README.md
```

- `Directory.Build.props`, `Directory.Packages.props` and `global.json` live at the root, so
  both `src/backend` and `tests/backend` use the same build settings, central package versions,
  SDK and test runner. The API image is therefore built from the root context
  (`src/backend/Dockerfile` plus a root `.dockerignore` that admits only what it needs).
- Frontend tests sit outside the npm package. To let them resolve packages, `npm install` runs
  `src/frontend/scripts/link-test-deps.mjs`, which links `tests/frontend/node_modules` to
  `src/frontend/node_modules`. Tests import app code through the `@/` alias (`vite.config.ts`
  and `tsconfig.test.json`), and Vitest scans `tests/frontend` via `test.dir`.
- Future feature spec folders are **not** created ahead of approval (constitution Principle I).

## Consequences

- `dotnet test --solution src/backend/DevSync.sln` and `npm test` (in `src/frontend`) work from
  any machine after `npm install`.
- Frontend tests depend on the link created by `postinstall`. If `npm install` is skipped or
  run with `--ignore-scripts`, run `node scripts/link-test-deps.mjs` manually.
- Lint for tests uses `tests/frontend/eslint.config.mjs`, which re-exports the frontend config.
- A fresh clone no longer has `backend/` or `frontend/` at the root; older docs from feature 001
  (`tasks.md`, `verification.md`) describe the original paths as historical record.
