import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  protected readonly currentYear = new Date().getFullYear();
  protected navOpen = false;

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
