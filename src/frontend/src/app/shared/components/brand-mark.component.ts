import { Component, input } from '@angular/core';

@Component({
  selector: 'app-brand-mark',
  host: {
    '[class.brand-mark]': 'true',
    '[class.brand-mark--gradient]': 'gradient()',
  },
  template: `
    <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
      @if (gradient()) {
        <defs>
          <linearGradient id="shora-mark-gradient" x1="4" y1="18" x2="20" y2="4" gradientUnits="userSpaceOnUse">
            <stop stop-color="#7C5CBF" />
            <stop offset="0.55" stop-color="#E8847A" />
            <stop offset="1" stop-color="#F5A962" />
          </linearGradient>
        </defs>
      }
      <path
        [attr.fill]="gradient() ? 'url(#shora-mark-gradient)' : 'currentColor'"
        fill-rule="evenodd"
        d="M4 18V11C4 7.134 7.134 4 12 4s8 3.134 8 7v7H4Zm2.5-2H17.5V11C17.5 8.462 15.038 6 12 6S6.5 8.462 6.5 11v5Z"
      />
      <path d="M7 18h10" stroke="#F5A962" stroke-width="1.2" stroke-linecap="round" />
      <circle cx="12" cy="4" r="1" fill="#F5A962" />
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      line-height: 0;
    }

    svg {
      width: 1em;
      height: 1em;
    }

    :host(.brand-mark--gradient) svg {
      width: 1.125rem;
      height: 1.125rem;
    }
  `,
})
export class BrandMarkComponent {
  readonly gradient = input(false);
}
