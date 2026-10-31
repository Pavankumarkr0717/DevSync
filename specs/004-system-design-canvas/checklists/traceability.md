# Traceability: System Design Canvas

Maps acceptance scenarios, requirements and success criteria to the tests that verify them
(T053). Path prefixes:
- `UT` = `tests/backend/DevSync.UnitTests/`
- `IT` = `tests/backend/DevSync.IntegrationTests/`
- `FE` = `tests/frontend/features/diagrams/`
- `E2E` = [verification.md](../verification.md)

## User stories

| Scenario | Tests |
|----------|-------|
| US1 list, empty state, create, open | IT `Diagrams/DiagramCrudTests.A_new_diagram_is_empty_at_revision_1`, `The_list_is_sorted_by_name_ignoring_case_*`; FE `DiagramListPage.test.tsx`; E2E A1, A2 |
| US1 add each component type, render, delete | FE `DiagramCanvas.test.tsx` (8 types with default title, size and grid snap; drop from the palette; icon and title as text; delete) |
| US2 connect, refuse self and duplicate connections | UT `Domain/Diagrams/DiagramValidatorTests`; IT `DiagramDocumentTests.Every_rule_is_enforced_*`; FE `document.test.ts` (connection rules), `DiagramEditing.test.tsx` (connect, duplicate toast, no self option); E2E A4 |
| US2 edit labels, titles, descriptions, types, metadata | FE `DiagramEditing.test.tsx` (label, title/description/type, limits and counters, duplicate metadata key, 20 entries) |
| US2 deleting a node removes its edges | FE `DiagramEditing.test.tsx`, `DiagramCanvas.test.tsx` |
| US3 autosave, statuses, one save per drag | FE `DiagramSaving.test.tsx` (one drag → one PUT; Unsaved → Saving → Saved; reload keeps it) |
| US3 pan and zoom never save | FE `DiagramSaving.test.tsx` (pan/zoom/fit only → no PUT) |
| US3 conflicts: reload, overwrite, copy | IT `DiagramConcurrencyAndIsolationTests.Of_ten_parallel_saves_*`; FE `DiagramSaving.test.tsx` (conflict bar, Reload latest, Overwrite with mine); E2E A6, A8 |
| US3 deleted, forbidden, invalid, offline | IT `DiagramConcurrencyAndIsolationTests.A_save_racing_a_delete_*`; FE `DiagramSaving.test.tsx` (deleted and forbidden → read only; problems with Details; retry) |
| US3 drafts, stale drafts | FE `DiagramSaving.test.tsx` (hidden page keeps a draft, restore, stale draft → conflict) |
| US3 unsupported format, Start over | UT `Domain/Diagrams/DiagramUpgraderAndNameTests`; IT `DiagramDocumentTests.A_stored_newer_version_*`, `An_unreadable_stored_document_*`; FE `DiagramSaving.test.tsx` (banner, Start over; viewer without it); E2E A13 |
| US4 zoom 10–400%, fit, grid, multi-select, view restore | FE `DiagramNavigation.test.tsx` |
| US5 roles | IT `Authorization/RoleMatrixTests` (6 diagram operations × 5 callers, plus the snapshot); FE `DiagramRoles.test.tsx`; E2E A9–A11 |
| US6 rename, delete, the 50 limit, isolation | IT `DiagramCrudTests.Rename_*`, `The_owner_deletes_*`, `A_workspace_holds_at_most_50_*`, `DiagramConcurrencyAndIsolationTests.Changing_or_deleting_one_diagram_*`; FE `DiagramManage.test.tsx`; E2E A12, A14 |

## Functional requirements

