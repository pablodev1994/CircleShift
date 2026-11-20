import type { Routes } from '@angular/router';

import { requireRole } from './core/navigation/role.guard';

const placeholder = () =>
  import('./features/placeholder/placeholder').then((m) => m.Placeholder);

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    title: 'CircleShift',
    loadComponent: () => import('./features/home/home').then((m) => m.Home),
  },
  {
    path: 'board',
    title: 'Offers & requests · CircleShift',
    loadComponent: placeholder,
    data: {
      heading: 'Offers & requests',
      summary: 'Search skills members offer and request, and publish your own posts.',
    },
  },
  {
    path: 'matches',
    title: 'Matches · CircleShift',
    loadComponent: placeholder,
    data: {
      heading: 'Matches',
      summary: 'Members whose skills complement yours, with the reasons for each match.',
    },
  },
  {
    path: 'exchanges',
    title: 'Exchanges · CircleShift',
    loadComponent: placeholder,
    data: {
      heading: 'Exchanges',
      summary: 'Invitations, scheduled sessions and completion confirmations.',
    },
  },
  {
    path: 'wallet',
    title: 'Wallet · CircleShift',
    loadComponent: placeholder,
    data: {
      heading: 'Wallet',
      summary: 'Your time-credit balance and every entry that changed it.',
    },
  },
  {
    path: 'moderation',
    title: 'Moderation · CircleShift',
    canMatch: [requireRole('moderator', 'admin')],
    loadComponent: placeholder,
    data: {
      heading: 'Moderation',
      summary: 'Reported content waiting for a human decision, and appeals.',
    },
  },
  {
    path: 'admin',
    title: 'Admin · CircleShift',
    canMatch: [requireRole('admin')],
    loadComponent: placeholder,
    data: {
      heading: 'Admin',
      summary: 'Community policy versions, the skill catalog and role assignments.',
    },
  },
  {
    path: '**',
    title: 'Not found · CircleShift',
    loadComponent: () => import('./shared/not-found/not-found').then((m) => m.NotFound),
  },
];
