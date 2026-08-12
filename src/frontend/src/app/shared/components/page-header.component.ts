import { Component, input } from '@angular/core';

@Component({
  selector: 'app-page-header',
  host: {
    class: 'page-header',
  },
  template: `
    <header class="page-header__inner">
      <h1 class="page-header__title">{{ title() }}</h1>
      @if (description()) {
        <p class="page-header__description">{{ description() }}</p>
      }
    </header>
  `,
  styles: `
    :host {
      display: block;
      margin-bottom: var(--space-2xl);
    }

    .page-header__inner {
      display: grid;
      gap: var(--space-md);
      text-align: start;
    }

    .page-header__title {
      position: relative;
      margin: 0;
      padding-inline-start: var(--space-lg);
      font-size: clamp(1.75rem, 4vw, var(--font-size-2xl));
      font-weight: 800;
      line-height: var(--line-height-heading);
    }

    .page-header__title::before {
      content: '';
      position: absolute;
      inset-inline-start: 0;
      top: 0.15em;
      bottom: 0.15em;
      width: 4px;
      border-radius: var(--radius-full);
      background: var(--color-primary);
    }

    .page-header__description {
      margin: 0;
      max-width: 52ch;
      color: var(--color-text-muted);
      font-size: var(--font-size-lg);
      line-height: var(--line-height-body);
    }

    :host(.page-header--center) .page-header__inner {
      text-align: center;
    }

    :host(.page-header--center) .page-header__title {
      padding-inline-start: 0;
    }

    :host(.page-header--center) .page-header__title::before {
      display: none;
    }

    :host(.page-header--center) .page-header__description {
      margin-inline: auto;
    }
  `,
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly description = input<string>();
}
