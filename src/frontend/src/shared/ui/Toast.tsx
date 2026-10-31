import { useEffect } from 'react';
import { useAppDispatch, useAppSelector } from '../../app/hooks';
import { selectToast, toastDismissed } from './uiSlice';

/** A short-lived global message (e.g. "You don't have permission to do that"). */
export function Toast() {
  const dispatch = useAppDispatch();
  const toast = useAppSelector(selectToast);

  useEffect(() => {
    if (!toast) {
      return;
    }
    const timer = window.setTimeout(() => dispatch(toastDismissed()), 6000);
    return () => window.clearTimeout(timer);
  }, [toast, dispatch]);

  if (!toast) {
    return null;
  }

  return (
    <div role="alert" className="toast">
      {toast}{' '}
      <button type="button" onClick={() => dispatch(toastDismissed())} aria-label="Dismiss">
        ×
      </button>
    </div>
  );
}
