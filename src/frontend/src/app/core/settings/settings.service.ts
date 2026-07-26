import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PublicSettings } from '@contracts/settings';
import { environment } from '../../../environments/environment';
import { ApiCacheService } from '../api/api-cache.service';
import { settingsPublicRequest } from '../api/cache.config';

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly cache = inject(ApiCacheService);

  getPublicSettings(): Observable<PublicSettings> {
    const req = settingsPublicRequest(environment.apiBaseUrl);
    return this.cache.getCached<PublicSettings>(req.url, req.ttlMs);
  }
}
