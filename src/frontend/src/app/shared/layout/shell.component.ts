import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import { AuthService } from '../../core/auth/auth.service';
import { ThemePreference, ThemeService } from '../../core/theme/theme.service';
import { BrandLogoComponent } from '../components/brand-logo.component';

@Component({
  selector: 'app-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, BrandLogoComponent],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);
  protected readonly copy = APP_COPY;
  protected navOpen = false;

  protected themeLabel(): string {
    const labels: Record<ThemePreference, string> = {
      system: this.copy.theme.system,
      light: this.copy.theme.light,
      dark: this.copy.theme.dark,
    };

    return `${this.copy.theme.cycle} — ${labels[this.theme.preference()]}`;
  }

  cycleTheme(): void {
    this.theme.toggle();
  }

  toggleNav(): void {
    this.navOpen = !this.navOpen;
  }

  closeNav(): void {
    this.navOpen = false;
  }

  logout(): void {
    this.closeNav();
    void this.auth.logout();
  }
}
