import { describe, expect, it } from 'vitest';
import {
  bookingStatusDataAttr,
  formatBookingStatus,
  formatCancellationNote,
} from './admin-bookings-labels.util';

describe('admin booking status and notes', () => {
  it('shows refunded instead of cancelled when the payment was refunded', () => {
    expect(formatBookingStatus('Cancelled', 'Refunded')).toBe('مستردة');
    expect(bookingStatusDataAttr('Cancelled', 'Refunded')).toBe('Refunded');
  });

  it('keeps the cancelled label until a refund is recorded', () => {
    expect(formatBookingStatus('Cancelled')).toBe('ملغية');
    expect(formatBookingStatus('Cancelled', 'Approved')).toBe('ملغية');
    expect(bookingStatusDataAttr('Cancelled', 'Approved')).toBe('Cancelled');
  });

  it('does not treat a non-cancelled booking as refunded', () => {
    expect(formatBookingStatus('Confirmed', 'Refunded')).toBe('مؤكدة');
    expect(bookingStatusDataAttr('Confirmed', 'Refunded')).toBe('Confirmed');
  });

  it('adds refunded copy to cancelled notes after a refund is recorded', () => {
    expect(formatCancellationNote('Cancelled', 'Cancelled by you', false, null, 'Refunded')).toBe(
      'تم الإلغاء من طرف العميل · تم استرداد المبلغ',
    );
  });

  it('adds refund-due copy while the refund is still outstanding', () => {
    expect(formatCancellationNote('Cancelled', 'Cancelled by you', true, null, 'Approved')).toBe(
      'تم الإلغاء من طرف العميل · استرداد مستحق',
    );
  });
});
