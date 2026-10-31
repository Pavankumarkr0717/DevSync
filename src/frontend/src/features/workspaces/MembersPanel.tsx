import { useState } from 'react';
import type { AssignableRole, WorkspaceDetails } from '../../shared/api/types';
import { Avatar } from '../../shared/ui/Avatar';
import { ConfirmDialog } from '../../shared/ui/ConfirmDialog';
import { ErrorMessage } from '../../shared/ui/ErrorMessage';
import { RoleBadge } from '../../shared/ui/RoleBadge';
import { useChangeMemberRoleMutation, useRemoveMemberMutation } from './workspacesApi';

/** Members and roles; the Owner can change roles and remove members (spec 002 US4, FR-035). */
export function MembersPanel({ workspace }: { workspace: WorkspaceDetails }) {
  const isOwner = workspace.myRole === 'owner';
  const [changeRole] = useChangeMemberRoleMutation();
  const [removeMember] = useRemoveMemberMutation();
  const [error, setError] = useState<unknown>(null);
  const [removing, setRemoving] = useState<{ userId: string; name: string } | null>(null);
  const [busy, setBusy] = useState(false);

  const onRoleChange = async (userId: string, role: AssignableRole) => {
    setError(null);
    try {
      await changeRole({ workspaceId: workspace.id, userId, role }).unwrap();
    } catch (e) {
      setError(e);
    }
  };

  const onRemove = async (userId: string) => {
    setError(null);
    setBusy(true);
    try {
      await removeMember({ workspaceId: workspace.id, userId }).unwrap();
      setRemoving(null);
    } catch (e) {
      setError(e);
      setRemoving(null);
    } finally {
      setBusy(false);
    }
  };

  return (
    <section aria-labelledby="members-heading" className="panel">
      <h2 id="members-heading">Members</h2>
      {error !== null && <ErrorMessage error={error} />}
      <ul aria-label="Members" className="member-list">
        {workspace.members.map((member) => (
          <li key={member.userId}>
            <Avatar id={member.userId} name={member.displayName} size="sm" />
            <span>{member.displayName}</span>
            {member.email && <span className="meta"> · {member.email}</span>}{' '}
            {isOwner && member.role !== 'owner' ? (
              <>
                <select
                  aria-label={`Role for ${member.displayName}`}
                  value={member.role}
                  onChange={(e) => void onRoleChange(member.userId, e.target.value as AssignableRole)}
                >
                  <option value="editor">editor</option>
                  <option value="viewer">viewer</option>
                </select>{' '}
                <button type="button" onClick={() => setRemoving({ userId: member.userId, name: member.displayName })}>
                  Remove
                </button>
              </>
            ) : (
              <span className="role">
                <span className="visually-hidden">— </span>
                <RoleBadge role={member.role} />
              </span>
            )}
          </li>
        ))}
      </ul>
      {removing && (
        <ConfirmDialog
          title={`Remove ${removing.name}?`}
          message={`Remove ${removing.name} from ${workspace.name}? They lose access to its files and diagrams.`}
          confirmLabel="Remove member"
          busy={busy}
          onConfirm={() => void onRemove(removing.userId)}
          onCancel={() => setRemoving(null)}
        />
      )}
    </section>
  );
}
