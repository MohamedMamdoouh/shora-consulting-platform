export type PostLoginRedirect =
  { kind: 'url'; url: string } | { kind: 'commands'; commands: string[] };

export function sanitizeAuthReturnUrl(returnUrl?: string | null): string | null {
  if (!returnUrl || !returnUrl.startsWith('/') || returnUrl.startsWith('//')) {
    return null;
  }

  if (returnUrl.startsWith('/booking/') || returnUrl === '/dashboard') {
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
