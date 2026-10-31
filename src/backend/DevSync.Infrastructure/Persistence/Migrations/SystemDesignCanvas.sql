START TRANSACTION;
CREATE TABLE diagrams (
    id uuid NOT NULL,
    workspace_id uuid NOT NULL,
    name character varying(100) NOT NULL,
    normalized_name character varying(100) NOT NULL,
    revision bigint NOT NULL,
    schema_version integer NOT NULL,
    document jsonb NOT NULL,
    node_count integer NOT NULL,
    edge_count integer NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by_user_id uuid,
    updated_by_user_id uuid,
    CONSTRAINT pk_diagrams PRIMARY KEY (id),
    CONSTRAINT ck_diagrams_document CHECK (jsonb_typeof(document) = 'object'),
    CONSTRAINT ck_diagrams_edge_count CHECK (edge_count BETWEEN 0 AND 1000),
    CONSTRAINT ck_diagrams_name CHECK (char_length(btrim(name)) BETWEEN 1 AND 100),
    CONSTRAINT ck_diagrams_node_count CHECK (node_count BETWEEN 0 AND 500),
    CONSTRAINT ck_diagrams_revision CHECK (revision >= 1),
    CONSTRAINT ck_diagrams_schema_version CHECK (schema_version >= 1),
    CONSTRAINT fk_diagrams_created_by_user_id_users FOREIGN KEY (created_by_user_id) REFERENCES users (id) ON DELETE SET NULL,
    CONSTRAINT fk_diagrams_updated_by_user_id_users FOREIGN KEY (updated_by_user_id) REFERENCES users (id) ON DELETE SET NULL,
    CONSTRAINT fk_diagrams_workspace_id_workspaces FOREIGN KEY (workspace_id) REFERENCES workspaces (id) ON DELETE CASCADE
);

CREATE INDEX ix_diagrams_created_by_user_id ON diagrams (created_by_user_id);

CREATE INDEX ix_diagrams_updated_by_user_id ON diagrams (updated_by_user_id);

CREATE UNIQUE INDEX ux_diagrams_workspace_name ON diagrams (workspace_id, normalized_name);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261031081026_SystemDesignCanvas', '10.0.14');

COMMIT;

