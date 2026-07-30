import { TestBed } from '@angular/core/testing';
import { CanActivateFn, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { BookingFlowState, BookingFlowStateService } from './booking-flow-state.service';
import {
  bookingPhoneGuard,
  bookingReviewGuard,
  bookingSlotSelectedGuard,
} from './booking.guards';

describe('booking flow guards', () => {
  let flow: {
    hasSlot: ReturnType<typeof vi.fn<() => boolean>>;
    getState: ReturnType<typeof vi.fn<() => BookingFlowState | null>>;
  };
  let router: Router;

  beforeEach(() => {
    flow = {
      hasSlot: vi.fn(),
      getState: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: BookingFlowStateService, useValue: flow },
      ],
    });

    router = TestBed.inject(Router);
  });

  function runGuard(guard: CanActivateFn, url = '/booking/review') {
    return TestBed.runInInjectionContext(() =>
      guard({} as never, { url } as RouterStateSnapshot),
    );
  }

  function expectRedirect(result: unknown, expectedUrl: string): void {
    expect(result).not.toBe(true);
    expect(router.serializeUrl(result as UrlTree)).toBe(expectedUrl);
  }

  it('allows the delivery step only after a slot is selected', () => {
    flow.hasSlot.mockReturnValue(true);

    expect(runGuard(bookingSlotSelectedGuard, '/booking/delivery')).toBe(true);
  });

  it('redirects delivery requests without a selected slot back to the start step', () => {
    flow.hasSlot.mockReturnValue(false);

    expectRedirect(runGuard(bookingSlotSelectedGuard, '/booking/delivery'), '/booking/start');
  });

  it('sends phone-step requests without a slot back to the start step', () => {
    flow.getState.mockReturnValue(null);

    expectRedirect(runGuard(bookingPhoneGuard, '/booking/phone'), '/booking/start');
  });

  it('sends phone-step requests without a delivery method back to delivery selection', () => {
    flow.getState.mockReturnValue(state());

    expectRedirect(runGuard(bookingPhoneGuard, '/booking/phone'), '/booking/delivery');
  });

  it('skips the phone step when chat delivery is selected', () => {
    flow.getState.mockReturnValue(state({ deliveryMethod: 'Chat' }));

    expectRedirect(runGuard(bookingPhoneGuard, '/booking/phone'), '/booking/review');
  });

  it('allows the phone step when voice-call delivery is selected', () => {
    flow.getState.mockReturnValue(state({ deliveryMethod: 'VoiceCall' }));

    expect(runGuard(bookingPhoneGuard, '/booking/phone')).toBe(true);
  });

  it('blocks review until voice-call bookings have a non-empty contact phone', () => {
    flow.getState.mockReturnValue(
      state({ deliveryMethod: 'VoiceCall', contactPhone: '   ' }),
    );

    expectRedirect(runGuard(bookingReviewGuard), '/booking/phone');
  });

  it('allows review for chat bookings without a contact phone', () => {
    flow.getState.mockReturnValue(state({ deliveryMethod: 'Chat', contactPhone: null }));

    expect(runGuard(bookingReviewGuard)).toBe(true);
  });
});

function state(overrides: Partial<BookingFlowState> = {}): BookingFlowState {
  return {
    slotId: 'slot-1',
    slotStartUtc: '2026-08-01T10:00:00.000Z',
    slotEndUtc: '2026-08-01T11:00:00.000Z',
    ...overrides,
  };
}
