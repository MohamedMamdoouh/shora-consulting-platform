import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs';

@Component({
  selector: 'app-admin-section-placeholder',
  template: `
    <article class="admin-placeholder section">
      <h2>{{ title() }}</h2>
      <p>{{ message() }}</p>
    </article>
  `,
  styles: `
    .admin-placeholder {
      text-align: center;
    }

    .admin-placeholder h2 {
      margin-top: 0;
    }

    .admin-placeholder p {
      margin: 0;
      color: var(--color-text-muted);
    }
  `,
})
export class AdminSectionPlaceholderComponent {
  private readonly route = inject(ActivatedRoute);

  readonly title = toSignal(
    this.route.data.pipe(map((data) => (data['title'] as string | undefined) ?? 'قريباً')),
    { initialValue: 'قريباً' },
  );

  readonly message = toSignal(
    this.route.data.pipe(
      map((data) => (data['message'] as string | undefined) ?? 'هذا القسم سيتم تنفيذه قريباً.'),
    ),
    { initialValue: 'هذا القسم سيتم تنفيذه قريباً.' },
  );
}
