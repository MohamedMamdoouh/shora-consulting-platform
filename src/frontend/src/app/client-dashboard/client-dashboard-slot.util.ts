import { MyBookingListItem } from '@contracts/booking';

export function formatSlotRange(item: Pick<MyBookingListItem, 'slotStartUtc' | 'slotEndUtc'>): string {
  const start = new Date(item.slotStartUtc);
  const end = new Date(item.slotEndUtc);

  const dateFormatter = new Intl.DateTimeFormat('ar-EG', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
  });
  const timeFormatter = new Intl.DateTimeFormat('ar-EG', {
    hour: 'numeric',
    minute: '2-digit',
  });

  return `${dateFormatter.format(start)} · ${timeFormatter.format(start)} – ${timeFormatter.format(end)}`;
}

export function formatSlotStartTime(slotStartUtc: string): string {
  return new Intl.DateTimeFormat('ar-EG', {
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(slotStartUtc));
}
