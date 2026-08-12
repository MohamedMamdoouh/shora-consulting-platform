import { Component, input } from '@angular/core';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import { BookingCtaComponent } from '../../public/shared/booking-cta.component';

@Component({
  selector: 'app-footer-cta-banner',
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
            <rect x="8" y="12" width="32" height="28" rx="6" fill="#8A79C4" opacity="0.15" />
            <rect x="8" y="12" width="32" height="28" rx="6" stroke="#8A79C4" stroke-width="2" />
            <path d="M8 20h32" stroke="#8A79C4" stroke-width="2" />
            <path d="M18 8v8M30 8v8" stroke="#8A79C4" stroke-width="2" stroke-linecap="round" />
            <rect x="16" y="26" width="6" height="6" rx="1.5" fill="#8A79C4" />
          </svg>
        </div>
        <div class="footer-cta__icon footer-cta__icon--clock">
          <svg viewBox="0 0 48 48" width="36" height="36" fill="none">
            <circle cx="24" cy="26" r="14" fill="#E8847A" opacity="0.15" />
            <circle cx="24" cy="26" r="14" stroke="#E8847A" stroke-width="2" />
            <path d="M24 20v7l5 3" stroke="#E8847A" stroke-width="2" stroke-linecap="round" />
            <path d="M24 8v4" stroke="#E8847A" stroke-width="2" stroke-linecap="round" />
          </svg>
        </div>
      </div>
    </aside>
  `,
  styles: `
    .footer-cta {
      display: grid;
      gap: var(--space-xl);
      align-items: center;
      padding: var(--space-2xl);
      background: var(--gradient-cta-banner);
      border: 1px solid var(--color-primary-muted);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-sm);
    }

    .footer-cta__content {
      display: grid;
      gap: var(--space-md);
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
      min-height: 5rem;
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
      inset-inline-end: 20%;
      top: 0;
      padding: var(--space-sm);
    }

    .footer-cta__icon--clock {
      inset-inline-start: 15%;
      bottom: 0;
      padding: var(--space-xs);
    }

    @media (min-width: 768px) {
      .footer-cta {
        grid-template-columns: 1fr auto;
        padding: var(--space-2xl) var(--space-3xl);
      }

      .footer-cta__visual {
        width: 10rem;
        min-height: 6rem;
      }
    }
  `,
})
export class FooterCtaBannerComponent {
  readonly title = input(APP_COPY.services.footerCtaTitle);
  readonly subtitle = input(APP_COPY.services.footerCtaSubtitle);

  protected readonly copy = APP_COPY;
}
