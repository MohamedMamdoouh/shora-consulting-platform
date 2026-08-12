import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BrandLogoComponent } from '../../shared/components/brand-logo.component';
import { BookingCtaComponent } from '../shared/booking-cta.component';

@Component({
  selector: 'app-about-page',
  imports: [BrandLogoComponent, BookingCtaComponent, RouterLink],
  templateUrl: './about-page.component.html',
  styleUrl: './about-page.component.scss',
})
export class AboutPageComponent {}
