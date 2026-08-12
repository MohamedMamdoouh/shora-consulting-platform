import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-booking-cta',
  imports: [RouterLink],
  template: `
    <a routerLink="/booking/start" class="btn btn--lg btn--gradient booking-cta">
      {{ label() }}
      <span class="booking-cta__arrow" aria-hidden="true">←</span>
    </a>
  `,
  styles: `
    .booking-cta {
      gap: var(--space-sm);
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
  readonly label = input('احجز جلسة');
}
