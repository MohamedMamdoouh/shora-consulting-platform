import { Component, input } from '@angular/core';

export type BrandLogoSize = 'shell' | 'auth' | 'hero';

@Component({
  selector: 'app-brand-logo',
  host: {
    '[class.brand-logo]': 'true',
    '[class.brand-logo--shell]': 'size() === "shell"',
    '[class.brand-logo--auth]': 'size() === "auth"',
    '[class.brand-logo--hero]': 'size() === "hero"',
  },
  template: `
    <svg viewBox="0 0 128 128" fill="none" role="img" aria-label="شورى">
      <defs>
        <linearGradient [attr.id]="bgId" x1="8" y1="8" x2="120" y2="120" gradientUnits="userSpaceOnUse">
          <stop stop-color="#F3EEFF" />
          <stop offset="1" stop-color="#FFF5F0" />
        </linearGradient>
        <linearGradient [attr.id]="brandId" x1="28" y1="96" x2="100" y2="24" gradientUnits="userSpaceOnUse">
          <stop stop-color="#7C5CBF" />
          <stop offset="0.55" stop-color="#E8847A" />
          <stop offset="1" stop-color="#F5A962" />
        </linearGradient>
      </defs>
      <rect x="8" y="8" width="112" height="112" rx="28" [attr.fill]="'url(#' + bgId + ')'" />
      <path
        [attr.fill]="'url(#' + brandId + ')'"
        fill-rule="evenodd"
        d="M28 96V52C28 30.536 44.536 16 64 16C83.464 16 100 30.536 100 52V96H28ZM42 92H86V54C86 40.745 76.255 30 64 30C51.745 30 42 40.745 42 54V92Z"
      />
      <path d="M46 92H82" stroke="#F5A962" stroke-width="4" stroke-linecap="round" opacity="0.95" />
      <circle cx="64" cy="24" r="4.5" fill="#F5A962" />
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
      height: 2.25rem;
    }

    :host(.brand-logo--auth) svg {
      height: 2.5rem;
    }

    :host(.brand-logo--hero) svg {
      height: 4.5rem;
    }
  `,
})
export class BrandLogoComponent {
  private static nextId = 0;

  readonly size = input<BrandLogoSize>('shell');
  protected readonly bgId = `shora-bg-${BrandLogoComponent.nextId}`;
  protected readonly brandId = `shora-brand-${BrandLogoComponent.nextId++}`;
}
