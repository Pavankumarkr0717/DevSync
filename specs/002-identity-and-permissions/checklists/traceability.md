# Traceability: Identity and Workspace Permissions

Maps acceptance scenarios, requirements and success criteria to the tests that verify them
(T079). `IT` = `tests/backend/DevSync.IntegrationTests/`, `UT` = `tests/backend/DevSync.UnitTests/`,
`FE` = `tests/frontend/`, `E2E` = [verification.md](../verification.md).

## User stories

| Scenario | Tests |
|----------|-------|
| US1-AS1 register → signed in | IT `Auth/RegisterTests.Registration_creates_the_account_and_signs_in`; FE `features/auth/RegisterPage.test.tsx` (signs in) |
| US1-AS2 sign in, any email case | IT `Auth/LoginTests.Correct_credentials_sign_in_with_any_email_case`; FE `LoginPage.test.tsx` |
| US1-AS3/AS4 wrong password = unknown email | IT `LoginTests.Wrong_password_and_unknown_email_are_indistinguishable`; UT `AuthHandlerTests.Wrong_password_and_unknown_email_*`; FE LoginPage generic message; E2E A2 |
| US1-AS5 invalid registration | IT `RegisterTests.Invalid_registrations_are_rejected_field_by_field`; UT `AuthHandlerTests.Register_rejects_invalid_input_*`; FE RegisterPage client checks |
| US1-AS6 duplicate registration | IT `RegisterTests.Duplicate_registration_*`, `Concurrent_registrations_*`; FE RegisterPage `email_taken`; E2E B3 |
| US1-AS7 redirect + return | FE `app/AppShell.test.tsx` (signed-out redirect), `LoginPage.test.tsx` (returns to requested page, open-redirect rejection) |
| US2-AS1 silent renewal | IT `Sessions/RefreshTests.Refresh_rotates_*`; FE `shared/api/baseQueryWithReauth.test.ts` |
| US2-AS2 profile | IT `Auth/ProfileTests`; FE `ProfilePage.test.tsx` |
| US2-AS3 sign out immediate | IT `Sessions/LogoutTests.Logout_ends_the_session_immediately_*`; FE ProfilePage; E2E A5 |
| US2-AS4 sign out everywhere | IT `LogoutTests.Logout_all_ends_every_session_*`; FE ProfilePage; E2E B12 |
| US2-AS5 reuse ends session | IT `RefreshTests.Replaying_a_used_token_later_*`; UT `AuthHandlerTests.A_token_reused_later_*`; E2E A4 |
| US2-AS6 session limit | IT `Sessions/SessionExpiryTests`; UT `Domain/SessionTests` |
| US2-AS7 expired/malformed token | IT `Auth/TokenValidationTests` |
| US3-AS1 creator is owner | IT `Workspaces/OwnershipTests.The_creator_is_recorded_as_owner`; UT `CreateWorkspaceHandlerTests` |
| US3-AS2 list all roles | IT `OwnershipTests.The_list_shows_every_workspace_*` |
| US3-AS3 non-member 404 | IT `OwnershipTests.Another_account_gets_not_found_*`, `Authorization/RoleMatrixTests` (NonMember) |
| US3-AS4 001 data handled | IT `Persistence/IdentityMigrationTests`; E2E upgrade log |
| US4-AS1 add via invitation | IT `Invitations/InvitationTests.The_invitee_sees_accepts_*`; FE `InvitationsPage.test.tsx`, `InvitationsPanel.test.tsx` |
| US4-AS2 role change next request | IT `Members/MemberManagementTests.Owner_downgrades_*`, `Owner_upgrades_*`; FE `MembersPanel.test.tsx` |
| US4-AS3 removal | IT `MemberManagementTests.Owner_removes_a_member_*`; FE MembersPanel |
| US4-AS4 non-owner refused | IT `RoleMatrixTests`, `Authorization/PrivilegeEscalationTests.An_editor_cannot_manage_*` |
| US4-AS5 member list, emails owner-only | IT `OwnershipTests.Only_the_owner_sees_member_emails`; FE MembersPanel |
| US4-AS6 owner self-change / duplicate | IT `MemberManagementTests.The_owner_cannot_change_or_remove_themselves`, `InvitationTests.Invitation_creation_rules_*` |
| US5-AS1 owner/editor edit | IT `Workspaces/UpdateWorkspaceTests`; FE `EditWorkspace.test.tsx` |
| US5-AS2 viewer refused | IT `PrivilegeEscalationTests.A_viewer_cannot_*`, RoleMatrix; E2E A7 |
| US5-AS3 validation | IT `UpdateWorkspaceTests.Invalid_values_*`; UT `Domain/WorkspaceUpdateTests` |
| US5-AS4 hidden controls + server check | FE `EditWorkspace.test.tsx` (viewer no Edit; stale role → toast) + IT RoleMatrix |

