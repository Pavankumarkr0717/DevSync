import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Avatar } from '@/shared/ui/Avatar';
import { initials, pastelFor } from '@/shared/ui/avatarStyle';

describe('Avatar', () => {
  it.each([
    ['Maya Chen', 'MC'],
    ['ana', 'A'],
    ['  Leo   van Park ', 'LP'],
    ['', '?'],
  ])('initials of %j are %s', (name, expected) => {
    expect(initials(name)).toBe(expected);
  });

  it('gives the same person the same colour', () => {
    expect(pastelFor('user-1')).toBe(pastelFor('user-1'));
  });

  it('is decorative, since the name is shown next to it', () => {
    const { container } = render(<Avatar id="u1" name="Maya Chen" />);

    expect(container.firstElementChild).toHaveAttribute('aria-hidden', 'true');
    expect(container).toHaveTextContent('MC');
  });
});
