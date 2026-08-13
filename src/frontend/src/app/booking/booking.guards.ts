import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { BookingFlowStateService } from './booking-flow-state.service';
import {
  resolveAdminBookingGuard,
  resolvePhoneGuard,
  resolveReviewGuard,
  resolveSlotSelectedGuard,
} from './booking-guard-decisions.util';
import type { BookingGuardDecision } from './booking-guard-decisions.util';

export const blockAdminBookingGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return toGuardResult(resolveAdminBookingGuard(auth.getCurrentUser()?.role), router);
};

export const bookingSlotSelectedGuard: CanActivateFn = () => {
  const flow = inject(BookingFlowStateService);
  const router = inject(Router);

  return toGuardResult(resolveSlotSelectedGuard(flow.hasSlot()), router);
};

export const bookingPhoneGuard: CanActivateFn = () => {
  const flow = inject(BookingFlowStateService);
  const router = inject(Router);

  return toGuardResult(resolvePhoneGuard(flow.getState()), router);
};

export const bookingReviewGuard: CanActivateFn = () => {
  const flow = inject(BookingFlowStateService);
  const router = inject(Router);

  return toGuardResult(resolveReviewGuard(flow.getState()), router);
};

function toGuardResult(decision: BookingGuardDecision, router: Router) {
  return decision === true ? true : router.createUrlTree([decision]);
}
