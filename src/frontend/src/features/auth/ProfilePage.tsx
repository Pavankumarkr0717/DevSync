import { useAppSelector } from '../../app/hooks';
import { formatDate } from '../../shared/text';
import { useLogoutAllMutation, useLogoutMutation } from './authApi';
import { selectCurrentUser } from './authSlice';

export function ProfilePage() {
  const user = useAppSelector(selectCurrentUser);
  const [logout, { isLoading: signingOut }] = useLogoutMutation();
  const [logoutAll, { isLoading: signingOutEverywhere }] = useLogoutAllMutation();

  if (!user) {
    return null;
  }

  const onSignOutEverywhere = () => {
    if (window.confirm('Sign out of DevSync on every device and browser?')) {
      void logoutAll();
    }
  };

  return (
    <section className="panel form-panel">
      <h1>Profile</h1>
      <dl>
        <dt>Name</dt>
        <dd>{user.displayName}</dd>
        <dt>Email</dt>
        <dd>{user.email}</dd>
        <dt>Member since</dt>
        <dd>{formatDate(user.createdAt)}</dd>
      </dl>
      <p className="actions">
        <button type="button" onClick={() => void logout()} disabled={signingOut}>
          Sign out
        </button>{' '}
        <button type="button" onClick={onSignOutEverywhere} disabled={signingOutEverywhere}>
          Sign out everywhere
        </button>
      </p>
    </section>
  );
}
