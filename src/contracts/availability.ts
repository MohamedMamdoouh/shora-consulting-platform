export interface AvailabilitySlot {
  id: string;
  startTimeUtc: string;
  endTimeUtc: string;
}

export interface AvailabilityResponse {
  slots: AvailabilitySlot[];
}
