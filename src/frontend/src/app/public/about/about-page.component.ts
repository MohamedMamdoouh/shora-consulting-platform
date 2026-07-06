import { Component } from '@angular/core';

@Component({
  selector: 'app-about-page',
  template: `
    <section class="page">
      <!-- TODO: replace with real bio content -->
      <h1>عن المستشار</h1>
      <p>نص مؤقت عن المستشار — سيتم استبداله بالسيرة الحقيقية لاحقاً.</p>
    </section>
  `,
  styles: `
    .page {
      max-width: 40rem;
      margin: 0 auto;
      padding: var(--space-xl);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
    }
  `
})
export class AboutPageComponent {}
