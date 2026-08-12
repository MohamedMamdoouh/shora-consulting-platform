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
  template: `<img src="logo.svg" width="128" height="128" alt="شورى" />`,
  styles: `
    :host {
      display: inline-block;
      line-height: 0;
    }

    img {
      display: block;
      width: auto;
      height: 2.25rem;
    }

    :host(.brand-logo--auth) img {
      height: 2.5rem;
    }

    :host(.brand-logo--hero) img {
      height: 4.5rem;
    }
  `,
})
export class BrandLogoComponent {
  readonly size = input<BrandLogoSize>('shell');
}
