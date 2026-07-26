export const CACHE_TTL_MS = {
  settingsPublic: 300_000,
  availability: 30_000,
} as const;

export interface CacheRequest {
  url: string;
  ttlMs: number;
}

export function settingsPublicRequest(apiBaseUrl: string): CacheRequest {
  return {
    url: `${apiBaseUrl}/settings/public`,
    ttlMs: CACHE_TTL_MS.settingsPublic,
  };
}

export function availabilityRequest(apiBaseUrl: string, from: string, to: string): CacheRequest {
  const params = new URLSearchParams({ from, to });
  return {
    url: `${apiBaseUrl}/availability?${params}`,
    ttlMs: CACHE_TTL_MS.availability,
  };
}
