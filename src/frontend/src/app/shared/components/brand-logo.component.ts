import { Component, input } from '@angular/core';

@Component({
  selector: 'app-brand-logo',
  host: {
    class: 'brand-logo',
    '[class.brand-logo--compact]': 'compact()',
  },
  template: `
    <svg
      viewBox="0 0 40 40"
      fill="none"
      [attr.role]="decorative() ? 'presentation' : 'img'"
      [attr.aria-hidden]="decorative() ? 'true' : null"
      [attr.aria-label]="decorative() ? null : ariaLabel()"
    >
      <rect x="2" y="2" width="36" height="36" rx="12" fill="currentColor" fill-opacity="0.08" />
      <path
        d="M26 11v18M26 11H17.5c-3.2 0-5.5 2.2-5.5 5.2V28"
        stroke="currentColor"
        stroke-width="2.75"
        stroke-linecap="round"
        stroke-linejoin="round"
      />
    </svg>
  `,
  styles: `
    :host {
      display: inline-block;
      line-height: 0;
      color: var(--color-primary);
    }

    svg {
      display: block;
      width: auto;
      height: 3rem;
    }

    :host(.brand-logo--compact) svg {
      height: 2rem;
    }
  `,
})
export class BrandLogoComponent {
  readonly compact = input(false);
  readonly decorative = input(false);
  readonly ariaLabel = input('دكتور محمود البنا');
}
