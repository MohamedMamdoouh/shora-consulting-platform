import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ErrorCatalogEntry } from '@contracts/error-catalog';
import { firstValueFrom } from 'rxjs';
import { ErrorReferenceService } from './error-reference.service';

@Component({
  selector: 'app-errors-detail-page',
  imports: [RouterLink],
  templateUrl: './errors-detail-page.component.html',
  styleUrl: './errors-detail-page.component.scss',
})
export class ErrorsDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly errorReference = inject(ErrorReferenceService);

  readonly entry = signal<ErrorCatalogEntry | null>(null);
  readonly notFound = signal(false);
  readonly loadError = signal('');

  async ngOnInit(): Promise<void> {
    const code = this.route.snapshot.paramMap.get('code');
    if (!code) {
      this.notFound.set(true);
      return;
    }

    try {
      this.entry.set(await firstValueFrom(this.errorReference.get(code)));
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 404) {
        this.notFound.set(true);
        return;
      }

      this.loadError.set('تعذر تحميل تفاصيل كود الخطأ.');
    }
  }

  async copyTypeUri(): Promise<void> {
    const currentEntry = this.entry();
    if (!currentEntry?.type) {
      return;
    }

    try {
      await navigator.clipboard.writeText(currentEntry.type);
    } catch {
      // clipboard unavailable — ignore
    }
  }
}
