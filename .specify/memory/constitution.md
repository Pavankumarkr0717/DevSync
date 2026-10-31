<!--
Sync Impact Report
- Version change: (unversioned template) → 1.0.0
- Modified principles: template placeholders → 12 defined principles (I–XII)
- Added sections: Core Principles I–XII; Technology & Architecture Constraints;
  Development Workflow & Quality Gates; Governance
- Removed sections: none
- Templates: none modified (dependent templates read the constitution at runtime)
- Follow-up TODOs: none
-->

# DevSync Constitution

DevSync is a real-time collaborative engineering workspace for code editing, system-design
diagrams, and secure code execution. This constitution governs every feature built for it.

## Core Principles

### I. Incremental, Specification-Driven Development

- Work MUST implement only the currently approved specification and its direct dependencies.
- Future features MUST NOT be scaffolded, stubbed, or pre-wired ahead of their approval.
- Unspecified requirements MUST be raised as questions or recorded as explicit assumptions;
  they are never permission to build speculative functionality.

Rationale: small, approved increments keep the codebase honest and reviewable.

### II. Clean Architecture & Separation of Concerns

- Code MUST separate API (transport), application/business logic, domain, infrastructure,
  and frontend presentation/state into distinct layers.
- Domain and application logic MUST NOT depend on transport or infrastructure details.
- Abstractions SHOULD exist only where they have more than one real use or isolate an
  external dependency; speculative interfaces and microservices MUST NOT be introduced.

### III. Correctness Before Performance

- Every feature MUST define measurable acceptance criteria before implementation.
- All external input MUST be validated at the boundary; errors MUST be handled explicitly
  and surfaced with actionable messages, never silently swallowed.
- Where concurrency, reconnection, or retries apply, that behavior MUST be tested.
- Performance optimization SHOULD follow correctness and be justified by measurement.

### IV. Security by Default

- Every protected operation MUST be authorized on the server; client-side checks are
  convenience only.
- Every workspace-scoped operation (HTTP or real-time) MUST verify workspace membership.
- Secrets MUST NOT be committed to source control.
- Untrusted code execution MUST run in an isolated sandbox outside the API process, with
  resource limits, and MUST NOT expose arbitrary host filesystem, network, or process access.

### V. Testability

- Backend business logic MUST have unit tests.
- API endpoints and persistence behavior MUST have integration tests.
- Frontend behavior MUST have component tests.
- Every bug fix MUST include a test that reproduces the defect and guards against regression.

### VI. Real-Time Correctness

Before any collaboration feature is implemented, its specification or plan MUST define:

- Message schemas (versioned and validated on receipt).
- Workspace/room boundaries and the authorization applied to each.
- Ordering and versioning rules.
- Reconnection and resynchronization behavior.
- Conflict-handling semantics.

### VII. Persistence & Recoverability

- Persisted state MUST be authoritative wherever durability is required; in-memory state
  MUST NOT be treated as durable storage.
- Schema changes MUST be applied through versioned migrations.
- Multi-step writes MUST use explicit transaction boundaries.
- Snapshots of collaborative state MUST be validated before being persisted or restored.

### VIII. Observable Engineering

- Services MUST emit structured logs carrying a correlation ID per request or session.
- Deployable services MUST expose health checks; metrics SHOULD be added where they inform
  operation of a feature.
- Credentials, tokens, and user source content MUST NOT be logged by default.

### IX. Simplicity & Cost Awareness

- The system MUST start as a modular monolith backed by PostgreSQL and run locally via
  Docker Compose.
- Redis, RabbitMQ, distributed hosting, and cloud infrastructure MUST NOT be introduced
  unless an approved specification explicitly requires them and justifies the cost.

### X. Developer Experience

- Setup MUST be reproducible from documented instructions.
- An environment example file (e.g. `.env.example`) MUST list required variables with
  placeholder values only — never real secrets.
- Errors SHOULD be useful to the person who sees them (user or developer).
- Each completed milestone MUST update the README covering what was delivered and how to
  run and verify it.

### XI. Dependency Discipline

- Hard problems (collaborative editing/CRDT/OT, sandbox isolation, cryptography, auth)
  SHOULD use maintained, well-adopted libraries.
- Cryptographic or collaboration primitives MUST NOT be built from scratch unless the
  specification states an explicit learning objective, and such code MUST NOT protect
  production data.
- Significant dependency choices MUST be documented in the feature plan with the reason
  and alternatives considered.

### XII. Evidence-Based Performance

- Throughput, latency, user-count, cost-saving, or reliability figures MUST NOT be stated
  without measured evidence.
- Any published benchmark MUST include methodology, environment, and steps to reproduce.

## Technology & Architecture Constraints

- Baseline stack: modular monolith, PostgreSQL, Docker Compose for local development.
- Module boundaries follow Principle II; cross-module access goes through application-layer
  interfaces, not direct infrastructure or database access to another module's data.
- Code execution runs in a separate isolated runtime (Principle IV); the API process only
  submits jobs and receives results.
- Additional infrastructure requires an approved specification (Principle IX).

## Development Workflow & Quality Gates

- Each feature MUST progress through: specification with acceptance criteria → technical
  plan → actionable tasks → implementation → verification evidence.
- The technical plan MUST include a Constitution Check against these principles; any
  deviation MUST be justified in writing or the plan revised.
- Before merging, review MUST confirm: security (Principle IV), architecture (Principle II),
  and test coverage (Principle V), and that verification evidence exists for each
  acceptance criterion.
- SHOULD-level exceptions MUST be documented in the plan with rationale.

## Governance

- This constitution supersedes conflicting practices, templates, and guidance.
- Amendments MUST be made by editing this file, recording the rationale in the change, and
  updating the version below.
- Versioning follows semantic versioning:
  - MAJOR: removal or backward-incompatible redefinition of a principle or governance rule.
  - MINOR: new principle or section, or materially expanded guidance.
  - PATCH: clarifications and wording fixes without semantic change.
- Compliance is checked at plan time (Constitution Check) and at review time (quality gates).
- MUST denotes a non-negotiable requirement; SHOULD denotes a recommendation whose exceptions
  MUST be documented.

**Version**: 1.0.0 | **Ratified**: 2026-10-29 | **Last Amended**: 2026-10-29
