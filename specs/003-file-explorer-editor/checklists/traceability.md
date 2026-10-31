# Traceability: Project Files and Code Editor

Maps acceptance scenarios, requirements and success criteria to the tests that verify them
(T079). Path prefixes:
- `UT` = `tests/backend/DevSync.UnitTests/`
- `IT` = `tests/backend/DevSync.IntegrationTests/`
- `FE` = `tests/frontend/features/files/`
- `E2E` = [verification.md](../verification.md)

## User stories

| Scenario | Tests |
|----------|-------|
| US1-AS1 empty workspace, create actions | FE `FileExplorer.test.tsx` (empty state); IT `Files/TreeTests.A_new_workspace_has_an_empty_tree_*` |
| US1-AS2/AS3 nested folders, file opens | UT `Domain/Files/ProjectTreeTests.Nested_*`, `Nesting_is_allowed_to_depth_20_*`; IT `TreeTests.Nested_folders_*`; FE `FileExplorer.test.tsx` (creates nested folders and a file, which opens) |
| US1-AS4 invalid / unsupported / duplicate names | UT `ItemNameTests`, `SupportedLanguagesTests`, `ProjectTreeTests.Duplicate_*`; IT `CreateItemTests`, `PathSecurityTests`; FE `FileExplorer.test.tsx` (validation, server messages) |
| US1-AS5 tree after reload | IT `TreeTests.Nested_*` (second member, fresh request); FE `FileEditing.test.tsx` (reload) |
| US2-AS1 open with highlighting | FE `FileEditing.test.tsx` (language from extension); `fileRules.test.ts` (`languageFor`); Monaco rendering is **manual** (not run) |
| US2-AS2 no save per keystroke, debounce, max wait | FE `autosave.test.ts` (1 s, ≤ 7 saves/60 s), `FileEditing.test.tsx` (one PUT for many keystrokes) |
| US2-AS3 Saving → Saved | FE `autosave.test.ts`, `FileEditing.test.tsx` |
| US2-AS4 Save Failed, edits kept, retry | FE `autosave.test.ts` (retry 2/4/8 s, cap 30 s, manual retry), `FileEditing.test.tsx` (Retry button) |
| US2-AS5 saved content after reload | IT `ContentTests`; FE `FileEditing.test.tsx`; E2E P5 |
| US2-AS6 immediate save on switch, close, leave | FE `FileEditing.test.tsx` (switching), `EditorTabs.test.tsx` (switch, close) |
| US2-AS7 unsaved edits on reload → save + draft | FE `drafts.test.ts`, `FileEditing.test.tsx` (pagehide draft, restore, discard) |
| US2-AS8 over 1 MiB | UT `FileContentRulesTests`; IT `ContentTests.Content_at_exactly_1_MiB_*`; FE `FileEditing.test.tsx`; E2E P8 |
| US3-AS1/AS2 conflict instead of overwrite | IT `Files/ContentConcurrencyTests` (10 parallel, stale); FE `ConflictBar.test.tsx`; E2E P6, P16 |
| US3-AS3 reload / overwrite / copy | FE `ConflictBar.test.tsx`; IT `ContentConcurrencyTests.A_save_based_on_an_old_revision_*` (informed overwrite) |
| US3-AS4 same person, two tabs | IT `ContentConcurrencyTests.The_same_person_in_two_tabs_*` |
| US3-AS5 rename/move by others don't block a save | IT `ContentConcurrencyTests.Rename_and_move_*`; FE `EditorTabs.test.tsx` (rename shown after focus) |
| US3-AS6 file deleted by others | IT `ContentConcurrencyTests.Saving_a_deleted_file_*`, `A_save_racing_a_delete_*`; FE `ConflictBar.test.tsx` (deleted) |
| US4-AS1/AS2 rename keeps content; extension rules | UT `ProjectTreeTests.Rename_*`; IT `Files/RenameMoveTests.Rename_*`; FE `OrganizeItems.test.tsx` |
| US4-AS3 move with subtree | UT `ProjectTreeTests.A_folder_moves_*`; IT `RenameMoveTests.Files_and_folders_move_*`; FE `OrganizeItems.test.tsx` |
| US4-AS4 move into self or descendant | UT/IT `*Moving_into_itself_*`; FE `OrganizeItems.test.tsx` (disabled targets); E2E P11 |
| US4-AS5 clash or too deep | UT `ProjectTreeTests.Moving_onto_*`, `A_move_that_pushes_*`; IT `RenameMoveTests.The_target_must_be_*`, `A_move_that_pushes_*` |
| US4-AS6 Owner deletes folder with count | IT `Files/DeleteItemTests.Deleting_a_folder_*`; FE `OrganizeItems.test.tsx` ("and 6 items inside") |
| US4-AS7 Editor cannot delete | IT `DeleteItemTests.Editors_and_viewers_cannot_delete`, `RoleMatrixTests` (DeleteItem); FE `OrganizeItems.test.tsx`, `FilesRoles.test.tsx`; E2E P14 |
| US4-AS8 concurrent structure changes | IT `Files/StructureConcurrencyTests`, `CreateItemTests.Two_parallel_creates_*` |
| US5-AS1 Viewer read-only UI | FE `FilesRoles.test.tsx` |
| US5-AS2 Viewer direct API calls refused | IT `RoleMatrixTests`, `PrivilegeEscalationTests.A_viewer_cannot_write_files_*`; E2E P13 |
| US5-AS3 non-member → not found | IT `RoleMatrixTests` (NonMember); FE `FilesRoles.test.tsx`; E2E P15 |
| US5-AS4 Editor/Owner allowed; only Owner deletes | IT `RoleMatrixTests`; FE `FilesRoles.test.tsx` |
| US5-AS5 downgrade while editing | IT `PrivilegeEscalationTests.An_editor_downgraded_*`; FE `FilesRoles.test.tsx` (read-only + copy) |
| US5-AS6 removed while open | IT `PrivilegeEscalationTests.A_removed_member_cannot_read_files` |
| US6-AS1 focus existing tab | FE `EditorTabs.test.tsx` |
| US6-AS2 per-tab text and status | FE `EditorTabs.test.tsx` (unsaved edits); cursor and scroll: **manual** (Monaco, not run) |
| US6-AS3 same-name disambiguation | FE `EditorTabs.test.tsx` |
| US6-AS4 tabs restored after refresh | FE `EditorTabs.test.tsx`, `tabStorage.test.ts` |
| US6-AS5 tab limit | FE `EditorTabs.test.tsx` (20 + toast) |

