// Types mirroring specs/002-identity-and-permissions/contracts/openapi.yaml.

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
  | 'too_many_invitations';

export interface Problem {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  code: ErrorCode;
  correlationId: string;
  errors?: Record<string, string[]>;
}
