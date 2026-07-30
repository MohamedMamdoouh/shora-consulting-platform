import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from './auth.service';

describe('AuthService redirects', () => {
  let service: AuthService;
  let router: {
    navigate: ReturnType<typeof vi.fn<(commands: unknown[], extras?: unknown) => Promise<boolean>>>;
    navigateByUrl: ReturnType<typeof vi.fn<(url: string) => Promise<boolean>>>;
    url: string;
  };

  beforeEach(() => {
    router = {
      navigate: vi.fn().mockResolvedValue(true),
      navigateByUrl: vi.fn().mockResolvedValue(true),
      url: '/',
    };

    TestBed.configureTestingModule({
      providers: [
        AuthService,
        { provide: HttpClient, useValue: { post: vi.fn() } },
        { provide: Router, useValue: router },
      ],
    });

    service = TestBed.inject(AuthService);
  });

  it('accepts only same-site booking and dashboard return URLs', () => {
    expect(service.sanitizeReturnUrl('/booking/review')).toBe('/booking/review');
    expect(service.sanitizeReturnUrl('/booking/payment/booking-1')).toBe(
      '/booking/payment/booking-1',
    );
    expect(service.sanitizeReturnUrl('/dashboard')).toBe('/dashboard');

    expect(service.sanitizeReturnUrl('/admin')).toBeNull();
    expect(service.sanitizeReturnUrl('https://example.com/booking/review')).toBeNull();
    expect(service.sanitizeReturnUrl('//example.com/booking/review')).toBeNull();
    expect(service.sanitizeReturnUrl('booking/review')).toBeNull();
    expect(service.sanitizeReturnUrl(null)).toBeNull();
  });

  it('returns clients to a sanitized booking URL after login', async () => {
    await service.redirectAfterLogin('Client', '/booking/review');

    expect(router.navigateByUrl).toHaveBeenCalledWith('/booking/review');
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('falls back to the dashboard when a client return URL is unsafe', async () => {
    await service.redirectAfterLogin('Client', 'https://example.com/booking/review');

    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('ignores booking return URLs for admins', async () => {
    await service.redirectAfterLogin('Admin', '/booking/review');

    expect(router.navigate).toHaveBeenCalledWith(['/admin']);
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });
});
