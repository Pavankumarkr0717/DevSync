import { describe, expect, it } from 'vitest';
import { languageFor, validateName } from '@/features/files/fileRules';

// Mirrors the server's ItemName and SupportedLanguages rules (spec 003 FR-005, FR-010) for instant feedback.
describe('fileRules', () => {
  it.each(['server.ts', 'résumé.md', '.eslintrc.json', 'my file (1).cs'])('accepts %s', (name) => {
    expect(validateName(name, 'file', [])).toBeNull();
  });

  it.each([
    ['', 'Name is required.'],
    ['a/b.ts', 'Name cannot contain "/"'],
    ['a\\b.ts', 'Name cannot contain "\\"'],
    ['con:.ts', 'Name cannot contain ":"'],
    ['x?.ts', 'Name cannot contain "?"'],
    ['.', 'Name cannot be "." or "..".'],
    ['..', 'Name cannot be "." or "..".'],
    [' x.ts', 'Name cannot start or end with a space.'],
    ['x.ts ', 'Name cannot start or end with a space.'],
    ['x.ts.', 'Name cannot end with a dot.'],
    ['a\u0001.ts', 'Name cannot contain control characters.'],
  ])('rejects %j', (name, message) => {
    expect(validateName(name, 'file', [])).toContain(message);
  });

  it('limits names to 255 characters, counted in code points', () => {
    expect(validateName(`${'a'.repeat(252)}.md`, 'file', [])).toBeNull();
    expect(validateName(`${'a'.repeat(253)}.md`, 'file', [])).toBe('Name must be at most 255 characters.');
    expect(validateName('😀'.repeat(255), 'folder', [])).toBeNull();
  });

  it('allows only the 11 extensions for files, ignoring case', () => {
    for (const name of ['a.cs', 'a.JS', 'a.ts', 'a.Tsx', 'a.jsx', 'a.json', 'a.html', 'a.css', 'a.py', 'a.java', 'a.MD']) {
      expect(validateName(name, 'file', [])).toBeNull();
    }
    for (const name of ['notes.txt', 'Dockerfile', 'ci.yml', '.gitignore']) {
      expect(validateName(name, 'file', [])).toBe('Supported: .cs .js .ts .tsx .jsx .json .html .css .py .java .md');
    }
    expect(validateName('Dockerfile', 'folder', [])).toBeNull();
  });

  it('rejects a sibling with the same name ignoring case, but not the item itself', () => {
    const siblings = [{ id: '1', name: 'Main.cs' }];
    expect(validateName('main.CS', 'file', siblings)).toBe('An item with this name already exists here.');
    expect(validateName('MAIN.cs', 'file', siblings, '1')).toBeNull();
  });

  it('maps extensions to editor languages', () => {
    expect(languageFor('a.cs')).toBe('csharp');
    expect(languageFor('a.jsx')).toBe('javascript');
    expect(languageFor('App.TSX')).toBe('typescript');
    expect(languageFor('README.md')).toBe('markdown');
    expect(languageFor('a.py')).toBe('python');
    expect(languageFor('a.txt')).toBe('plaintext');
  });
});
