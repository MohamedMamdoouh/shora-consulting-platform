import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import { AuthService } from '../../core/auth/auth.service';
import { ThemeService } from '../../core/theme/theme.service';
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
    const next =
      this.theme.resolved() === 'dark' ? this.copy.theme.light : this.copy.theme.dark;
    return `${this.copy.theme.cycle} — ${next}`;
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
