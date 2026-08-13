import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminEarningsResponse } from '@contracts/earnings';
import { firstValueFrom } from 'rxjs';
import { readApiError } from '../../core/api/api-error.util';
import { AdminEarningsService } from '../../core/admin/admin-earnings.service';
import {
  localDateEndExclusiveToUtcIso,
  localDateStartToUtcIso,
} from '../bookings/admin-bookings-labels.util';
import { formatEarningsAmount, formatEarningsCount } from './admin-earnings-labels.util';

type PageState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; earnings: AdminEarningsResponse };

@Component({
  selector: 'app-admin-earnings-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './admin-earnings-page.component.html',
  styleUrl: './admin-earnings-page.component.scss',
})
export class AdminEarningsPageComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly adminEarningsService = inject(AdminEarningsService);

  readonly pageState = signal<PageState>({ status: 'loading' });

  readonly formatEarningsAmount = formatEarningsAmount;
  readonly formatEarningsCount = formatEarningsCount;

  readonly filtersForm = this.fb.nonNullable.group({
    fromDate: this.fb.control<string | null>(null),
    toDate: this.fb.control<string | null>(null),
  });

  ngOnInit(): void {
    void this.loadEarnings();
  }

  async applyFilters(): Promise<void> {
    await this.loadEarnings();
  }

  async loadEarnings(): Promise<void> {
    this.pageState.set({ status: 'loading' });

    const values = this.filtersForm.getRawValue();

    try {
      const earnings = await firstValueFrom(
        this.adminEarningsService.getEarnings({
          from: values.fromDate ? localDateStartToUtcIso(values.fromDate) : undefined,
          to: values.toDate ? localDateEndExclusiveToUtcIso(values.toDate) : undefined,
        }),
      );
      this.pageState.set({ status: 'ready', earnings });
    } catch (error) {
      this.pageState.set({
        status: 'error',
        message: readApiError(error, 'تعذر تحميل ملخص الأرباح. حاول مرة أخرى.'),
      });
    }
  }
}
