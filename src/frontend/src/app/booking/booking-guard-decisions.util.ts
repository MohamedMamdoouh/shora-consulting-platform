import type { DeliveryMethod } from '@contracts/booking';

export interface BookingGuardState {
  slotId?: string | null;
  slotStartUtc?: string;
  slotEndUtc?: string;
  deliveryMethod?: DeliveryMethod;
  contactPhone?: string | null;
}

export type BookingGuardDecision =
  true | '/booking/start' | '/booking/delivery' | '/booking/phone' | '/booking/review';

export function resolveSlotSelectedGuard(hasSlot: boolean): BookingGuardDecision {
  return hasSlot ? true : '/booking/start';
}

export function resolvePhoneGuard(state: BookingGuardState | null): BookingGuardDecision {
  if (!state?.slotId) {
    return '/booking/start';
  }

  if (!state.deliveryMethod) {
    return '/booking/delivery';
  }

  if (state.deliveryMethod === 'Chat') {
    return '/booking/review';
  }

  return true;
}

export function resolveReviewGuard(state: BookingGuardState | null): BookingGuardDecision {
  if (!state?.slotId) {
    return '/booking/start';
  }

  if (!state.deliveryMethod) {
    return '/booking/delivery';
  }

  if (state.deliveryMethod === 'VoiceCall' && !state.contactPhone?.trim()) {
    return '/booking/phone';
  }

  return true;
}
