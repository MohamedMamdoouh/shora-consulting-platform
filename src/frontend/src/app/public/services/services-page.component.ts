import { Component } from '@angular/core';

@Component({
  selector: 'app-services-page',
  template: `
    <section class="page">
      <h1>الخدمات</h1>
      <p>جلسة واحدة بسعر ثابت — التفاصيل ستُعرض ديناميكياً من واجهة الإعدادات في المواصفة 03.</p>
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
export class ServicesPageComponent {}