## Functional requirements

| FR | Tests |
|----|-------|
| FR-001–FR-003 tree, listing, stable identity | UT `ProjectTreeTests`; IT `TreeTests`, `RenameMoveTests.Rename_keeps_*`; FE `FileExplorer.test.tsx` |
| FR-004 unique names ignoring case | UT `ProjectTreeTests`; IT `CreateItemTests`, `ProjectFilesMigrationTests.Sibling_names_*` |
| FR-005 name rules | UT `ItemNameTests`; IT `PathSecurityTests`, `RenameMoveTests.Rename_rejects_*`; FE `fileRules.test.ts` |
| FR-006 path ≤ 1,024 · FR-007 depth ≤ 20 · FR-008 ≤ 5,000 items | UT `ProjectTreeTests`; IT `CreateItemTests`, `RenameMoveTests`, `StructureConcurrencyTests.Parallel_creates_*` |
| FR-010/FR-011 extensions, language | UT `SupportedLanguagesTests`; IT `CreateItemTests`; FE `fileRules.test.ts`, `FileEditing.test.tsx` |
| FR-012–FR-015 create, rename, move, delete | UT `ProjectTreeTests`, `Application/Files/*HandlerTests`; IT `TreeTests`, `CreateItemTests`, `RenameMoveTests`, `DeleteItemTests`; FE `FileExplorer.test.tsx`, `OrganizeItems.test.tsx` |
| FR-016 content limits | UT `FileContentRulesTests`; IT `ContentTests`; E2E P8, P9 |
| FR-017 exact storage | UT `FileContentRulesTests.Text_is_returned_exactly_*`; IT `ContentTests.Content_round_trips_*`; E2E P5 |
| FR-018/FR-019 revisions, conflicts | UT `SaveFileContentHandlerTests`; IT `ContentTests`, `ContentConcurrencyTests`; FE `filesApi.test.ts`; E2E P6, P7, P16 |
| FR-020–FR-022 autosave, flush, status | FE `autosave.test.ts`, `FileEditing.test.tsx`, `EditorTabs.test.tsx` |
| FR-023 conflict choice | FE `ConflictBar.test.tsx` |
| FR-024–FR-027 authorization, scoping | UT `WorkspacePermissionsTests`; IT `RoleMatrixTests` (70 cases), `PrivilegeEscalationTests`, `PathSecurityTests`; E2E P12–P15 |
| FR-028 distinct error codes | IT `CreateItemTests`, `ContentTests`, `RenameMoveTests`, `PathSecurityTests` (each asserts its code) |
| FR-029 all-or-nothing structure changes | IT `StructureConcurrencyTests`, `CreateItemTests.Two_parallel_*`, `ContentConcurrencyTests.A_save_racing_*` |
| FR-030–FR-034 web app | FE `FilesPage.test.tsx`, `FileExplorer.test.tsx`, `OrganizeItems.test.tsx`, `EditorTabs.test.tsx`, `FilesRoles.test.tsx` |
| FR-035–FR-037 drafts | FE `drafts.test.ts`, `FileEditing.test.tsx`, `ConflictBar.test.tsx` (stale draft) |
| FR-038 S01/S02 unchanged | Every 001/002 test still passes (part of 559 + 179); IT `ProjectFilesMigrationTests.Upgrading_from_002_*`; E2E upgrade |

## Non-functional requirements and success criteria

| Item | Evidence |
|------|----------|
| NFR-001 no file content in logs | IT `Security/FileLogScanTests`; E2E P17 |
| NFR-002 role matrix | IT `RoleMatrixTests` (14 operations × 5 callers) |
| NFR-003 concurrency and timing tests | IT `ContentConcurrencyTests`, `StructureConcurrencyTests`; FE `autosave.test.ts` |
| NFR-004 no new infrastructure | ADR 0003; only two tables in the existing database |
| SC-001 three levels + typing in < 1 min | **Manual, not run** (no browser in this session); the flow is covered by `FileExplorer.test.tsx` |
| SC-002 "Saved" survives reload | FE `FileEditing.test.tsx`; E2E P5 |
| SC-003 ≤ 7 saves/60 s, saved ≤ 2 s after stopping | FE `autosave.test.ts` |
| SC-004 exactly one winner per race | IT `ContentConcurrencyTests`; E2E P16 |
| SC-005 Viewer, non-member, anonymous and Editor-delete refused | IT `RoleMatrixTests`, `PrivilegeEscalationTests`, `DeleteItemTests`; E2E P13–P15 |
| SC-006 invalid input refused with a specific error | IT `CreateItemTests`, `PathSecurityTests`, `ContentTests`, `RenameMoveTests`; E2E P7–P11 |
| SC-007 001/002 regression | Full suites green |
| SC-008 1 MiB file stays responsive | **Manual, not run** |
| SC-009 reload during the delay → saved or offered | FE `FileEditing.test.tsx`, `drafts.test.ts` |
