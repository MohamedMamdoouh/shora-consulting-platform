import { Component, input } from '@angular/core';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import { BookingCtaComponent } from '../../public/shared/booking-cta.component';

@Component({
  selector: 'app-footer-cta-banner',
  host: {
    class: 'footer-cta-host',
  },
  imports: [BookingCtaComponent],
  template: `
    <aside class="footer-cta" aria-label="دعوة للحجز">
      <div class="footer-cta__content">
        <h2 class="footer-cta__title">{{ title() }}</h2>
        <p class="footer-cta__subtitle">{{ subtitle() }}</p>
        <app-booking-cta [label]="copy.cta.bookSessionNow" />
      </div>

      <div class="footer-cta__visual" aria-hidden="true">
        <div class="footer-cta__icon footer-cta__icon--calendar">
          <svg viewBox="0 0 48 48" width="40" height="40" fill="none">
            <rect x="8" y="12" width="32" height="28" rx="6" fill="currentColor" opacity="0.12" />
            <rect x="8" y="12" width="32" height="28" rx="6" stroke="currentColor" stroke-width="2" />
            <path d="M8 20h32" stroke="currentColor" stroke-width="2" />
            <path d="M18 8v8M30 8v8" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
            <rect x="16" y="26" width="6" height="6" rx="1.5" fill="currentColor" />
          </svg>
        </div>
      </div>
    </aside>
  `,
  styles: `
    :host {
      display: block;
      margin-top: var(--space-lg);
    }

    .footer-cta {
      display: grid;
      gap: var(--space-sm);
      align-items: center;
      padding: var(--space-md);
      background: var(--gradient-cta-banner);
      border: 1px solid var(--color-primary-muted);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-sm);
    }

    .footer-cta__content {
      display: grid;
      gap: var(--space-sm);
    }

    .footer-cta__title {
      margin: 0;
      font-size: var(--font-size-xl);
      font-weight: 800;
      line-height: var(--line-height-heading);
    }

    .footer-cta__subtitle {
      margin: 0;
      color: var(--color-text-muted);
      font-size: var(--font-size-sm);
      line-height: var(--line-height-body);
    }

    .footer-cta__visual {
      position: relative;
      min-height: 3.5rem;
    }

    .footer-cta__icon {
      position: absolute;
      display: flex;
      align-items: center;
      justify-content: center;
      border-radius: var(--radius-md);
      background: var(--color-surface);
      box-shadow: var(--shadow-sm);
    }

    .footer-cta__icon--calendar {
      inset-inline-end: 0;
      top: 50%;
      transform: translateY(-50%);
      padding: var(--space-sm);
      color: var(--color-primary);
    }

    @media (min-width: 768px) {
      .footer-cta {
        grid-template-columns: 1fr auto;
        padding: var(--space-md) var(--space-lg);
      }

      .footer-cta__visual {
        width: 4rem;
        min-height: 3rem;
      }
    }
  `,
})
export class FooterCtaBannerComponent {
  readonly title = input(APP_COPY.services.footerCtaTitle);
  readonly subtitle = input(APP_COPY.services.footerCtaSubtitle);

  protected readonly copy = APP_COPY;
}
