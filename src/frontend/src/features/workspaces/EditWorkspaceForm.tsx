import { useState, type FormEvent } from 'react';
import { toApiError } from '../../shared/api/problem';
import type { WorkspaceDetails } from '../../shared/api/types';
import { ErrorMessage } from '../../shared/ui/ErrorMessage';
import { validateWorkspace, type WorkspaceFieldErrors } from './workspaceValidation';
import { useUpdateWorkspaceMutation } from './workspacesApi';

/** Inline name/description editing for Owners and Editors (spec 002 US5). */
export function EditWorkspaceForm({ workspace, onDone }: { workspace: WorkspaceDetails; onDone: () => void }) {
  const [update, { isLoading }] = useUpdateWorkspaceMutation();
  const [name, setName] = useState(workspace.name);
  const [description, setDescription] = useState(workspace.description ?? '');
  const [fieldErrors, setFieldErrors] = useState<WorkspaceFieldErrors>({});
  const [requestError, setRequestError] = useState<unknown>(null);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setRequestError(null);
    const errors = validateWorkspace(name, description);
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      return;
    }

    try {
      await update({ id: workspace.id, name, description: description.trim() || null }).unwrap();
      onDone();
    } catch (error) {
      const apiError = toApiError(error);
      if (apiError.code === 'validation_failed') {
        setFieldErrors({ name: apiError.fieldErrors.name?.[0], description: apiError.fieldErrors.description?.[0] });
      } else if (apiError.status === 403) {
        onDone(); // the permission toast explains; the page refetches with the current role
      } else {
        setRequestError(error);
      }
    }
  };

  return (
    <form onSubmit={onSubmit} noValidate aria-label="Edit workspace">
      <div className="field">
        <label htmlFor="edit-name">Name</label>
        <input id="edit-name" value={name} onChange={(e) => setName(e.target.value)} aria-invalid={Boolean(fieldErrors.name)} />
        {fieldErrors.name && <span className="field-error">{fieldErrors.name}</span>}
      </div>
      <div className="field">
        <label htmlFor="edit-description">Description</label>
        <textarea
          id="edit-description"
          rows={3}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          aria-invalid={Boolean(fieldErrors.description)}
        />
        {fieldErrors.description && <span className="field-error">{fieldErrors.description}</span>}
      </div>
      {requestError !== null && <ErrorMessage error={requestError} />}
      <button type="submit" disabled={isLoading}>
        {isLoading ? 'Saving…' : 'Save'}
      </button>{' '}
      <button type="button" onClick={onDone}>
        Cancel
      </button>
    </form>
  );
}
