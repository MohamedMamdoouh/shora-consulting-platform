import { AsyncPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { SettingsService } from '../../core/settings/settings.service';
import { BookingCtaComponent } from '../shared/booking-cta.component';
import { CONSULTATION_TOPICS } from '../shared/topic.constants';

@Component({
  selector: 'app-services-page',
  imports: [AsyncPipe, BookingCtaComponent],
  templateUrl: './services-page.component.html',
  styleUrl: './services-page.component.scss',
})
export class ServicesPageComponent {
  private readonly settingsService = inject(SettingsService);

  readonly topics = CONSULTATION_TOPICS;
  readonly settings$ = this.settingsService.getPublicSettings();

  formatPrice(amount: number): string {
    return new Intl.NumberFormat('ar-EG', {
      style: 'currency',
      currency: 'EGP',
      maximumFractionDigits: 0,
    }).format(amount);
  }
}
