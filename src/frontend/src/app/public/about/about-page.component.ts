import { Component } from '@angular/core';
import { BookingCtaComponent } from '../shared/booking-cta.component';

@Component({
  selector: 'app-about-page',
  imports: [BookingCtaComponent],
  templateUrl: './about-page.component.html',
  styleUrl: './about-page.component.scss',
})
export class AboutPageComponent {}
