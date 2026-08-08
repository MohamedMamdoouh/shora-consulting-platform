import { describe, expect, it } from 'vitest';
import {
  resolvePhoneGuard,
  resolveReviewGuard,
  resolveSlotSelectedGuard,
} from './booking-guard-decisions.util';
import type { BookingGuardState } from './booking-guard-decisions.util';

describe('booking guard decisions', () => {
  it('allows the delivery step only after a slot is selected', () => {
    expect(resolveSlotSelectedGuard(true)).toBe(true);
    expect(resolveSlotSelectedGuard(false)).toBe('/booking/start');
  });

  it('routes phone-step requests according to the completed booking steps', () => {
    expect(resolvePhoneGuard(null)).toBe('/booking/start');
    expect(resolvePhoneGuard(state())).toBe('/booking/delivery');
    expect(resolvePhoneGuard(state({ deliveryMethod: 'Chat' }))).toBe('/booking/review');
    expect(resolvePhoneGuard(state({ deliveryMethod: 'VoiceCall' }))).toBe(true);
  });

  it('blocks review until voice-call bookings have a non-empty contact phone', () => {
    expect(resolveReviewGuard(null)).toBe('/booking/start');
    expect(resolveReviewGuard(state())).toBe('/booking/delivery');
    expect(resolveReviewGuard(state({ deliveryMethod: 'VoiceCall', contactPhone: '   ' }))).toBe(
      '/booking/phone',
    );
    expect(
      resolveReviewGuard(state({ deliveryMethod: 'VoiceCall', contactPhone: '01012345678' })),
    ).toBe(true);
    expect(resolveReviewGuard(state({ deliveryMethod: 'Chat', contactPhone: null }))).toBe(true);
  });
});

function state(overrides: Partial<BookingGuardState> = {}): BookingGuardState {
  return {
    slotId: 'slot-1',
    slotStartUtc: '2026-08-01T10:00:00.000Z',
    slotEndUtc: '2026-08-01T11:00:00.000Z',
    ...overrides,
  };
}
