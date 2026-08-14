import { computed, Injectable, signal } from '@angular/core';
import {
  oppositeTheme,
  parseStoredThemePreference,
  resolveTheme,
  THEME_STORAGE_KEY,
  type ResolvedTheme,
  type ThemePreference,
} from './theme.util';

export type { ResolvedTheme, ThemePreference } from './theme.util';

const mediaQuery =
  typeof window.matchMedia === 'function'
    ? window.matchMedia('(prefers-color-scheme: dark)')
    : null;

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly preferenceState = signal<ThemePreference>(readStoredPreference());
  private readonly systemDark = signal(mediaQuery?.matches ?? false);

  readonly preference = this.preferenceState.asReadonly();
  readonly resolved = computed<ResolvedTheme>(() =>
    resolveTheme(this.preferenceState(), this.systemDark()),
  );

  constructor() {
    mediaQuery?.addEventListener('change', (event) => {
      this.systemDark.set(event.matches);
      this.apply();
    });
    this.apply();
  }

  toggle(): void {
    const next = oppositeTheme(this.resolved());
    this.preferenceState.set(next);
    persistPreference(next);
    this.apply();
  }

  private apply(): void {
    const theme = this.resolved();
    document.documentElement.setAttribute('data-theme', theme);
    document.documentElement.style.colorScheme = theme;
  }
}

function readStoredPreference(): ThemePreference {
  try {
    return parseStoredThemePreference(localStorage.getItem(THEME_STORAGE_KEY));
  } catch {
    return 'system';
  }
}

function persistPreference(preference: ThemePreference): void {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, preference);
  } catch {
    // Private mode or blocked storage.
  }
}
