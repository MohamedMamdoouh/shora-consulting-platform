import { Component, input } from '@angular/core';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import { formatCurrency, formatDurationMinutes } from '../../core/i18n/app-locale';
import { BookingCtaComponent } from '../../public/shared/booking-cta.component';
import { CounselingSceneComponent } from './counseling-scene.component';

@Component({
  selector: 'app-featured-session-card',
  imports: [BookingCtaComponent, CounselingSceneComponent],
  template: `
    <article class="featured-session">
      <div class="featured-session__content">
        <h2 class="featured-session__title">{{ copy.session.individualTitle }}</h2>
        <p class="featured-session__subtitle">{{ copy.session.flatPriceSubtitle }}</p>

        <div class="featured-session__meta">
          <div class="featured-session__meta-item">
            <span class="featured-session__meta-icon featured-session__meta-icon--price" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="22" height="22" fill="none">
                <path
                  d="M20.5 10.5c0 4.4-3.6 8-8 8H8l-3 3v-4.5C3.5 14.1 3.5 9.9 6 7.4S11.6 4.5 16 4.5c2.5 0 4.5 2 4.5 6z"
                  stroke="currentColor"
                  stroke-width="1.75"
                  stroke-linejoin="round"
                />
                <circle cx="9" cy="10.5" r="1" fill="currentColor" />
                <circle cx="13" cy="10.5" r="1" fill="currentColor" />
              </svg>
            </span>
            <div>
              <span class="featured-session__meta-label">{{ copy.session.priceLabel }}</span>
              <strong class="featured-session__meta-value">{{ formatPrice(sessionPrice()) }}</strong>
            </div>
          </div>

          <div class="featured-session__meta-item">
            <span class="featured-session__meta-icon featured-session__meta-icon--duration" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="22" height="22" fill="none">
                <circle cx="12" cy="12" r="8.5" stroke="currentColor" stroke-width="1.75" />
                <path d="M12 7.5V12l3 2" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" />
              </svg>
            </span>
            <div>
              <span class="featured-session__meta-label">{{ copy.session.durationLabel }}</span>
              <strong class="featured-session__meta-value">{{
                formatDurationMinutes(sessionDurationMinutes(), copy.session.durationUnit)
              }}</strong>
            </div>
          </div>

          <div class="featured-session__meta-item">
            <span class="featured-session__meta-icon featured-session__meta-icon--method" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="22" height="22" fill="none">
                <path
                  d="M17.5 4.5A8.5 8.5 0 0112 20.5 8.5 8.5 0 016.5 4.5a8.5 8.5 0 0111 0z"
                  stroke="currentColor"
                  stroke-width="1.75"
                />
                <path
                  d="M8.5 15.5c1.2 1.5 2.8 2.3 3.5 2.3s2.3-.8 3.5-2.3"
                  stroke="currentColor"
                  stroke-width="1.75"
                  stroke-linecap="round"
                />
              </svg>
            </span>
            <div>
              <span class="featured-session__meta-label">{{ copy.session.methodLabel }}</span>
              <strong class="featured-session__meta-value featured-session__meta-value--small">{{
                copy.session.deliveryMethods
              }}</strong>
            </div>
          </div>
        </div>

        <app-booking-cta
          class="featured-session__cta"
          [label]="copy.cta.bookSessionNow"
          [fullWidth]="true"
          [showCalendarIcon]="true"
        />

        <p class="featured-session__note">
          <span class="featured-session__note-icon" aria-hidden="true">
            <svg viewBox="0 0 24 24" width="16" height="16" fill="none">
              <path
                d="M12 3l7 3v5c0 4.5-3 7.8-7 9-4-1.2-7-4.5-7-9V6l7-3z"
                stroke="currentColor"
                stroke-width="1.75"
                stroke-linejoin="round"
              />
            </svg>
          </span>
          {{ copy.session.privacyNote }}
        </p>
      </div>

      <div class="featured-session__visual">
        <app-counseling-scene />
      </div>
    </article>
  `,
  styles: `
    .featured-session {
      display: grid;
      gap: var(--space-xl);
      padding: var(--space-xl);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-card);
    }

    .featured-session__content {
      display: grid;
      gap: var(--space-lg);
    }

    .featured-session__title {
      margin: 0;
      font-size: var(--font-size-xl);
      font-weight: 800;
    }

    .featured-session__subtitle {
      margin: calc(var(--space-sm) * -1) 0 0;
      color: var(--color-text-muted);
      font-size: var(--font-size-sm);
    }

    .featured-session__meta {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: var(--space-md);
    }

    .featured-session__meta-item {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--space-sm);
      text-align: center;
    }

    .featured-session__meta-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 3rem;
      height: 3rem;
      border-radius: var(--radius-full);
    }

    .featured-session__meta-icon--price {
      background: rgba(220, 38, 38, 0.08);
      color: #dc2626;
    }

    .featured-session__meta-icon--duration {
      background: var(--color-primary-muted);
      color: var(--color-primary);
    }

    .featured-session__meta-icon--method {
      background: var(--color-success-bg);
      color: var(--color-success);
    }

    .featured-session__meta-label {
      display: block;
      color: var(--color-text-subtle);
      font-size: var(--font-size-xs);
      font-weight: 500;
    }

    .featured-session__meta-value {
      display: block;
      margin-top: var(--space-2xs);
      font-family: var(--font-display);
      font-size: var(--font-size-lg);
      font-weight: 700;
      color: var(--color-text);
    }

    .featured-session__meta-value--small {
      font-family: var(--font-body);
      font-size: var(--font-size-sm);
      font-weight: 600;
      line-height: var(--line-height-tight);
    }

    .featured-session__cta {
      width: 100%;
    }

    .featured-session__note {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: var(--space-sm);
      margin: 0;
      color: var(--color-text-subtle);
      font-size: var(--font-size-xs);
    }

    .featured-session__note-icon {
      display: inline-flex;
      color: var(--color-text-muted);
    }

    .featured-session__visual {
      display: flex;
      align-items: center;
      justify-content: center;
    }

    @media (min-width: 768px) {
      .featured-session {
        grid-template-columns: 1.1fr 0.9fr;
        align-items: center;
        padding: var(--space-2xl);
        gap: var(--space-2xl);
      }

      .featured-session__meta-item {
        align-items: flex-start;
        text-align: start;
      }
    }

    @media (max-width: 640px) {
      .featured-session__meta {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class FeaturedSessionCardComponent {
  readonly sessionPrice = input.required<number>();
  readonly sessionDurationMinutes = input.required<number>();

  protected readonly copy = APP_COPY;
  protected readonly formatPrice = (amount: number) => formatCurrency(amount, 'EGP');
  protected readonly formatDurationMinutes = formatDurationMinutes;
}
