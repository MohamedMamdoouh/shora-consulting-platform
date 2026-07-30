import { describe, expect, it, beforeEach } from 'vitest';
import {
  applyDeliveryMethod,
  readBookingFlowState,
  writeBookingFlowState,
} from './booking-flow-state.util';

describe('booking flow state storage', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('persists slot selection in sessionStorage', () => {
    writeBookingFlowState({
      slotId: 'slot-1',
      slotStartUtc: '2026-08-01T10:00:00.000Z',
      slotEndUtc: '2026-08-01T11:00:00.000Z',
    });

    expect(readBookingFlowState()).toEqual({
      slotId: 'slot-1',
      slotStartUtc: '2026-08-01T10:00:00.000Z',
      slotEndUtc: '2026-08-01T11:00:00.000Z',
    });
  });

  it('clears contact phone when delivery method is Chat', () => {
    writeBookingFlowState({
      slotId: 'slot-1',
      slotStartUtc: '2026-08-01T10:00:00.000Z',
      slotEndUtc: '2026-08-01T11:00:00.000Z',
      deliveryMethod: 'VoiceCall',
      contactPhone: '01012345678',
    });

    const next = applyDeliveryMethod(readBookingFlowState()!, 'Chat');
    writeBookingFlowState(next);

    expect(readBookingFlowState()?.deliveryMethod).toBe('Chat');
    expect(readBookingFlowState()?.contactPhone).toBeNull();
  });
});
