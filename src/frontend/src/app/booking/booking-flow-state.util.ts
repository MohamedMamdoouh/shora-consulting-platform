import { DeliveryMethod } from '@contracts/booking';

const STORAGE_KEY = 'shora.booking.flow';

export interface BookingFlowState {
  slotId: string;
  slotStartUtc: string;
  slotEndUtc: string;
  deliveryMethod?: DeliveryMethod;
  contactPhone?: string | null;
}

export function readBookingFlowState(): BookingFlowState | null {
  const raw = sessionStorage.getItem(STORAGE_KEY);
  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as BookingFlowState;
    if (!parsed.slotId || !parsed.slotStartUtc || !parsed.slotEndUtc) {
      return null;
    }

    return parsed;
  } catch {
    return null;
  }
}

export function writeBookingFlowState(state: BookingFlowState): void {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state));
}

export function clearBookingFlowState(): void {
  sessionStorage.removeItem(STORAGE_KEY);
}

export function applyDeliveryMethod(
  state: BookingFlowState,
  deliveryMethod: DeliveryMethod,
): BookingFlowState {
  return {
    ...state,
    deliveryMethod,
    contactPhone: deliveryMethod === 'Chat' ? null : state.contactPhone,
  };
}
