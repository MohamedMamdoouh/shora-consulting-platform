import { ErrorCodes } from '@contracts/error-codes';

export function isSlotUnavailableError(code: string | undefined): boolean {
  return code === ErrorCodes.Booking.SlotUnavailable;
}
