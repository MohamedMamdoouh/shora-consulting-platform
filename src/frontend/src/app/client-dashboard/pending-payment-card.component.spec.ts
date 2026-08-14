import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { BookingService } from '../core/booking/booking.service';
import { ConfirmDialogService } from '../core/ui/confirm-dialog.service';
import { APP_COPY } from '../core/i18n/app-copy.constants';
import { PendingPaymentCardComponent } from './pending-payment-card.component';

describe('PendingPaymentCardComponent', () => {
  it('refreshes the dashboard after an embedded receipt upload succeeds', async () => {
    const result = vi.fn(
      async (options: { onComplete?: () => void }) => {
        options.onComplete?.();
      },
    );

    await TestBed.configureTestingModule({
      imports: [PendingPaymentCardComponent],
      providers: [
        provideRouter([]),
        { provide: BookingService, useValue: {} },
        { provide: ConfirmDialogService, useValue: { result } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(PendingPaymentCardComponent);
    const changed = vi.fn();
    const subscription = fixture.componentInstance.changed.subscribe(changed);

    await fixture.componentInstance.onReceiptSubmitted();

    expect(result).toHaveBeenCalledWith(
      expect.objectContaining({
        message: APP_COPY.client.receiptUploaded,
        redirectTo: ['/dashboard'],
        onComplete: expect.any(Function),
      }),
    );
    expect(changed).toHaveBeenCalledTimes(1);

    subscription.unsubscribe();
  });
});
