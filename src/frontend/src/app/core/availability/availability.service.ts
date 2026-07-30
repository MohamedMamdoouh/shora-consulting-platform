import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AvailabilityResponse } from '@contracts/availability';
import { environment } from '../../../environments/environment';
import { ApiCacheService } from '../api/api-cache.service';
import { availabilityRequest } from '../api/cache.config';

const AVAILABILITY_HORIZON_DAYS = 28;

@Injectable({ providedIn: 'root' })
export class AvailabilityService {
  private readonly cache = inject(ApiCacheService);

  getAvailability(from: Date = new Date(), to?: Date): Observable<AvailabilityResponse> {
    const rangeEnd =
      to ?? new Date(from.getTime() + AVAILABILITY_HORIZON_DAYS * 24 * 60 * 60 * 1000);
    const req = availabilityRequest(
      environment.apiBaseUrl,
      from.toISOString(),
      rangeEnd.toISOString(),
    );

    return this.cache.getCached<AvailabilityResponse>(req.url, req.ttlMs);
  }
}
