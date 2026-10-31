import { useState } from 'react';
import { useNavigate } from 'react-router';
import { formatDate } from '../../shared/text';
import { ErrorMessage } from '../../shared/ui/ErrorMessage';
import { Loading } from '../../shared/ui/Loading';
import { useAcceptInvitationMutation, useDeclineInvitationMutation, useListMyInvitationsQuery } from './invitationsApi';

/** Invitations addressed to the signed-in account (spec 002 FR-020). */
export function InvitationsPage() {
  const navigate = useNavigate();
  const { data: invitations, isLoading, error } = useListMyInvitationsQuery();
  const [accept] = useAcceptInvitationMutation();
  const [decline] = useDeclineInvitationMutation();
  const [actionError, setActionError] = useState<unknown>(null);

  const onAccept = async (id: string) => {
    setActionError(null);
    try {
      const joined = await accept(id).unwrap();
      navigate(`/workspaces/${joined.id}`);
    } catch (e) {
      setActionError(e);
    }
  };

  const onDecline = async (id: string) => {
    setActionError(null);
    try {
      await decline(id).unwrap();
    } catch (e) {
      setActionError(e);
    }
  };

  return (
    <section>
      <h1>Invitations</h1>
      {isLoading && <Loading />}
      {error && <ErrorMessage error={error} />}
      {actionError !== null && <ErrorMessage error={actionError} />}
      {invitations && invitations.length === 0 && <p>No pending invitations.</p>}
      {invitations && invitations.length > 0 && (
        <ul className="workspace-list" aria-label="Pending invitations">
          {invitations.map((invitation) => (
            <li key={invitation.id}>
              <strong>{invitation.workspace.name}</strong> as {invitation.role}
              <div className="meta">
                from {invitation.invitedBy.displayName} · expires {formatDate(invitation.expiresAt)}
              </div>
              <button type="button" onClick={() => void onAccept(invitation.id)}>
                Accept
              </button>{' '}
              <button type="button" onClick={() => void onDecline(invitation.id)}>
                Decline
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
