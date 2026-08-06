export interface MessageResponse {
  message: string;
}

export interface HealthResponse {
  status: string;
  timestampUtc: string;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  code?: string;
  errors?: Record<string, string[]>;
  conflictingBookingIds?: string[];
}
