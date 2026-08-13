import { Component } from '@angular/core';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import { FooterCtaBannerComponent } from '../../shared/components/footer-cta-banner.component';
import { PageHeaderComponent } from '../../shared/components/page-header.component';

@Component({
  selector: 'app-about-page',
  imports: [PageHeaderComponent, FooterCtaBannerComponent],
  templateUrl: './about-page.component.html',
  styleUrl: './about-page.component.scss',
})
export class AboutPageComponent {
  protected readonly copy = APP_COPY;
}
