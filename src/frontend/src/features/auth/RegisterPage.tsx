import { useState, type FormEvent } from 'react';
import { Link, Navigate } from 'react-router';
import { useAppSelector } from '../../app/hooks';
import { toApiError } from '../../shared/api/problem';
import { codePointLength } from '../../shared/text';
import { ErrorMessage } from '../../shared/ui/ErrorMessage';
import { useRegisterMutation } from './authApi';
import { selectAuthStatus } from './authSlice';
import { DEFAULT_ROUTE } from './returnTo';

export const PASSWORD_MIN = 12;
export const PASSWORD_MAX = 128;
export const EMAIL_TAKEN_TEXT = 'An account with this email already exists.';

type Field = 'displayName' | 'email' | 'password';
type FieldErrors = Partial<Record<Field, string>>;

/** Mirrors the server rules (spec 002 FR-001–FR-003); the server remains authoritative. */
function validate(displayName: string, email: string, password: string): FieldErrors {
  const errors: FieldErrors = {};
  const name = displayName.trim();
  if (!name) {
    errors.displayName = 'Name is required.';
  } else if (codePointLength(name) > 100) {
    errors.displayName = 'Name must be at most 100 characters.';
  }

  const trimmedEmail = email.trim();
  if (!trimmedEmail) {
    errors.email = 'Email is required.';
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmedEmail) || trimmedEmail.length > 254) {
    errors.email = 'Enter a valid email address.';
  }

  const length = codePointLength(password);
  if (length < PASSWORD_MIN) {
    errors.password = `Password must be at least ${PASSWORD_MIN} characters.`;
  } else if (length > PASSWORD_MAX) {
    errors.password = `Password must be at most ${PASSWORD_MAX} characters.`;
  } else if (password.toLowerCase() === trimmedEmail.toLowerCase()) {
    errors.password = 'Password must not be your email address.';
  }
  return errors;
}

export function RegisterPage() {
  const status = useAppSelector(selectAuthStatus);
  const [register, { isLoading }] = useRegisterMutation();
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [requestError, setRequestError] = useState<unknown>(null);

  if (status === 'signedIn') {
    return <Navigate to={DEFAULT_ROUTE} replace />;
  }

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setRequestError(null);
    const errors = validate(displayName, email, password);
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      return;
    }

    try {
      await register({ displayName, email, password }).unwrap();
    } catch (error) {
      const apiError = toApiError(error);
      if (apiError.code === 'email_taken') {
        setFieldErrors({ email: EMAIL_TAKEN_TEXT });
      } else if (apiError.code === 'validation_failed') {
        setFieldErrors({
          displayName: apiError.fieldErrors.displayName?.[0],
          email: apiError.fieldErrors.email?.[0],
          password: apiError.fieldErrors.password?.[0],
        });
      } else {
        setRequestError(error);
      }
    }
  };

  const field = (id: Field, label: string, type: string, value: string, set: (v: string) => void, autoComplete: string, hint?: string) => (
    <div className="field">
      <label htmlFor={`register-${id}`}>{label}</label>
      <input
        id={`register-${id}`}
        type={type}
        autoComplete={autoComplete}
        value={value}
        onChange={(e) => set(e.target.value)}
        aria-invalid={Boolean(fieldErrors[id])}
        aria-describedby={hint ? `register-${id}-hint` : undefined}
      />
      {hint && (
        <span id={`register-${id}-hint`} className="meta">
          {hint}
        </span>
      )}
      {fieldErrors[id] && <span className="field-error">{fieldErrors[id]}</span>}
    </div>
  );

  return (
    <section className="auth-page">
      <h1>Create your account</h1>
      <form onSubmit={onSubmit} noValidate>
        {field('displayName', 'Name', 'text', displayName, setDisplayName, 'name')}
        {field('email', 'Email', 'email', email, setEmail, 'email')}
        {field('password', 'Password', 'password', password, setPassword, 'new-password', `${PASSWORD_MIN}–${PASSWORD_MAX} characters`)}
        {requestError !== null && <ErrorMessage error={requestError} />}
        <button type="submit" disabled={isLoading}>
          {isLoading ? 'Creating account…' : 'Create account'}
        </button>
      </form>
      <p>
        Already have an account? <Link to="/login">Sign in</Link>
      </p>
    </section>
  );
}
