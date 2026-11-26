import { Injectable, computed, inject, signal } from '@angular/core';

import { SessionService } from '../auth/session.service';
import { NAV_ITEMS } from './nav-items';
import type { NavItem } from './nav-items';
import type { Role } from './roles';

/**
 * Holds the navigation state as Signals.
 *
 * Signed-in members get the navigation for their most privileged verified
 * role. Visitors can preview each persona's navigation with a local role,
 * which never grants anything on the server.
 */
@Injectable({ providedIn: 'root' })
export class NavigationService {
  private readonly session = inject(SessionService);
  private readonly preview = signal<Role>('member');

  /** Whether the role comes from the local preview (signed out) rather than the session. */
  readonly previewing = computed(() => !this.session.signedIn());

  readonly currentRole = computed<Role>(() => (this.previewing() ? this.preview() : this.session.role()));

  readonly items = computed<readonly NavItem[]>(() =>
    NAV_ITEMS.filter((item) => item.roles.includes(this.currentRole())),
  );

  /** Changes the preview role; ignored while signed in. */
  setRole(role: Role): void {
    this.preview.set(role);
  }

  canSee(path: string): boolean {
    return this.items().some((item) => item.path === path);
  }

  /** A navigation area the current role cannot see (pages outside the menu are never hidden). */
  isHidden(path: string): boolean {
    return NAV_ITEMS.some((item) => item.path === path) && !this.canSee(path);
  }
}
