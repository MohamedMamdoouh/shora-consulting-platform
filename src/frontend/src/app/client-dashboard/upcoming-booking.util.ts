import { DeliveryMethod } from '@contracts/booking';
import { formatSlotRange, formatSlotStartTime } from './client-dashboard-slot.util';

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
  return `ستتلقى مكالمة في ${time} على الرقم ${contactPhone}.`;
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
