import { computed, Injectable, signal } from '@angular/core';
import {
  nextThemePreference,
  parseStoredThemePreference,
  resolveTheme,
  THEME_STORAGE_KEY,
  type ResolvedTheme,
  type ThemePreference,
} from './theme.util';

export type { ResolvedTheme, ThemePreference } from './theme.util';
export { THEME_STORAGE_KEY } from './theme.util';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly mediaQuery = this.getMediaQuery();
  private readonly preferenceState = signal<ThemePreference>(this.readStoredPreference());
  private readonly systemDark = signal(this.mediaQuery?.matches ?? false);

  readonly preference = this.preferenceState.asReadonly();
  readonly resolved = computed<ResolvedTheme>(() =>
    resolveTheme(this.preferenceState(), this.systemDark()),
  );

  constructor() {
    this.mediaQuery?.addEventListener('change', this.onSystemChange);
    this.apply(this.resolved());
  }

  initialize(): void {
    this.apply(this.resolved());
  }

  toggle(): void {
    this.setPreference(nextThemePreference(this.preferenceState()));
  }

  setPreference(preference: ThemePreference): void {
    this.preferenceState.set(preference);
    this.persist(preference);
    this.apply(resolveTheme(preference, this.systemDark()));
  }

  private readonly onSystemChange = (event: MediaQueryListEvent): void => {
    this.systemDark.set(event.matches);
    if (this.preferenceState() === 'system') {
      this.apply(this.resolved());
    }
  };

  private apply(theme: ResolvedTheme): void {
    const root = document.documentElement;
    root.setAttribute('data-theme', theme);
    root.style.colorScheme = theme;
  }

  private readStoredPreference(): ThemePreference {
    try {
      return parseStoredThemePreference(localStorage.getItem(THEME_STORAGE_KEY));
    } catch {
      return 'system';
    }
  }

  private persist(preference: ThemePreference): void {
    try {
      localStorage.setItem(THEME_STORAGE_KEY, preference);
    } catch {
      // Ignore persistence failures.
    }
  }

  private getMediaQuery(): MediaQueryList | null {
    if (typeof window.matchMedia !== 'function') {
      return null;
    }

    return window.matchMedia('(prefers-color-scheme: dark)');
  }
}
