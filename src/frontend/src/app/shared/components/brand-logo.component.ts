import { Component, input } from '@angular/core';

@Component({
  selector: 'app-brand-logo',
  host: {
    class: 'brand-logo',
    '[class.brand-logo--compact]': 'compact()',
  },
  template: `
    <svg viewBox="0 0 128 128" fill="none" role="img" [attr.aria-label]="ariaLabel()">
      <defs>
        <linearGradient [attr.id]="bgId" x1="8" y1="8" x2="120" y2="120" gradientUnits="userSpaceOnUse">
          <stop stop-color="#EDE9F8" />
          <stop offset="1" stop-color="#FFF5F0" />
        </linearGradient>
        <linearGradient [attr.id]="brandId" x1="28" y1="96" x2="100" y2="24" gradientUnits="userSpaceOnUse">
          <stop stop-color="#8A79C4" />
          <stop offset="0.55" stop-color="#E8847A" />
          <stop offset="1" stop-color="#F5A962" />
        </linearGradient>
      </defs>
      <rect x="8" y="8" width="112" height="112" rx="28" [attr.fill]="'url(#' + bgId + ')'" />
      <path
        d="M64 92c-14 0-24-10-24-22 0-8 5-14 12-17-2 8 2 16 8 20-6-4-8-12-5-19 8 5 13 14 9 23 4-6 4-14 0-20 6 4 9 12 7 19 7-3 12-9 12-17-7 3-12 9-12 17 0 12-10 22-24 22z"
        [attr.fill]="'url(#' + brandId + ')'"
      />
      <circle cx="48" cy="52" r="5" fill="#8A79C4" opacity="0.9" />
      <circle cx="80" cy="52" r="5" fill="#E8847A" opacity="0.9" />
    </svg>
  `,
  styles: `
    :host {
      display: inline-block;
      line-height: 0;
    }

    svg {
      display: block;
      width: auto;
      height: 4.5rem;
    }

    :host(.brand-logo--compact) svg {
      height: 2.75rem;
    }
  `,
})
export class BrandLogoComponent {
  private static nextId = 0;

  readonly compact = input(false);
  readonly ariaLabel = input('شورى');

  protected readonly bgId = `shora-bg-${BrandLogoComponent.nextId}`;
  protected readonly brandId = `shora-brand-${BrandLogoComponent.nextId++}`;
}
