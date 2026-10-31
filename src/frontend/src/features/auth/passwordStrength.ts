import { codePointLength } from '../../shared/text';

const MIN = 12;

/**
 * A guide for the person, not a rule: the server only requires 12–128 characters. Longer passwords
 * and a mix of letter case, digits and symbols score higher.
 */
export function passwordStrength(password: string): number {
  const length = codePointLength(password);
  if (length < MIN) {
    return 0;
  }
  const kinds = [/[a-z]/, /[A-Z]/, /\d/, /[^A-Za-z0-9]/].filter((re) => re.test(password)).length;
  return Math.min(4, 1 + (length >= 16 ? 1 : 0) + (kinds >= 3 ? 1 : 0) + (length >= 20 || kinds === 4 ? 1 : 0));
}
