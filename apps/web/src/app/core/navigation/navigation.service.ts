import { Injectable, computed, signal } from '@angular/core';

import { NAV_ITEMS } from './nav-items';
import type { NavItem } from './nav-items';
import type { Role } from './roles';

/**
 * Holds the navigation state as Signals.
 *
 * Until sign-in exists, the role is a local preview so each persona's
 * navigation can be checked. It never grants access on the server.
 */
@Injectable({ providedIn: 'root' })
export class NavigationService {
  private readonly role = signal<Role>('member');

  readonly currentRole = this.role.asReadonly();

  readonly items = computed<readonly NavItem[]>(() =>
    NAV_ITEMS.filter((item) => item.roles.includes(this.role())),
  );

  setRole(role: Role): void {
    this.role.set(role);
  }

  canSee(path: string): boolean {
    return this.items().some((item) => item.path === path);
  }
}
