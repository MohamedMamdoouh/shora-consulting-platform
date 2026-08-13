export type ThemePreference = 'system' | 'light' | 'dark';
export type ResolvedTheme = 'light' | 'dark';

export const THEME_STORAGE_KEY = 'shora.theme';

const PREFERENCE_CYCLE: Record<ThemePreference, ThemePreference> = {
  system: 'light',
  light: 'dark',
  dark: 'system',
};

export function resolveTheme(
  preference: ThemePreference,
  systemDark: boolean,
): ResolvedTheme {
  if (preference === 'system') {
    return systemDark ? 'dark' : 'light';
  }

  return preference;
}

export function nextThemePreference(preference: ThemePreference): ThemePreference {
  return PREFERENCE_CYCLE[preference];
}

export function parseStoredThemePreference(stored: string | null): ThemePreference {
  if (stored === 'light' || stored === 'dark' || stored === 'system') {
    return stored;
  }

  return 'system';
}
