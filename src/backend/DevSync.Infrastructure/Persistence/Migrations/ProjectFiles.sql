START TRANSACTION;
CREATE TABLE workspace_items (
    id uuid NOT NULL,
    workspace_id uuid NOT NULL,
    parent_id uuid,
    kind character varying(8) NOT NULL,
    name character varying(255) NOT NULL,
    normalized_name character varying(255) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by_user_id uuid,
    CONSTRAINT pk_workspace_items PRIMARY KEY (id),
    CONSTRAINT ck_workspace_items_kind CHECK (kind IN ('folder','file')),
    CONSTRAINT ck_workspace_items_name_length CHECK (char_length(name) BETWEEN 1 AND 255),
    CONSTRAINT fk_workspace_items_created_by_user_id_users FOREIGN KEY (created_by_user_id) REFERENCES users (id) ON DELETE SET NULL,
    CONSTRAINT fk_workspace_items_parent_id_workspace_items FOREIGN KEY (parent_id) REFERENCES workspace_items (id) ON DELETE CASCADE,
    CONSTRAINT fk_workspace_items_workspace_id_workspaces FOREIGN KEY (workspace_id) REFERENCES workspaces (id) ON DELETE CASCADE
);

CREATE TABLE file_contents (
    item_id uuid NOT NULL,
    content text NOT NULL DEFAULT '',
    size_bytes integer NOT NULL,
    revision bigint NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    updated_by_user_id uuid,
    CONSTRAINT pk_file_contents PRIMARY KEY (item_id),
    CONSTRAINT ck_file_contents_revision CHECK (revision >= 1),
    CONSTRAINT ck_file_contents_size CHECK (size_bytes BETWEEN 0 AND 1048576 AND size_bytes = octet_length(content)),
    CONSTRAINT fk_file_contents_item_id_workspace_items FOREIGN KEY (item_id) REFERENCES workspace_items (id) ON DELETE CASCADE,
    CONSTRAINT fk_file_contents_updated_by_user_id_users FOREIGN KEY (updated_by_user_id) REFERENCES users (id) ON DELETE SET NULL
);

CREATE INDEX ix_file_contents_updated_by_user_id ON file_contents (updated_by_user_id);

CREATE INDEX ix_workspace_items_created_by_user_id ON workspace_items (created_by_user_id);

CREATE INDEX ix_workspace_items_parent_id ON workspace_items (parent_id);

CREATE UNIQUE INDEX ux_workspace_items_sibling_name ON workspace_items (workspace_id, parent_id, normalized_name) NULLS NOT DISTINCT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261031052604_ProjectFiles', '10.0.14');

COMMIT;

