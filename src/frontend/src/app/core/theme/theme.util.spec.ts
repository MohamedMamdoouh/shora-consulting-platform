import { describe, expect, it } from 'vitest';
import {
  nextThemePreference,
  parseStoredThemePreference,
  resolveTheme,
} from './theme.util';

describe('theme utils', () => {
  it('defaults to system and resolves dark when the OS is dark', () => {
    expect(parseStoredThemePreference(null)).toBe('system');
    expect(resolveTheme('system', true)).toBe('dark');
  });

  it('defaults to system and resolves light when the OS is light', () => {
    expect(parseStoredThemePreference(null)).toBe('system');
    expect(resolveTheme('system', false)).toBe('light');
  });

  it('ignores OS changes after an explicit light or dark preference', () => {
    expect(resolveTheme('light', true)).toBe('light');
    expect(resolveTheme('dark', false)).toBe('dark');
  });

  it('restores a stored dark preference even when the OS is light', () => {
    expect(parseStoredThemePreference('dark')).toBe('dark');
    expect(resolveTheme(parseStoredThemePreference('dark'), false)).toBe('dark');
  });

  it('pins the opposite of the system theme, then returns to system', () => {
    expect(nextThemePreference('system', true)).toBe('light');
    expect(nextThemePreference('light', true)).toBe('system');
    expect(nextThemePreference('system', false)).toBe('dark');
    expect(nextThemePreference('dark', false)).toBe('system');
  });

  it('skips a no-op return to system when the OS already matches the pin', () => {
    expect(nextThemePreference('dark', true)).toBe('light');
    expect(nextThemePreference('light', false)).toBe('dark');
  });
});
