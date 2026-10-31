# Traceability: Workspace Foundation

Maps each acceptance scenario, requirement and success criterion to the tests that verify it
(T070, SC-006). Paths are relative to `tests/backend/` (`IT` = DevSync.IntegrationTests,
`UT` = DevSync.UnitTests) or `tests/frontend/` (`FE`), as of the layout in
[ADR 0001](../../../docs/adr/0001-repository-layout.md).

## Acceptance scenarios

| Scenario | Tests |
|----------|-------|
| US1-AS1 create → owner | IT `Workspaces/CreateWorkspaceTests.AS1_*`; UT `Application/CreateWorkspaceHandlerTests`; FE `features/workspaces/CreateWorkspacePage.test.tsx` (navigates to details) |
| US1-AS2 trimming | IT `CreateWorkspaceTests.AS2_*`; UT `Domain/WorkspaceNameTests.Name_is_trimmed`, `WorkspaceDescriptionTests` |
| US1-AS3 blank name | IT `CreateWorkspaceTests.AS3_*`, `Missing_or_null_name_*`; UT `WorkspaceNameTests.Missing_or_blank_*`; FE create page "blocks an empty…" |
| US1-AS4 length limits | IT `CreateWorkspaceTests.AS4_*`; UT `WorkspaceNameTests.Length_*`, `WorkspaceDescriptionTests.Length_*`; FE create page "100 / 500", "100-emoji" |
| US1-AS5 no identity | IT `CreateWorkspaceTests.AS5_*`, `Auth/DevIdentityTests` |
| US2-AS1 list newest first | IT `Workspaces/ListWorkspacesTests.AS1_*`; FE `WorkspaceListPage.test.tsx` |
| US2-AS2 empty list | IT `ListWorkspacesTests.AS2_*`; FE list page "empty state" |
| US2-AS3 others hidden | IT `ListWorkspacesTests.AS3_*`; FE `DevUserPicker.test.tsx` "switching users" |
| US2-AS4 no identity | IT `ListWorkspacesTests.AS4_*` |
| US3-AS1 member details | IT `Workspaces/WorkspaceDetailsTests.AS1_*`, `Missing_description_*`; FE `WorkspaceDetailsPage.test.tsx` |
| US3-AS2 non-member 404 | IT `WorkspaceDetailsTests.AS2_*`, `Not_member_missing_and_malformed_*` |
| US3-AS3 missing/malformed 404 | IT `WorkspaceDetailsTests.AS3_*`; FE details page "404" |
| US4-AS1 runs from README | Quickstart V1 ([verification.md](../verification.md)) |
| US4-AS2 readiness reflects DB | IT `Health/HealthTests`; quickstart V10 |
| US4-AS3 migrations on startup | IT `Persistence/MigrationTests` |
| US4-AS4 placeholder env file | Security pass in verification.md |
| US4-AS5 test command passes | verification.md "Automated suites" |

## Functional requirements

| FR | Tests |
|----|-------|
| FR-001 create + rules | UT Domain/*; IT CreateWorkspaceTests |
| FR-002 owner membership, atomic | UT `WorkspaceTests.Create_makes_the_creator_*`; IT `CreateWorkspacePersistenceTests.Failure_after_*_rolls_back_*` |
| FR-003 unique non-guessable id | UT `WorkspaceTests.Create_assigns_a_version_7_uuid`; IT `Duplicate_names_*`, `Client_supplied_identifiers_*`, `A_duplicate_workspace_id_*` |
| FR-004 list, paging | UT `ListMyWorkspacesHandlerTests`; IT `ListWorkspacesTests` (paging, ordering, validation); FE list page paging |
| FR-005 details | IT `WorkspaceDetailsTests.AS1_*`; FE details page |
| FR-006 identical 404 | IT `WorkspaceDetailsTests.Not_member_missing_and_malformed_*`; quickstart V8 |
| FR-007 field-level validation | IT `CreateWorkspaceTests`, `ListWorkspacesTests.Out_of_range_*`, `Non_integer_*` |
| FR-008 error shape + correlation | IT `Infrastructure/HttpClientExtensions.AssertProblemAsync` (used by every error test), `Errors/CorrelationIdTests` |
| FR-009 identity required | IT `Auth/DevIdentityTests`, each story's no-identity test |
| FR-010 members only | IT `WorkspaceDetailsTests.AS2_*`, `ListWorkspacesTests.AS3_*` |
| FR-011 dev identity, guarded | IT `Auth/DevIdentityTests`, `Auth/StartupGuardTests` |
| FR-012 durable | IT restart tests in Create/List/Details; quickstart V9 |
| FR-013 migrations | IT `MigrationTests` |
| FR-014 health | IT `HealthTests` |
| FR-015 structured logs, no secrets | IT `Observability/LoggingTests`; quickstart V11 |
| FR-016 browser UI | FE all `*.test.tsx` |

## Success criteria

| SC | Evidence |
|----|----------|
| SC-001 clone → healthy via README | verification.md T073 (with documented port override) |
| SC-002 create + see in list < 1 min | Manual browser pass recommended; flow covered by FE create → details test |
| SC-003 restart persistence | IT restart tests ×3 (+ `Seeding_is_idempotent_*`); quickstart V9 |
| SC-004 no data to non-members | IT `WorkspaceDetailsTests.AS2_*`, indistinguishable-404 test; quickstart V8 |
| SC-005 invalid requests store nothing | IT `CreateWorkspaceTests.AssertNoWorkspacesStoredAsync` on every invalid case |
| SC-006 every scenario tested | This document; all suites green |

## Edge cases from the spec

| Edge case | Tests |
|-----------|-------|
| Duplicate names | IT `Duplicate_names_are_allowed_*` |
| Control characters | UT `WorkspaceNameTests.Control_*`, `WorkspaceDescriptionTests.Other_control_*`; IT AS3 `bad\u0000name` |
| Malformed body / missing fields | IT `Malformed_body_*`, `Missing_or_null_name_*` |
| Unknown fields ignored | IT `Client_supplied_identifiers_and_unknown_fields_*` |
| DB unavailable | IT `*_returns_503_*` ×3, `HealthTests`, `Errors/DatabaseOutageTests`; quickstart V10 |
| Partial create rolled back | IT `Failure_after_the_workspace_insert_rolls_back_*` |
| DB backstop constraints | IT `The_database_rejects_a_blank_name_*`, `A_duplicate_workspace_id_*` |
| Large lists paged | IT `Pages_through_25_*`, `Order_is_stable_*` |
