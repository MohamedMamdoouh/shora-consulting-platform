import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import { SettingsService } from '../../core/settings/settings.service';
import { FeaturedSessionCardComponent } from '../../shared/components/featured-session-card.component';
import { FooterCtaBannerComponent } from '../../shared/components/footer-cta-banner.component';
import { TopicCardComponent } from '../../shared/components/topic-card.component';
import { CONSULTATION_TOPICS, HOW_IT_WORKS_STEPS } from '../shared/topic.constants';
import { BookingCtaComponent } from '../shared/booking-cta.component';

@Component({
  selector: 'app-home-page',
  imports: [
    BookingCtaComponent,
    FeaturedSessionCardComponent,
    TopicCardComponent,
    FooterCtaBannerComponent,
  ],
  templateUrl: './home-page.component.html',
  styleUrl: './home-page.component.scss',
})
export class HomePageComponent {
  private readonly settingsService = inject(SettingsService);

  protected readonly copy = APP_COPY;
  readonly topics = CONSULTATION_TOPICS;
  readonly steps = HOW_IT_WORKS_STEPS;
  readonly settings = toSignal(this.settingsService.getPublicSettings());
}
