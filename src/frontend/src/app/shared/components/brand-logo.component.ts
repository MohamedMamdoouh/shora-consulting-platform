import { Component, input } from '@angular/core';

@Component({
  selector: 'app-brand-logo',
  host: {
    class: 'brand-logo',
    '[class.brand-logo--compact]': 'compact()',
    '[class.brand-logo--hero]': 'hero()',
  },
  template: `
    <img
      src="/shora-logo.png"
      width="512"
      height="512"
      [attr.alt]="decorative() ? '' : ariaLabel()"
      [attr.aria-hidden]="decorative() ? 'true' : null"
      decoding="async"
    />
  `,
  styles: `
    :host {
      display: inline-block;
      line-height: 0;
    }

    img {
      display: block;
      width: auto;
      height: 3rem;
      border-radius: var(--radius-md);
    }

    :host(.brand-logo--compact) img {
      height: 2rem;
    }

    :host(.brand-logo--hero) {
      flex-shrink: 0;
    }

    :host(.brand-logo--hero) img {
      width: auto;
      max-width: 100%;
      height: clamp(12rem, 52vmin, 32rem);
    }
  `,
})
export class BrandLogoComponent {
  readonly compact = input(false);
  readonly hero = input(false);
  readonly decorative = input(false);
  readonly ariaLabel = input('منصة شورى');
}
