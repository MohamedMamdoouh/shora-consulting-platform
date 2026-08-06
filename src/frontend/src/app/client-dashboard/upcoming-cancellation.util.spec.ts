import { MyBookingListItem } from '@contracts/booking';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  buildConsultantWhatsAppContactUrl,
  canReopenCancellationRequest,
  canSubmitCancellationRequest,
  getCancellationRequestDeadlineUtc,
  isCancellationPending,
  shouldShowDeclinedBanner,
  shouldShowWhatsAppFallback,
} from './upcoming-cancellation.util';

const NOW = '2026-08-06T10:00:00.000Z';

function booking(overrides: Partial<MyBookingListItem> = {}): MyBookingListItem {
  return {
    bookingId: 'booking-1',
    slotStartUtc: '2026-08-06T12:00:00.000Z',
    slotEndUtc: '2026-08-06T13:00:00.000Z',
    deliveryMethod: 'Chat',
    status: 'Confirmed',
    ...overrides,
  };
}

describe('upcoming cancellation utilities', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(NOW));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('uses server-provided cancellation deadlines before falling back to one hour before the slot', () => {
    expect(
      getCancellationRequestDeadlineUtc(
        booking({
          cancellationRequest: {
            status: 'Declined',
            reopenCount: 0,
            autoDeclineAtUtc: '2026-08-06T11:30:00.000Z',
          },
        }),
      ),
    ).toBe('2026-08-06T11:30:00.000Z');

    expect(getCancellationRequestDeadlineUtc(booking())).toBe('2026-08-06T11:00:00.000Z');
  });

  it('allows a fresh cancellation request only while a confirmed booking is inside the request window', () => {
    expect(canSubmitCancellationRequest(booking())).toBe(true);

    vi.setSystemTime(new Date('2026-08-06T11:00:00.000Z'));

    expect(canSubmitCancellationRequest(booking())).toBe(false);
    expect(canSubmitCancellationRequest(booking({ status: 'PendingApproval' }))).toBe(false);
  });

  it('allows a single declined request to be reopened before the deadline', () => {
    const declined = booking({
      cancellationRequest: {
        status: 'Declined',
        reopenCount: 0,
        autoDeclineAtUtc: '2026-08-06T11:00:00.000Z',
      },
    });

    expect(canReopenCancellationRequest(declined)).toBe(true);
    expect(canSubmitCancellationRequest(declined)).toBe(true);
    expect(
      canReopenCancellationRequest(
        booking({
          cancellationRequest: {
            status: 'Declined',
            reopenCount: 1,
            autoDeclineAtUtc: '2026-08-06T11:00:00.000Z',
          },
        }),
      ),
    ).toBe(false);
  });

  it('separates pending and unseen declined cancellation states', () => {
    expect(
      isCancellationPending(
        booking({
          status: 'CancellationRequested',
          cancellationRequest: {
            status: 'Pending',
            reopenCount: 0,
            autoDeclineAtUtc: '2026-08-06T11:00:00.000Z',
          },
        }),
      ),
    ).toBe(true);

    expect(
      shouldShowDeclinedBanner(
        booking({
          cancellationRequest: {
            status: 'AutoDeclined',
            reopenCount: 0,
            autoDeclineAtUtc: '2026-08-06T09:00:00.000Z',
          },
        }),
      ),
    ).toBe(true);
    expect(
      shouldShowDeclinedBanner(
        booking({
          cancellationRequest: {
            status: 'Declined',
            reopenCount: 0,
            clientDecisionSeenAtUtc: '2026-08-06T09:30:00.000Z',
            autoDeclineAtUtc: '2026-08-06T11:00:00.000Z',
          },
        }),
      ),
    ).toBe(false);
  });

  it('falls back to WhatsApp only when in-app cancellation is no longer actionable', () => {
    expect(shouldShowWhatsAppFallback(booking())).toBe(false);

    vi.setSystemTime(new Date('2026-08-06T11:00:01.000Z'));

    expect(shouldShowWhatsAppFallback(booking())).toBe(true);

    vi.setSystemTime(new Date(NOW));

    expect(
      shouldShowWhatsAppFallback(
        booking({
          cancellationRequest: {
            status: 'Declined',
            reopenCount: 1,
            clientDecisionSeenAtUtc: '2026-08-06T09:30:00.000Z',
            autoDeclineAtUtc: '2026-08-06T11:00:00.000Z',
          },
        }),
      ),
    ).toBe(true);
  });

  it('builds WhatsApp contact URLs from configured phone digits only', () => {
    expect(buildConsultantWhatsAppContactUrl('+20 10 1234 5678')).toBe(
      'https://wa.me/201012345678',
    );
    expect(buildConsultantWhatsAppContactUrl('   ')).toBeNull();
    expect(buildConsultantWhatsAppContactUrl(null)).toBeNull();
  });
});
