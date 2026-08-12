import { Component, inject, OnInit } from '@angular/core';
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

  entry: ErrorCatalogEntry | null = null;
  notFound = false;
  loadError = '';

  async ngOnInit(): Promise<void> {
    const code = this.route.snapshot.paramMap.get('code');
    if (!code) {
      this.notFound = true;
      return;
    }

    try {
      this.entry = await firstValueFrom(this.errorReference.get(code));
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 404) {
        this.notFound = true;
        return;
      }

      this.loadError = 'تعذر تحميل تفاصيل كود الخطأ.';
    }
  }

  async copyTypeUri(): Promise<void> {
    if (!this.entry?.type) {
      return;
    }

    try {
      await navigator.clipboard.writeText(this.entry.type);
    } catch {
      // clipboard unavailable — ignore
    }
  }
}
