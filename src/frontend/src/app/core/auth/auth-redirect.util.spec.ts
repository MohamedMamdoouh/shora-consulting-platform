import { describe, expect, it } from 'vitest';
import { resolvePostLoginRedirect, sanitizeAuthReturnUrl } from './auth-redirect.util';

describe('auth redirect policy', () => {
  it('accepts only same-site booking and dashboard return URLs', () => {
    expect(sanitizeAuthReturnUrl('/booking/review')).toBe('/booking/review');
    expect(sanitizeAuthReturnUrl('/booking/payment/booking-1')).toBe('/booking/payment/booking-1');
    expect(sanitizeAuthReturnUrl('/dashboard')).toBe('/dashboard');
    expect(sanitizeAuthReturnUrl('/dashboard/pending')).toBe('/dashboard/pending');
    expect(sanitizeAuthReturnUrl('/dashboard/upcoming')).toBe('/dashboard/upcoming');
    expect(sanitizeAuthReturnUrl('/dashboard/history')).toBe('/dashboard/history');

    expect(sanitizeAuthReturnUrl('/dashboard/../admin')).toBeNull();
    expect(sanitizeAuthReturnUrl('/admin')).toBeNull();
    expect(sanitizeAuthReturnUrl('https://example.com/booking/review')).toBeNull();
    expect(sanitizeAuthReturnUrl('//example.com/booking/review')).toBeNull();
    expect(sanitizeAuthReturnUrl('booking/review')).toBeNull();
    expect(sanitizeAuthReturnUrl(null)).toBeNull();
  });

  it('returns clients to a sanitized booking URL after login', () => {
    expect(resolvePostLoginRedirect('Client', '/booking/review')).toEqual({
      kind: 'url',
      url: '/booking/review',
    });
  });

  it('returns clients to dashboard child routes after login', () => {
    expect(resolvePostLoginRedirect('Client', '/dashboard/pending')).toEqual({
      kind: 'url',
      url: '/dashboard/pending',
    });
  });

  it('falls back to the dashboard when a client return URL is unsafe', () => {
    expect(resolvePostLoginRedirect('Client', 'https://example.com/booking/review')).toEqual({
      kind: 'commands',
      commands: ['/dashboard'],
    });
  });

  it('ignores booking return URLs for admins', () => {
    expect(resolvePostLoginRedirect('Admin', '/booking/review')).toEqual({
      kind: 'commands',
      commands: ['/admin'],
    });
  });
});
