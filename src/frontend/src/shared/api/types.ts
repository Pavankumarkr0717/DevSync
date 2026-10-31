// Types mirroring specs/002-identity-and-permissions/contracts/openapi.yaml and
// specs/003-file-explorer-editor/contracts/openapi.yaml.

export type WorkspaceRole = 'owner' | 'editor' | 'viewer';
export type AssignableRole = 'editor' | 'viewer';

export interface UserRef {
  id: string;
  displayName: string;
}

export interface UserProfile {
  id: string;
  displayName: string;
  email: string;
  createdAt: string;
}

export interface AuthSession {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: UserProfile;
}

export interface RegisterRequest {
  displayName: string;
  email: string;
  password: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface CreateWorkspaceRequest {
  name: string;
  description?: string | null;
}

export interface WorkspaceSummary {
  id: string;
  name: string;
  role: WorkspaceRole;
  createdAt: string;
}

export interface WorkspacePage {
  items: WorkspaceSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface Member {
  userId: string;
  displayName: string;
  role: WorkspaceRole;
  joinedAt: string;
  /** Present only when the caller is the workspace owner. */
  email?: string;
}

export interface WorkspaceDetails {
  id: string;
  name: string;
  description: string | null;
  owner: UserRef;
  createdAt: string;
  updatedAt: string;
  members: Member[];
  myRole: WorkspaceRole;
}

export interface WorkspaceInvitation {
  id: string;
  email: string;
  role: AssignableRole;
  invitedBy: UserRef;
  createdAt: string;
  expiresAt: string;
}

export interface ReceivedInvitation {
  id: string;
  workspace: { id: string; name: string };
  role: AssignableRole;
  invitedBy: UserRef;
  expiresAt: string;
}

export type ErrorCode =
  | 'validation_failed'
  | 'malformed_request'
  | 'unauthenticated'
  | 'not_found'
  | 'service_unavailable'
  | 'internal_error'
  | 'token_expired'
  | 'session_ended'
  | 'invalid_credentials'
  | 'email_taken'
  | 'too_many_attempts'
  | 'csrf_check_failed'
  | 'refresh_conflict'
  | 'forbidden'
  | 'cannot_change_owner'
  | 'cannot_remove_owner'
  | 'member_not_found'
  | 'already_member'
  | 'cannot_invite_self'
  | 'invitation_exists'
  | 'too_many_invitations'
  | 'item_not_found'
  | 'parent_not_found'
  | 'invalid_name'
  | 'unsupported_extension'
  | 'too_deep'
  | 'path_too_long'
  | 'invalid_move'
  | 'invalid_content'
  | 'name_taken'
  | 'too_many_items'
  | 'revision_conflict'
  | 'content_too_large'
  | 'unsupported_media_type'
  | 'revision_required'
  | 'diagram_not_found'
  | 'diagram_name_taken'
  | 'too_many_diagrams'
  | 'invalid_diagram'
  | 'unsupported_schema_version'
  | 'stored_version_unsupported';

export interface Problem {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  code: ErrorCode;
  correlationId: string;
  errors?: Record<string, string[]>;
  /** Present on revision_conflict (412): the file's revision now. */
  currentRevision?: number;
  /** Present on invalid_diagram (400): every rule the document breaks. */
  problems?: DiagramProblem[];
}

// ---------- Diagrams (specs/004-system-design-canvas/contracts) ----------

export type ComponentType =
  | 'client'
  | 'api_gateway'
  | 'microservice'
  | 'database'
  | 'cache'
  | 'message_queue'
  | 'load_balancer'
  | 'external_service';

export interface MetadataEntry {
  key: string;
  value: string;
}

export interface DiagramNodeDoc {
  id: string;
  type: ComponentType;
  title: string;
  description: string;
  metadata: MetadataEntry[];
  position: { x: number; y: number };
  size: { width: number; height: number };
}

export interface DiagramEdgeDoc {
  id: string;
  source: string;
  target: string;
  label: string;
}

export interface Viewport {
  x: number;
  y: number;
  zoom: number;
}

export interface DiagramDocument {
  schemaVersion: 1;
  nodes: DiagramNodeDoc[];
  edges: DiagramEdgeDoc[];
  viewport: Viewport | null;
}

export interface DiagramSummary {
  id: string;
  name: string;
  revision: number;
  nodeCount: number;
  edgeCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface Diagram extends DiagramSummary {
  /** null when this release can't read the stored format (see unsupportedSchemaVersion). */
  document: DiagramDocument | null;
  /** The stored format version when it is newer than this release (0 = unreadable). */
  unsupportedSchemaVersion: number | null;
}

export interface SaveDiagramResult {
  revision: number;
  nodeCount: number;
  edgeCount: number;
  updatedAt: string;
}

export interface DiagramProblem {
  code: string;
  path: string;
  message: string;
}

export type ItemKind = 'folder' | 'file';

export interface Item {
  id: string;
  /** null = top level. */
  parentId: string | null;
  kind: ItemKind;
  name: string;
  createdAt: string;
  updatedAt: string;
}

export interface TreeLimits {
  maxNameLength: number;
  maxPathLength: number;
  maxDepth: number;
  maxItems: number;
  maxContentBytes: number;
}

export interface Tree {
  items: Item[];
  limits: TreeLimits;
}

/** A file's content as stored, with its revision (from the ETag). */
export interface FileText {
  text: string;
  revision: number;
}

export interface SaveResult {
  revision: number;
  sizeBytes: number;
  updatedAt: string;
}
