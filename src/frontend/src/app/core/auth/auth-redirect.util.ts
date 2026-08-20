export type PostLoginRedirect =
  { kind: 'url'; url: string } | { kind: 'commands'; commands: string[] };

const ALLOWED_DASHBOARD_RETURN_URLS = new Set([
  '/dashboard',
  '/dashboard/upcoming',
  '/dashboard/pending',
  '/dashboard/history',
]);

function isAllowedDashboardReturnUrl(returnUrl: string): boolean {
  return ALLOWED_DASHBOARD_RETURN_URLS.has(returnUrl);
}

export function sanitizeAuthReturnUrl(returnUrl?: string | null): string | null {
  if (!returnUrl || !returnUrl.startsWith('/') || returnUrl.startsWith('//')) {
    return null;
  }

  if (returnUrl.startsWith('/booking/') || isAllowedDashboardReturnUrl(returnUrl)) {
    return returnUrl;
  }

  return null;
}

export function resolvePostLoginRedirect(
  role: string,
  returnUrl?: string | null,
): PostLoginRedirect {
  const safeReturnUrl = sanitizeAuthReturnUrl(returnUrl);
  if (safeReturnUrl && role === 'Client') {
    return { kind: 'url', url: safeReturnUrl };
  }

  if (role === 'Admin') {
    return { kind: 'commands', commands: ['/admin'] };
  }

  return { kind: 'commands', commands: ['/dashboard'] };
}
