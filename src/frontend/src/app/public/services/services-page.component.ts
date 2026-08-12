import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import { SettingsService } from '../../core/settings/settings.service';
import { FeaturedSessionCardComponent } from '../../shared/components/featured-session-card.component';
import { FooterCtaBannerComponent } from '../../shared/components/footer-cta-banner.component';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { TopicCardComponent } from '../../shared/components/topic-card.component';
import { CONSULTATION_TOPICS } from '../shared/topic.constants';

@Component({
  selector: 'app-services-page',
  imports: [
    PageHeaderComponent,
    FeaturedSessionCardComponent,
    TopicCardComponent,
    FooterCtaBannerComponent,
  ],
  templateUrl: './services-page.component.html',
  styleUrl: './services-page.component.scss',
})
export class ServicesPageComponent {
  private readonly settingsService = inject(SettingsService);

  protected readonly copy = APP_COPY;
  readonly topics = CONSULTATION_TOPICS;
  readonly settings = toSignal(this.settingsService.getPublicSettings());
}
