import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-booking-cta',
  imports: [RouterLink],
  template: `<a routerLink="/booking/start" class="btn">احجز جلسة</a>`,
})
export class BookingCtaComponent {}
