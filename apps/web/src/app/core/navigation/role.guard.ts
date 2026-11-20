import { inject } from '@angular/core';
import type { CanMatchFn } from '@angular/router';
import { Router } from '@angular/router';

import { NavigationService } from './navigation.service';
import type { Role } from './roles';

/**
 * Route guard for role-specific areas. It is a UX convenience only:
 * the API checks permissions on every request.
 */
export function requireRole(...allowed: readonly Role[]): CanMatchFn {
  return () => {
    const nav = inject(NavigationService);
    return allowed.includes(nav.currentRole()) || inject(Router).parseUrl('/');
  };
}
