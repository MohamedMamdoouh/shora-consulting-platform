export interface AvailabilitySlot {
  id: string;
  startTimeUtc: string;
  endTimeUtc: string;
}

export interface AvailabilityResponse {
  slots: AvailabilitySlot[];
}

export const DAY_OF_WEEK_NAMES = [
  'Sunday',
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
] as const;

export type DayOfWeekName = (typeof DAY_OF_WEEK_NAMES)[number];
export type DayOfWeekNumber = 0 | 1 | 2 | 3 | 4 | 5 | 6;

/** API serializes System.DayOfWeek as a name; older payloads may send 0–6. */
export type DayOfWeek = DayOfWeekName | DayOfWeekNumber;

export interface AvailabilityWindow {
  id: string;
  dayOfWeek: DayOfWeek;
  startTime: string;
  endTime: string;
  isActive: boolean;
}

export interface CreateAvailabilityWindowRequest {
  dayOfWeek: DayOfWeekName;
  startTime: string;
  endTime: string;
  isActive?: boolean;
}

export interface UpdateAvailabilityWindowRequest {
  dayOfWeek: DayOfWeekName;
  startTime: string;
  endTime: string;
  isActive: boolean;
}

export interface BlockedDate {
  id: string;
  startUtc: string;
  endUtc: string;
  reason: string | null;
}

export interface CreateBlockedDateRequest {
  startUtc: string;
  endUtc: string;
  reason?: string | null;
}
