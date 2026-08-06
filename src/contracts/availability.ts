export interface AvailabilitySlot {
  id: string;
  startTimeUtc: string;
  endTimeUtc: string;
}

export interface AvailabilityResponse {
  slots: AvailabilitySlot[];
}

export type DayOfWeek = 0 | 1 | 2 | 3 | 4 | 5 | 6;

export interface AvailabilityWindow {
  id: string;
  dayOfWeek: DayOfWeek;
  startTime: string;
  endTime: string;
  isActive: boolean;
}

export interface CreateAvailabilityWindowRequest {
  dayOfWeek: DayOfWeek;
  startTime: string;
  endTime: string;
  isActive?: boolean;
}

export interface UpdateAvailabilityWindowRequest {
  dayOfWeek: DayOfWeek;
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
