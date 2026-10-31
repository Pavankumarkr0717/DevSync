START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    DELETE FROM workspace_memberships;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    DELETE FROM workspaces;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    DELETE FROM users;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    ALTER TABLE workspace_memberships DROP CONSTRAINT ck_workspace_memberships_role;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    ALTER TABLE users ADD access_failed_count integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    ALTER TABLE users ADD concurrency_stamp text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    ALTER TABLE users ADD email character varying(254) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    ALTER TABLE users ADD email_confirmed boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    ALTER TABLE users ADD lockout_enabled boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    ALTER TABLE users ADD lockout_end timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    ALTER TABLE users ADD normalized_email character varying(254) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    ALTER TABLE users ADD normalized_user_name character varying(256) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    ALTER TABLE users ADD password_hash text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    ALTER TABLE users ADD security_stamp text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    ALTER TABLE users ADD user_name character varying(256) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    CREATE TABLE sessions (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        last_refreshed_at timestamp with time zone NOT NULL,
        idle_expires_at timestamp with time zone NOT NULL,
        absolute_expires_at timestamp with time zone NOT NULL,
        revoked_at timestamp with time zone,
        revoked_reason character varying(32),
        CONSTRAINT pk_sessions PRIMARY KEY (id),
        CONSTRAINT ck_sessions_revoked_reason CHECK (revoked_reason IN ('logout','logout_all','refresh_token_reuse')),
        CONSTRAINT fk_sessions_user_id_users FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    CREATE TABLE workspace_invitations (
        id uuid NOT NULL,
        workspace_id uuid NOT NULL,
        email character varying(254) NOT NULL,
        normalized_email character varying(254) NOT NULL,
        role character varying(20) NOT NULL,
        invited_by_user_id uuid NOT NULL,
        status character varying(16) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        expires_at timestamp with time zone NOT NULL,
        responded_at timestamp with time zone,
        accepted_user_id uuid,
        CONSTRAINT pk_workspace_invitations PRIMARY KEY (id),
        CONSTRAINT ck_workspace_invitations_expiry CHECK (expires_at > created_at),
        CONSTRAINT ck_workspace_invitations_role CHECK (role IN ('editor','viewer')),
        CONSTRAINT ck_workspace_invitations_status CHECK (status IN ('pending','accepted','declined','revoked','expired')),
        CONSTRAINT fk_workspace_invitations_accepted_user_id_users FOREIGN KEY (accepted_user_id) REFERENCES users (id) ON DELETE SET NULL,
        CONSTRAINT fk_workspace_invitations_invited_by_user_id_users FOREIGN KEY (invited_by_user_id) REFERENCES users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_workspace_invitations_workspace_id_workspaces FOREIGN KEY (workspace_id) REFERENCES workspaces (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    CREATE TABLE refresh_tokens (
        id uuid NOT NULL,
        session_id uuid NOT NULL,
        token_hash bytea NOT NULL,
        issued_at timestamp with time zone NOT NULL,
        used_at timestamp with time zone,
        replaced_by_id uuid,
        CONSTRAINT pk_refresh_tokens PRIMARY KEY (id),
        CONSTRAINT ck_refresh_tokens_token_hash_length CHECK (octet_length(token_hash) = 32),
        CONSTRAINT fk_refresh_tokens_replaced_by_id_refresh_tokens FOREIGN KEY (replaced_by_id) REFERENCES refresh_tokens (id) ON DELETE SET NULL,
        CONSTRAINT fk_refresh_tokens_session_id_sessions FOREIGN KEY (session_id) REFERENCES sessions (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    CREATE UNIQUE INDEX ux_workspace_memberships_one_owner ON workspace_memberships (workspace_id) WHERE role = 'owner';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    ALTER TABLE workspace_memberships ADD CONSTRAINT ck_workspace_memberships_role CHECK (role IN ('owner','editor','viewer'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    CREATE UNIQUE INDEX ux_users_normalized_email ON users (normalized_email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    CREATE UNIQUE INDEX ux_users_normalized_user_name ON users (normalized_user_name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    CREATE INDEX ix_refresh_tokens_replaced_by_id ON refresh_tokens (replaced_by_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    CREATE INDEX ix_refresh_tokens_session_id ON refresh_tokens (session_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    CREATE UNIQUE INDEX ux_refresh_tokens_token_hash ON refresh_tokens (token_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    CREATE INDEX ix_sessions_user_id_active ON sessions (user_id) WHERE revoked_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    CREATE INDEX ix_workspace_invitations_accepted_user_id ON workspace_invitations (accepted_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    CREATE INDEX ix_workspace_invitations_invited_by_user_id ON workspace_invitations (invited_by_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    CREATE INDEX ix_workspace_invitations_pending_email ON workspace_invitations (normalized_email) WHERE status = 'pending';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    CREATE INDEX ix_workspace_invitations_workspace_id ON workspace_invitations (workspace_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    CREATE UNIQUE INDEX ux_workspace_invitations_pending ON workspace_invitations (workspace_id, normalized_email) WHERE status = 'pending';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030030028_IdentityAndPermissions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030030028_IdentityAndPermissions', '10.0.14');
    END IF;
END $EF$;
COMMIT;

