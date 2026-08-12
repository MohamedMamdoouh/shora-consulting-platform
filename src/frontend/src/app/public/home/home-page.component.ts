import { Component } from '@angular/core';
import { CONSULTATION_TOPICS, HOW_IT_WORKS_STEPS } from '../shared/topic.constants';
import { BookingCtaComponent } from '../shared/booking-cta.component';
import { BrandLogoComponent } from '../../shared/components/brand-logo.component';

@Component({
  selector: 'app-home-page',
  imports: [BookingCtaComponent, BrandLogoComponent],
  templateUrl: './home-page.component.html',
  styleUrl: './home-page.component.scss',
})
export class HomePageComponent {
  readonly topics = CONSULTATION_TOPICS;
  readonly steps = HOW_IT_WORKS_STEPS;
}
