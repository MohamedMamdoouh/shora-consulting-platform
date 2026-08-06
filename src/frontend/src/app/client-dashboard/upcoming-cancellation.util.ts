import { MyBookingListItem } from '@contracts/booking';

const DEFAULT_CANCELLATION_AUTO_DECLINE_HOURS = 1;

export function getCancellationRequestDeadlineUtc(item: MyBookingListItem): string {
  if (item.cancellationRequest?.autoDeclineAtUtc) {
    return item.cancellationRequest.autoDeclineAtUtc;
  }

  const deadlineMs =
    new Date(item.slotStartUtc).getTime() -
    DEFAULT_CANCELLATION_AUTO_DECLINE_HOURS * 60 * 60 * 1000;

  return new Date(deadlineMs).toISOString();
}

export function isWithinCancellationRequestWindow(item: MyBookingListItem): boolean {
  return Date.now() < new Date(getCancellationRequestDeadlineUtc(item)).getTime();
}

export function isCancellationPending(item: MyBookingListItem): boolean {
  return item.status === 'CancellationRequested' && item.cancellationRequest?.status === 'Pending';
}

export function shouldShowDeclinedBanner(item: MyBookingListItem): boolean {
  const meta = item.cancellationRequest;

  if (!meta || item.status !== 'Confirmed') {
    return false;
  }

  return (
    (meta.status === 'Declined' || meta.status === 'AutoDeclined') && !meta.clientDecisionSeenAtUtc
  );
}

export function canReopenCancellationRequest(item: MyBookingListItem): boolean {
  const meta = item.cancellationRequest;

  if (!meta || item.status !== 'Confirmed') {
    return false;
  }

  return (
    meta.status === 'Declined' && meta.reopenCount < 1 && isWithinCancellationRequestWindow(item)
  );
}

export function canSubmitCancellationRequest(item: MyBookingListItem): boolean {
  if (!isWithinCancellationRequestWindow(item) || item.status !== 'Confirmed') {
    return false;
  }

  const meta = item.cancellationRequest;

  if (!meta) {
    return true;
  }

  return canReopenCancellationRequest(item);
}

export function shouldShowWhatsAppFallback(item: MyBookingListItem): boolean {
  if (
    isCancellationPending(item) ||
    canSubmitCancellationRequest(item) ||
    shouldShowDeclinedBanner(item)
  ) {
    return false;
  }

  const meta = item.cancellationRequest;

  if (!isWithinCancellationRequestWindow(item)) {
    return true;
  }

  if (!meta) {
    return false;
  }

  return meta.status === 'AutoDeclined' || (meta.status === 'Declined' && meta.reopenCount >= 1);
}

export function buildConsultantWhatsAppContactUrl(
  consultantWhatsAppNumber: string | null | undefined,
): string | null {
  if (!consultantWhatsAppNumber) {
    return null;
  }

  const digits = consultantWhatsAppNumber.replace(/\D/g, '');
  return digits ? `https://wa.me/${digits}` : null;
}
