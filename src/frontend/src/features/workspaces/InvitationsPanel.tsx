import { useState, type FormEvent } from 'react';
import { toApiError } from '../../shared/api/problem';
import type { AssignableRole } from '../../shared/api/types';
import { formatDate } from '../../shared/text';
import { ErrorMessage } from '../../shared/ui/ErrorMessage';
import { Loading } from '../../shared/ui/Loading';
import { useInviteMemberMutation, useListWorkspaceInvitationsQuery, useRevokeInvitationMutation } from './workspacesApi';

/** Owner-only: invite by email and manage pending invitations (spec 002 FR-020). */
export function InvitationsPanel({ workspaceId }: { workspaceId: string }) {
  const { data: invitations, isLoading, error: listError } = useListWorkspaceInvitationsQuery(workspaceId);
  const [invite, { isLoading: inviting }] = useInviteMemberMutation();
  const [revoke] = useRevokeInvitationMutation();
  const [email, setEmail] = useState('');
  const [role, setRole] = useState<AssignableRole>('editor');
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [requestError, setRequestError] = useState<unknown>(null);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFieldError(null);
    setFormError(null);
    setRequestError(null);
    if (!email.trim()) {
      setFieldError('Email is required.');
      return;
    }

    try {
      await invite({ workspaceId, email, role }).unwrap();
      setEmail('');
    } catch (error) {
      const apiError = toApiError(error);
      if (apiError.code === 'validation_failed') {
        setFieldError(apiError.fieldErrors.email?.[0] ?? apiError.fieldErrors.role?.[0] ?? apiError.title);
      } else if (apiError.status === 400 || apiError.status === 409) {
        setFormError(apiError.title);
      } else {
        setRequestError(error);
      }
    }
  };

  return (
    <section aria-labelledby="invitations-heading" className="panel">
      <h2 id="invitations-heading">Invitations</h2>
      <form onSubmit={onSubmit} noValidate aria-label="Invite a member" className="inline-form">
        <div className="field">
          <label htmlFor="invite-email">Email</label>
          <input id="invite-email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} aria-invalid={Boolean(fieldError)} />
          {fieldError && <span className="field-error">{fieldError}</span>}
        </div>
        <div className="field">
          <label htmlFor="invite-role">Role</label>
          <select id="invite-role" value={role} onChange={(e) => setRole(e.target.value as AssignableRole)}>
            <option value="editor">editor</option>
            <option value="viewer">viewer</option>
          </select>
        </div>
        <button type="submit" disabled={inviting}>
          {inviting ? 'Inviting…' : 'Invite'}
        </button>
      </form>
      {formError && (
        <p role="alert" className="error">
          {formError}
        </p>
      )}
      {requestError !== null && <ErrorMessage error={requestError} />}

      {isLoading && <Loading />}
      {listError && <ErrorMessage error={listError} />}
      {invitations && invitations.length === 0 && <p className="meta">No pending invitations.</p>}
      {invitations && invitations.length > 0 && (
        <ul aria-label="Pending invitations">
          {invitations.map((invitation) => (
            <li key={invitation.id}>
              {invitation.email} — {invitation.role} <span className="meta">(expires {formatDate(invitation.expiresAt)})</span>{' '}
              <button
                type="button"
                onClick={() => void revoke({ workspaceId, invitationId: invitation.id })}
                aria-label={`Revoke invitation for ${invitation.email}`}
              >
                Revoke
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
