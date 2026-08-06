import { AdminBookingListItem } from '@contracts/booking';
import { describe, expect, it } from 'vitest';
import {
  canDirectCancelBooking,
  formatCancellationQueueNote,
  formatRemainingTime,
  isCancellationRequestPending,
} from './admin-cancellation-labels.util';
import { canRecordRefund, canRevokeRefund, hasBookingRowActions } from './admin-refund-labels.util';

const NOW_MS = Date.parse('2026-08-06T10:00:00.000Z');

function booking(overrides: Partial<AdminBookingListItem> = {}): AdminBookingListItem {
  return {
    bookingId: 'booking-1',
    clientDisplayName: 'Client One',
    deliveryMethod: 'Chat',
    slotStartUtc: '2026-08-06T12:00:00.000Z',
    slotEndUtc: '2026-08-06T13:00:00.000Z',
    status: 'Confirmed',
    refundDue: false,
    ...overrides,
  };
}

describe('admin cancellation and refund action labels', () => {
  it('detects only pending cancellation requests on cancellation-requested bookings', () => {
    expect(
      isCancellationRequestPending(
        booking({
          status: 'CancellationRequested',
          cancellationRequest: {
            status: 'Pending',
            requestedAtUtc: '2026-08-06T09:00:00.000Z',
            autoDeclineAtUtc: '2026-08-06T11:00:00.000Z',
          },
        }),
      ),
    ).toBe(true);

    expect(
      isCancellationRequestPending(
        booking({
          status: 'Confirmed',
          cancellationRequest: {
            status: 'Pending',
            requestedAtUtc: '2026-08-06T09:00:00.000Z',
            autoDeclineAtUtc: '2026-08-06T11:00:00.000Z',
          },
        }),
      ),
    ).toBe(false);
  });

  it('allows direct cancellation only for pending bookings or future confirmed sessions', () => {
    expect(canDirectCancelBooking(booking({ status: 'PendingPayment' }), NOW_MS)).toBe(true);
    expect(canDirectCancelBooking(booking({ status: 'PendingApproval' }), NOW_MS)).toBe(true);
    expect(canDirectCancelBooking(booking({ status: 'Confirmed' }), NOW_MS)).toBe(true);
    expect(
      canDirectCancelBooking(
        booking({ status: 'Confirmed' }),
        Date.parse('2026-08-06T12:00:00.000Z'),
      ),
    ).toBe(false);
    expect(canDirectCancelBooking(booking({ status: 'Completed' }), NOW_MS)).toBe(false);
  });

  it('formats remaining cancellation decision time and expired deadlines deterministically', () => {
    const deadline = new Date(NOW_MS + (2 * 3600 + 5 * 60 + 9) * 1000).toISOString();

    expect(formatRemainingTime(deadline, NOW_MS)).toContain('2');
    expect(formatRemainingTime(deadline, NOW_MS)).toContain('5');
    expect(formatRemainingTime(deadline, NOW_MS)).toContain('9');
    expect(formatRemainingTime('2026-08-06T09:59:59.000Z', NOW_MS)).not.toContain('2');
  });

  it('includes trimmed client reason when building the cancellation queue note', () => {
    const item = booking({
      status: 'CancellationRequested',
      cancellationRequest: {
        status: 'Pending',
        clientReason: '  Schedule conflict  ',
        requestedAtUtc: '2026-08-06T09:00:00.000Z',
        autoDeclineAtUtc: '2999-08-06T11:00:00.000Z',
      },
    });

    expect(formatCancellationQueueNote(item)).toContain('Schedule conflict');
    expect(formatCancellationQueueNote(booking())).toBeNull();
  });

  it('requires a persisted payment before recording or revoking refunds', () => {
    expect(canRecordRefund(booking({ refundDue: true, paymentId: 'payment-1' }))).toBe(true);
    expect(canRecordRefund(booking({ refundDue: true, paymentId: null }))).toBe(false);
    expect(
      canRevokeRefund(
        booking({
          status: 'Cancelled',
          paymentStatus: 'Refunded',
          paymentId: 'payment-1',
        }),
      ),
    ).toBe(true);
    expect(
      canRevokeRefund(
        booking({
          status: 'Confirmed',
          paymentStatus: 'Refunded',
          paymentId: 'payment-1',
        }),
      ),
    ).toBe(false);
  });

  it('shows row actions for reviewable, cancellable, and refundable bookings only', () => {
    expect(hasBookingRowActions(booking({ status: 'PendingApproval' }))).toBe(true);
    expect(
      hasBookingRowActions(
        booking({ status: 'Confirmed', slotStartUtc: '2999-08-06T12:00:00.000Z' }),
      ),
    ).toBe(true);
    expect(hasBookingRowActions(booking({ status: 'Completed' }))).toBe(false);
    expect(
      hasBookingRowActions(
        booking({ status: 'Cancelled', refundDue: true, paymentId: 'payment-1' }),
      ),
    ).toBe(true);
  });
});
