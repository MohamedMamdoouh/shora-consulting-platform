import { describe, expect, it } from 'vitest';
import {
  formatPastBookingCancellation,
  localizeCancellationDetail,
  localizeCancellationReasonLabel,
} from './client-dashboard-labels.util';

describe('cancellation labels', () => {
  it('tells the client who cancelled the booking', () => {
    expect(localizeCancellationReasonLabel('Cancelled by you')).toBe('تم الإلغاء من طرفك');
    expect(localizeCancellationReasonLabel('Cancelled by the instructor')).toBe(
      'تم الإلغاء من طرف المستشار',
    );
    expect(localizeCancellationReasonLabel('Cancelled by the system')).toBe(
      'تم الإلغاء تلقائيًا من النظام',
    );
  });

  it('uses the admin perspective for the same actor labels', () => {
    expect(localizeCancellationReasonLabel('Cancelled by you', 'admin')).toBe(
      'تم الإلغاء من طرف العميل',
    );
    expect(localizeCancellationReasonLabel('Cancelled by the instructor', 'admin')).toBe(
      'تم الإلغاء من طرفك',
    );
  });

  it('localizes known system details and keeps free-text reasons', () => {
    expect(localizeCancellationDetail('Receipt not uploaded in time')).toBe(
      'لم يتم رفع الإيصال في الوقت المحدد',
    );
    expect(localizeCancellationDetail('  تعارض في الموعد  ')).toBe('تعارض في الموعد');
  });

  it('builds past-booking notes with actor, detail, and refund', () => {
    expect(
      formatPastBookingCancellation({
        status: 'Cancelled',
        cancellationReasonLabel: 'Cancelled by you',
        cancellationDetail: 'Schedule conflict',
        refundLabel: 'Refunded',
      }),
    ).toEqual({
      cancelledBy: 'تم الإلغاء من طرفك',
      detail: 'Schedule conflict',
      refund: 'تم استرداد المبلغ',
    });

    expect(
      formatPastBookingCancellation({
        status: 'Completed',
        cancellationReasonLabel: 'Cancelled by you',
      }),
    ).toBeNull();
  });
});
