import { ErrorCodes } from '@contracts/error-codes';
import { describe, expect, it } from 'vitest';
import { isSlotUnavailableError } from './booking-error.util';

describe('isSlotUnavailableError', () => {
  it('returns true for the slot-unavailable code', () => {
    expect(isSlotUnavailableError(ErrorCodes.Booking.SlotUnavailable)).toBe(true);
  });

  it('returns false for other codes or undefined', () => {
    expect(isSlotUnavailableError(ErrorCodes.Booking.NotFound)).toBe(false);
    expect(isSlotUnavailableError(undefined)).toBe(false);
  });
});
