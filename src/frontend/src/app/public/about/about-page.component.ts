import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BookingCtaComponent } from '../shared/booking-cta.component';

@Component({
  selector: 'app-about-page',
  imports: [BookingCtaComponent, RouterLink],
  templateUrl: './about-page.component.html',
  styleUrl: './about-page.component.scss',
})
export class AboutPageComponent {}
