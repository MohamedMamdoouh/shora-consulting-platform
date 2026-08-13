import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { APP_COPY } from '../../core/i18n/app-copy.constants';

@Component({
  selector: 'app-booking-cta',
  imports: [RouterLink],
  template: `
    <a
      routerLink="/booking/start"
      class="btn booking-cta"
      [class.btn--lg]="!fullWidth()"
      [class.booking-cta--full]="fullWidth()"
      [class.btn--gradient]="!soft()"
      [class.booking-cta--soft]="soft()"
    >
      @if (showCalendarIcon()) {
        <span class="booking-cta__icon" aria-hidden="true">
          <svg viewBox="0 0 24 24" width="20" height="20" fill="none">
            <rect
              x="4"
              y="5"
              width="16"
              height="15"
              rx="2"
              stroke="currentColor"
              stroke-width="1.75"
            />
            <path
              d="M4 9h16M8 3v4M16 3v4"
              stroke="currentColor"
              stroke-width="1.75"
              stroke-linecap="round"
            />
          </svg>
        </span>
      }
      {{ label() }}
      <span class="booking-cta__arrow" aria-hidden="true">←</span>
    </a>
  `,
  styles: `
    .booking-cta {
      gap: var(--space-sm);
      border-radius: var(--radius-md);
    }

    .booking-cta--full {
      width: 100%;
      min-height: 3.25rem;
      font-size: var(--font-size-base);
    }

    .booking-cta--soft {
      background: var(--color-primary-soft);
      color: var(--color-primary);
      border-color: transparent;
    }

    .booking-cta--soft:hover {
      background: var(--color-primary-muted);
      color: var(--color-primary-hover);
    }

    .booking-cta__icon {
      display: inline-flex;
    }

    .booking-cta__arrow {
      font-size: 1.125em;
      transition: transform var(--transition-fast);
    }

    .booking-cta:hover .booking-cta__arrow {
      transform: translateX(-3px);
    }
  `,
})
export class BookingCtaComponent {
  readonly label = input<string>(APP_COPY.cta.bookSession);
  readonly fullWidth = input(false);
  readonly showCalendarIcon = input(false);
  readonly soft = input(false);
}
