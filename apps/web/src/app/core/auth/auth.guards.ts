import { inject } from '@angular/core';
import type { CanActivateFn } from '@angular/router';
import { Router } from '@angular/router';

import { SessionService } from './session.service';

/** Only same-app paths: `/profile` yes, `//evil.example` or `https://…` no. */
export function safeReturnUrl(value: unknown, fallback = '/profile'): string {
  return typeof value === 'string' && value.startsWith('/') && !value.startsWith('//') && !value.startsWith('/\\')
    ? value
    : fallback;
}

/** Pages that need a session send visitors to sign in and back. The API still checks every request. */
export const requireSignedIn: CanActivateFn = async (_route, state) => {
  const session = inject(SessionService);
  const router = inject(Router);
  await session.load();
  return session.signedIn() || router.createUrlTree(['/sign-in'], { queryParams: { returnUrl: state.url } });
};

/** Sign-in and join pages are for visitors; members go to their profile. */
export const requireVisitor: CanActivateFn = async () => {
  const session = inject(SessionService);
  const router = inject(Router);
  await session.load();
  return !session.signedIn() || router.createUrlTree(['/profile']);
};
