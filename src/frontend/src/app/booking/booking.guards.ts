import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { BookingFlowStateService } from './booking-flow-state.service';

export const bookingSlotSelectedGuard: CanActivateFn = () => {
  const flow = inject(BookingFlowStateService);
  const router = inject(Router);

  if (flow.hasSlot()) {
    return true;
  }

  return router.createUrlTree(['/booking/start']);
};

export const bookingPhoneGuard: CanActivateFn = () => {
  const flow = inject(BookingFlowStateService);
  const router = inject(Router);
  const state = flow.getState();

  if (!state?.slotId) {
    return router.createUrlTree(['/booking/start']);
  }

  if (!state.deliveryMethod) {
    return router.createUrlTree(['/booking/delivery']);
  }

  if (state.deliveryMethod === 'Chat') {
    return router.createUrlTree(['/booking/review']);
  }

  return true;
};

export const bookingReviewGuard: CanActivateFn = () => {
  const flow = inject(BookingFlowStateService);
  const router = inject(Router);
  const state = flow.getState();

  if (!state?.slotId) {
    return router.createUrlTree(['/booking/start']);
  }

  if (!state.deliveryMethod) {
    return router.createUrlTree(['/booking/delivery']);
  }

  if (state.deliveryMethod === 'VoiceCall' && !state.contactPhone?.trim()) {
    return router.createUrlTree(['/booking/phone']);
  }

  return true;
};
