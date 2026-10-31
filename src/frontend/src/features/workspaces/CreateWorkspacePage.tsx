import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import { toApiError } from '../../shared/api/problem';
import { ErrorMessage } from '../../shared/ui/ErrorMessage';
import { validateWorkspace as validate, type WorkspaceFieldErrors as FieldErrors } from './workspaceValidation';
import { useCreateWorkspaceMutation } from './workspacesApi';

export function CreateWorkspacePage() {
  const navigate = useNavigate();
  const [createWorkspace, { isLoading }] = useCreateWorkspaceMutation();
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [requestError, setRequestError] = useState<unknown>(null);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setRequestError(null);

    const errors = validate(name, description);
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      return;
    }

    try {
      const created = await createWorkspace({ name, description: description.trim() || null }).unwrap();
      navigate(`/workspaces/${created.id}`);
    } catch (error) {
      const apiError = toApiError(error);
      if (apiError.code === 'validation_failed') {
        setFieldErrors({
          name: apiError.fieldErrors.name?.[0],
          description: apiError.fieldErrors.description?.[0],
        });
      } else {
        setRequestError(error);
      }
    }
  };

  return (
    <section>
      <h1>New workspace</h1>
      <form onSubmit={onSubmit} noValidate>
        <div className="field">
          <label htmlFor="workspace-name">Name</label>
          <input
            id="workspace-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            aria-invalid={Boolean(fieldErrors.name)}
            aria-describedby={fieldErrors.name ? 'name-error' : undefined}
          />
          {fieldErrors.name && (
            <span id="name-error" className="field-error">
              {fieldErrors.name}
            </span>
          )}
        </div>
        <div className="field">
          <label htmlFor="workspace-description">Description (optional)</label>
          <textarea
            id="workspace-description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={4}
            aria-invalid={Boolean(fieldErrors.description)}
            aria-describedby={fieldErrors.description ? 'description-error' : undefined}
          />
          {fieldErrors.description && (
            <span id="description-error" className="field-error">
              {fieldErrors.description}
            </span>
          )}
        </div>
        {requestError !== null && <ErrorMessage error={requestError} />}
        <button type="submit" disabled={isLoading}>
          {isLoading ? 'Creating…' : 'Create workspace'}
        </button>{' '}
        <Link to="/workspaces">Cancel</Link>
      </form>
    </section>
  );
}
