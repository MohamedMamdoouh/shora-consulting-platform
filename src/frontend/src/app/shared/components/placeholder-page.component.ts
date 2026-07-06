import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

@Component({
  selector: 'app-placeholder-page',
  template: `
    <section class="placeholder-page">
      <h1>{{ title }}</h1>
      <p>{{ message }}</p>
    </section>
  `,
  styles: `
    .placeholder-page {
      max-width: 40rem;
      margin: 0 auto;
      padding: var(--space-xl);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
    }
  `
})
export class PlaceholderPageComponent {
  private readonly route = inject(ActivatedRoute);

  title = this.route.snapshot.data['title'] ?? 'قريباً';
  message = this.route.snapshot.data['message'] ?? 'هذا القسم سيتم تنفيذه في المواصفة التالية.';
}
