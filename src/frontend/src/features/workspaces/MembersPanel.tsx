import { useState } from 'react';
import type { AssignableRole, WorkspaceDetails } from '../../shared/api/types';
import { ErrorMessage } from '../../shared/ui/ErrorMessage';
import { useChangeMemberRoleMutation, useRemoveMemberMutation } from './workspacesApi';

/** Members and roles; the Owner can change roles and remove members (spec 002 US4, FR-035). */
export function MembersPanel({ workspace }: { workspace: WorkspaceDetails }) {
  const isOwner = workspace.myRole === 'owner';
  const [changeRole] = useChangeMemberRoleMutation();
  const [removeMember] = useRemoveMemberMutation();
  const [error, setError] = useState<unknown>(null);

  const onRoleChange = async (userId: string, role: AssignableRole) => {
    setError(null);
    try {
      await changeRole({ workspaceId: workspace.id, userId, role }).unwrap();
    } catch (e) {
      setError(e);
    }
  };

  const onRemove = async (userId: string, name: string) => {
    if (!window.confirm(`Remove ${name} from ${workspace.name}?`)) {
      return;
    }
    setError(null);
    try {
      await removeMember({ workspaceId: workspace.id, userId }).unwrap();
    } catch (e) {
      setError(e);
    }
  };

  return (
    <section aria-labelledby="members-heading">
      <h2 id="members-heading">Members</h2>
      {error !== null && <ErrorMessage error={error} />}
      <ul aria-label="Members" className="member-list">
        {workspace.members.map((member) => (
          <li key={member.userId}>
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
                <button type="button" onClick={() => void onRemove(member.userId, member.displayName)}>
                  Remove
                </button>
              </>
            ) : (
              <span className="role">— {member.role}</span>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
