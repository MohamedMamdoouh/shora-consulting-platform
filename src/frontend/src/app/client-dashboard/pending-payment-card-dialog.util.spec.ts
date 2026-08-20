import { describe, expect, it, vi } from 'vitest';
import { APP_COPY } from '../core/i18n/app-copy.constants';
import { buildReceiptUploadedResult } from './pending-payment-card-dialog.util';

describe('buildReceiptUploadedResult', () => {
  it('sends the client to pending bookings after receipt upload', async () => {
    const refreshDashboard = vi.fn();
    const result = buildReceiptUploadedResult(APP_COPY.client.receiptUploaded, refreshDashboard);

    expect(result).toEqual(
      expect.objectContaining({
        message: APP_COPY.client.receiptUploaded,
        redirectTo: ['/dashboard', 'pending'],
      }),
    );

    await result.onComplete?.();

    expect(refreshDashboard).toHaveBeenCalledTimes(1);
  });
});
