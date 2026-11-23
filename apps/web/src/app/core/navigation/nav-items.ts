import type { Role } from './roles';

export interface NavItem {
  readonly path: string;
  readonly label: string;
  /** Roles that see this entry. The API enforces access; this only hides links. */
  readonly roles: readonly Role[];
}

const EVERYONE: readonly Role[] = ['member', 'coordinator', 'moderator', 'admin'];

export const NAV_ITEMS: readonly NavItem[] = [
  { path: '/', label: 'Home', roles: EVERYONE },
  { path: '/board', label: 'Offers & requests', roles: EVERYONE },
  { path: '/matches', label: 'Matches', roles: EVERYONE },
  { path: '/exchanges', label: 'Exchanges', roles: EVERYONE },
  { path: '/wallet', label: 'Wallet', roles: EVERYONE },
  { path: '/moderation', label: 'Moderation', roles: ['moderator', 'admin'] },
  { path: '/admin', label: 'Admin', roles: ['admin'] },
];
