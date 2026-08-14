import { MyBookingListItem } from '@contracts/booking';

export type DashboardSectionState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; items: MyBookingListItem[]; totalCount: number };
