export type ThemePreference = 'system' | 'light' | 'dark';
export type ResolvedTheme = 'light' | 'dark';

export const THEME_STORAGE_KEY = 'shora.theme';

export function resolveTheme(
  preference: ThemePreference,
  systemDark: boolean,
): ResolvedTheme {
  if (preference === 'system') {
    return systemDark ? 'dark' : 'light';
  }

  return preference;
}

export function oppositeTheme(theme: ResolvedTheme): ResolvedTheme {
  return theme === 'dark' ? 'light' : 'dark';
}

export function nextThemePreference(
  preference: ThemePreference,
  systemDark: boolean,
): ThemePreference {
  const resolved = resolveTheme(preference, systemDark);
  const opposite = oppositeTheme(resolved);

  if (preference === 'system') {
    return opposite;
  }

  if (resolveTheme('system', systemDark) === resolved) {
    return opposite;
  }

  return 'system';
}

export function parseStoredThemePreference(stored: string | null): ThemePreference {
  if (stored === 'light' || stored === 'dark' || stored === 'system') {
    return stored;
  }

  return 'system';
}