## Functional requirements

| FR | Tests |
|----|-------|
| FR-001–003 account rules | UT `AuthHandlerTests` (names, emails, password 11/12/128/129, = email, untrimmed); IT `RegisterTests` |
| FR-004 password hashing | IT `Security/PasswordStorageTests`, `Security/SecretExposureTests` |
| FR-005 indistinguishable sign-in | IT `LoginTests`; UT `AuthHandlerTests` (verify called for unknown email) |
| FR-006 duplicate rejected | IT `RegisterTests` |
| FR-007 registration signs in | IT `RegisterTests` |
| FR-008 throttling | IT `LoginTests.Five_failures_*` (existing and unknown), `One_client_is_limited_*` |
| FR-009–011 session model | UT `Domain/SessionTests`; IT `SessionExpiryTests`, `RefreshTests` |
| FR-012 reuse detection | IT `RefreshTests`; UT `AuthHandlerTests` |
| FR-013 concurrent renewal | IT `RefreshTests.Parallel_refreshes_*`, `Replaying_*_within_10_seconds_*`; FE baseQueryWithReauth (single-flight, 409 retry) |
| FR-014 refresh token hashed | IT `SecretExposureTests`; UT `AuthHandlerTests.Register_never_trims_*` (hash stored) |
| FR-015 not script-readable, no web storage | IT `RegisterTests` cookie attributes; FE `SessionBootstrap.test.tsx` storage spy |
| FR-016 CSRF | IT `LoginTests.*client_header*`, `*foreign_origin*`, `RefreshTests.*client_header*`, `RegisterTests.*client_header*` |
| FR-017/018 logout immediacy | IT `LogoutTests`, `TokenValidationTests.A_token_whose_session_was_revoked_*` |
| FR-019 profile | IT `ProfileTests` |
| FR-020 invitations | UT `Domain/InvitationTests`, `Application/InviteHandlerTests`; IT `InvitationTests` |
| FR-021–024 roles and owner invariant | UT `Domain/WorkspaceMembershipRulesTests`; IT `IdentityMigrationTests` (one-owner index), `MemberManagementTests` |
| FR-025 fresh role per request | IT `PrivilegeEscalationTests.A_downgrade_*`, `Extra_role_claims_*` |
| FR-026 all operations authenticated | IT `TokenValidationTests`, RoleMatrix (Anonymous), `CorrelationIdTests` (unknown route 401) |
| FR-027 permission table | UT `WorkspacePermissionsTests`; IT `RoleMatrixTests` |
| FR-028 distinct error codes | IT `TokenValidationTests` (unauthenticated/token_expired/session_ended), RoleMatrix (403/404), validation tests |
| FR-029 dev identity removed | IT `TokenValidationTests.The_development_identity_no_longer_exists` |
| FR-030 001 data deleted | IT `IdentityMigrationTests`; E2E upgrade |
| FR-031 001 behaviour kept | All ported 001 IT tests; FE workspace tests |
| FR-032–036 web app | FE `features/auth/*`, `features/workspaces/*`, `features/invitations/*`, `app/AppShell.test.tsx` |

## Non-functional requirements and success criteria

| Item | Evidence |
|------|----------|
| NFR-001 no secrets in logs/responses | IT `SecretExposureTests`, `Observability/LoggingTests`; E2E log checks |
| NFR-002 security events | IT `Security/SecurityEventTests` |
| NFR-003 full role matrix tested | IT `RoleMatrixTests` (7 operations × 5 callers) |
| NFR-004 signing key required | IT `Auth/SigningKeyStartupTests` |
| NFR-005 no new infrastructure | Plan constitution check; only PostgreSQL and in-process services |
| SC-001 register < 1 min / sign in < 15 s | Not timed; single-form flows (FE tests); manual browser pass recommended |
| SC-002 role matrix 100% | IT `RoleMatrixTests` all pass |
| SC-003 revoked/expired credentials rejected | IT `LogoutTests`, `RefreshTests`, `SessionExpiryTests`, `TokenValidationTests` |
| SC-004 identical failures | IT `LoginTests`; E2E A2 |
| SC-005 no plaintext secrets | IT `SecretExposureTests` |
| SC-006 silent renewal, idle expiry | FE baseQueryWithReauth + `useSessionKeepAlive`; IT `SessionExpiryTests` |
| SC-007 next-request role effect | IT `PrivilegeEscalationTests`, `MemberManagementTests` |
| SC-008 001 regression | Ported 001 IT suite passes (part of 301) |
