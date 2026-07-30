import { Injectable } from '@angular/core';
import { DeliveryMethod } from '@contracts/booking';
import {
  applyDeliveryMethod,
  BookingFlowState,
  clearBookingFlowState,
  readBookingFlowState,
  writeBookingFlowState,
} from './booking-flow-state.util';

export type { BookingFlowState };

@Injectable({ providedIn: 'root' })
export class BookingFlowStateService {
  getState(): BookingFlowState | null {
    return readBookingFlowState();
  }

  hasSlot(): boolean {
    return !!readBookingFlowState()?.slotId;
  }

  setSlot(slot: { id: string; startTimeUtc: string; endTimeUtc: string }): void {
    writeBookingFlowState({
      slotId: slot.id,
      slotStartUtc: slot.startTimeUtc,
      slotEndUtc: slot.endTimeUtc,
    });
  }

  setDeliveryMethod(deliveryMethod: DeliveryMethod): void {
    const current = readBookingFlowState();
    if (!current?.slotId) {
      return;
    }

    writeBookingFlowState(applyDeliveryMethod(current, deliveryMethod));
  }

  setContactPhone(contactPhone: string): void {
    const current = readBookingFlowState();
    if (!current?.slotId) {
      return;
    }

    writeBookingFlowState({
      ...current,
      contactPhone,
    });
  }

  clear(): void {
    clearBookingFlowState();
  }
}