| Requirement | Tests |
|-------------|-------|
| FR-001–FR-003 several diagrams, list, stable id | IT `DiagramCrudTests`; FE `DiagramListPage.test.tsx` |
| FR-004 names | UT `DiagramUpgraderAndNameTests` (DiagramName); IT `DiagramCrudTests.Invalid_names_*`, `Names_are_unique_*`; FE `DiagramListPage.test.tsx`, `DiagramManage.test.tsx` |
| FR-005 a new diagram is empty | IT `DiagramCrudTests.A_new_diagram_is_empty_*`; E2E A1, A2 |
| FR-006 permanent delete with confirmation | IT `DiagramCrudTests.The_owner_deletes_*`; FE `DiagramManage.test.tsx` (name and component count) |
| FR-007 at most 50 | IT `DiagramCrudTests.A_workspace_holds_at_most_50_*` (including parallel creates); FE `DiagramManage.test.tsx`; E2E A14 |
| FR-010–FR-014 nodes, types, limits, palette | UT `DiagramValidatorTests`; FE `DiagramCanvas.test.tsx`, `DiagramEditing.test.tsx` |
| FR-015–FR-018 edges and their rules | UT `DiagramValidatorTests`; IT `DiagramDocumentTests`; FE `document.test.ts`, `DiagramEditing.test.tsx` |
| FR-019–FR-021 navigation, stored view, empty state | FE `DiagramNavigation.test.tsx`, `DiagramCanvas.test.tsx` |
| FR-022, FR-023 one versioned document, revision CAS | IT `DiagramDocumentTests.A_valid_document_round_trips_*`, `Http_preconditions_and_limits`, `DiagramConcurrencyAndIsolationTests`; E2E A3, A6–A8 |
| FR-024–FR-026a statuses, conflicts, autosave, leave | FE `DiagramSaving.test.tsx`; shared controller tests in `tests/frontend/features/files/autosave.test.ts`, `drafts.test.ts` |
| FR-027–FR-029 version 1, upgrade, newer versions | UT `DiagramUpgraderAndNameTests`; IT `DiagramDocumentTests.Other_schema_versions_*`, `A_stored_newer_version_*`; E2E A5, A13 |
| FR-030, FR-031 permissions, 404 for non-members | IT `RoleMatrixTests`; FE `DiagramRoles.test.tsx`; E2E A9–A11 |
| FR-032 addressed by workspace and id | IT `DiagramConcurrencyAndIsolationTests.A_diagram_of_another_workspace_*`, `DiagramCrudTests.Malformed_or_missing_ids_*`, `PathSecurityTests`; E2E A12 |
| FR-033 server authoritative | UT `DiagramValidatorTests`, `Application/Diagrams/DiagramHandlerTests`; IT `DiagramDocumentTests` (every rule, several at once, malformed, unknown property, nothing stored); E2E A4, A7 |
| FR-034 client prevention | FE `document.test.ts`, `DiagramEditing.test.tsx` |
| FR-035 links and routes | FE `DiagramListPage.test.tsx` (Open diagrams), `DiagramManage.test.tsx` (not found) |
| FR-036 messages | FE `DiagramSaving.test.tsx` (first problem + Details), `DiagramListPage.test.tsx` (name errors) |
| FR-037 earlier features unchanged | The full S01–S03 suites (see verification.md) |
| FR-039, FR-040 design system, no behaviour change | Every S01–S03 frontend test still passes after the restyle; visual check is **manual** (verification.md) |

## Non-functional requirements and success criteria

| Item | Tests |
|------|-------|
| NFR-001 no diagram text in logs | IT `DiagramLogScanTests` (in `Diagrams/DiagramConcurrencyAndIsolationTests.cs`); E2E A15 |
| NFR-002 role × operation coverage | IT `RoleMatrixTests` |
| NFR-003 a test per validation rule | UT `DiagramValidatorTests`; IT `DiagramDocumentTests.Every_rule_is_enforced_*` |
| NFR-004 no new infrastructure | `docker-compose.yml` unchanged; `SystemDesignCanvasMigrationTests` |
| SC-001 build a five-component diagram | FE `DiagramCanvas.test.tsx`, `DiagramEditing.test.tsx`; browser walk-through is **manual** |
| SC-002 saved → reload shows the same | IT `DiagramDocumentTests.A_valid_document_round_trips_*`; FE `DiagramSaving.test.tsx`; E2E A3 |
| SC-003 invalid documents always refused | IT `DiagramDocumentTests`; E2E A4 |
| SC-004 one winner per race | IT `Of_ten_parallel_saves_*`; E2E A8 |
| SC-005 no changes from viewers, non-members, anonymous | IT `RoleMatrixTests`; E2E A9–A11 |
| SC-006 diagrams isolated | IT `Changing_or_deleting_one_diagram_*`; FE `DiagramManage.test.tsx` |
| SC-007 earlier tests pass | verification.md |
| SC-008 a diagram at the limits stays usable | **Not measured** (no performance figure claimed; constitution XII) |
| SC-009 one save per drag | FE `DiagramSaving.test.tsx`, `DiagramNavigation.test.tsx` (multi-select drag) |
