export const CACHE_TTL_MS = {
  settingsPublic: 300_000,
  availability: 30_000,
} as const;

export const CACHE_KEYS = {
  settingsPublic: 'settings:public',
  availabilityPrefix: 'availability:',
} as const;

export function availabilityCacheKey(from: string, to: string): string {
  return `${CACHE_KEYS.availabilityPrefix}${from}:${to}`;
}
