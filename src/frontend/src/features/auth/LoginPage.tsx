import { useState, type FormEvent } from 'react';
import { Link, Navigate, useSearchParams } from 'react-router';
import { useAppSelector } from '../../app/hooks';
import { toApiError } from '../../shared/api/problem';
import { ErrorMessage } from '../../shared/ui/ErrorMessage';
import { useLoginMutation } from './authApi';
import { selectAuth } from './authSlice';
import { safeReturnTo } from './returnTo';

export const INVALID_CREDENTIALS_TEXT = 'Email or password is incorrect.';
export const TOO_MANY_ATTEMPTS_TEXT = 'Too many attempts. Try again in a few minutes.';

export function LoginPage() {
  const { status, notice } = useAppSelector(selectAuth);
  const [params] = useSearchParams();
  const returnTo = safeReturnTo(params.get('returnTo'));
  const [login, { isLoading }] = useLoginMutation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [fieldErrors, setFieldErrors] = useState<{ email?: string; password?: string }>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [requestError, setRequestError] = useState<unknown>(null);

  if (status === 'signedIn') {
    return <Navigate to={returnTo} replace />;
  }

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);
    setRequestError(null);

    const errors = {
      email: email.trim() ? undefined : 'Email is required.',
      password: password ? undefined : 'Password is required.',
    };
    setFieldErrors(errors);
    if (errors.email || errors.password) {
      return;
    }

    try {
      await login({ email, password }).unwrap();
    } catch (error) {
      const apiError = toApiError(error);
      if (apiError.code === 'invalid_credentials') {
        setFormError(INVALID_CREDENTIALS_TEXT);
      } else if (apiError.code === 'too_many_attempts') {
        setFormError(TOO_MANY_ATTEMPTS_TEXT);
      } else if (apiError.code === 'validation_failed') {
        setFieldErrors({ email: apiError.fieldErrors.email?.[0], password: apiError.fieldErrors.password?.[0] });
      } else {
        setRequestError(error);
      }
    }
  };

  return (
    <section className="auth-page">
      <h1>Sign in</h1>
      {notice && (
        <p role="status" className="notice">
          {notice}
        </p>
      )}
      <form onSubmit={onSubmit} noValidate>
        <div className="field">
          <label htmlFor="login-email">Email</label>
          <input
            id="login-email"
            type="email"
            autoComplete="username"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            aria-invalid={Boolean(fieldErrors.email)}
          />
          {fieldErrors.email && <span className="field-error">{fieldErrors.email}</span>}
        </div>
        <div className="field">
          <label htmlFor="login-password">Password</label>
          <input
            id="login-password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            aria-invalid={Boolean(fieldErrors.password)}
          />
          {fieldErrors.password && <span className="field-error">{fieldErrors.password}</span>}
        </div>
        {formError && (
          <p role="alert" className="error">
            {formError}
          </p>
        )}
        {requestError !== null && <ErrorMessage error={requestError} />}
        <button type="submit" disabled={isLoading}>
          {isLoading ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
      <p>
        New to DevSync? <Link to="/register">Create an account</Link>
      </p>
    </section>
  );
}
