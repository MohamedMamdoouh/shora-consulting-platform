import { DeliveryMethod } from '@contracts/booking';
import { formatSlotRange, formatSlotStartTime } from './client-dashboard-slot.util';

import { APP_COPY } from '../core/i18n/app-copy.constants';

export function formatDeliveryMethodLabel(method: DeliveryMethod): string {
  switch (method) {
    case 'VoiceCall':
      return 'مكالمة صوتية';
    case 'Chat':
      return 'محادثة واتساب';
    default:
      return method;
  }
}

export function formatVoiceCallInstruction(
  contactPhone: string | null | undefined,
  slotStartUtc: string,
): string | null {
  if (!contactPhone) {
    return null;
  }

  const time = formatSlotStartTime(slotStartUtc);
  return APP_COPY.client.voiceCallInstruction(time, contactPhone);
}

export function buildWhatsAppChatUrl(
  consultantWhatsAppNumber: string,
  slotStartUtc: string,
  slotEndUtc: string,
): string {
  const digits = consultantWhatsAppNumber.replace(/\D/g, '');
  const slotLabel = formatSlotRange({ slotStartUtc, slotEndUtc });
  const text = encodeURIComponent(`مرحبًا، لدي جلسة محجوزة في ${slotLabel}`);
  return `https://wa.me/${digits}?text=${text}`;
}
