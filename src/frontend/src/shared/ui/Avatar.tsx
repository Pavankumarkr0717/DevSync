import { initials, pastelFor } from './avatarStyle';

interface AvatarProps {
  id: string;
  name: string;
  size?: 'sm' | 'md' | 'lg';
}

/** Initials on a pastel circle (docs/design/design-system.md). Decorative: the name is shown nearby. */
export function Avatar({ id, name, size = 'md' }: AvatarProps) {
  return (
    <span className={`avatar avatar-${size} pastel-${pastelFor(id)}`} aria-hidden="true">
      {initials(name)}
    </span>
  );
}
