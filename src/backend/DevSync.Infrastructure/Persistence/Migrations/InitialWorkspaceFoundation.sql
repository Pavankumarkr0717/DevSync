CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029180535_InitialWorkspaceFoundation') THEN
    CREATE TABLE users (
        id uuid NOT NULL,
        display_name character varying(100) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_users PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029180535_InitialWorkspaceFoundation') THEN
    CREATE TABLE workspaces (
        id uuid NOT NULL,
        name character varying(100) NOT NULL,
        description character varying(500),
        owner_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_workspaces PRIMARY KEY (id),
        CONSTRAINT ck_workspaces_name_not_blank CHECK (char_length(btrim(name)) > 0),
        CONSTRAINT fk_workspaces_owner_id_users FOREIGN KEY (owner_id) REFERENCES users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029180535_InitialWorkspaceFoundation') THEN
    CREATE TABLE workspace_memberships (
        workspace_id uuid NOT NULL,
        user_id uuid NOT NULL,
        role character varying(20) NOT NULL,
        joined_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_workspace_memberships PRIMARY KEY (workspace_id, user_id),
        CONSTRAINT ck_workspace_memberships_role CHECK (role IN ('owner')),
        CONSTRAINT fk_workspace_memberships_user_id_users FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_workspace_memberships_workspace_id_workspaces FOREIGN KEY (workspace_id) REFERENCES workspaces (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029180535_InitialWorkspaceFoundation') THEN
    CREATE INDEX ix_workspace_memberships_user_id ON workspace_memberships (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029180535_InitialWorkspaceFoundation') THEN
    CREATE INDEX ix_workspaces_owner_id ON workspaces (owner_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029180535_InitialWorkspaceFoundation') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261029180535_InitialWorkspaceFoundation', '10.0.14');
    END IF;
END $EF$;
COMMIT;

