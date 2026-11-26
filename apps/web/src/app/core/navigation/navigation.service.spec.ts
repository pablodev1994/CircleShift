import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { SessionService } from '../auth/session.service';
import { NavigationService } from './navigation.service';

describe('NavigationService', () => {
  let nav: NavigationService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting()] });
    nav = TestBed.inject(NavigationService);
  });

  const paths = () => nav.items().map((item) => item.path);

  it('starts as a member and hides moderation and admin', () => {
    expect(nav.currentRole()).toBe('member');
    expect(paths()).toEqual(['/', '/members', '/board', '/matches', '/exchanges', '/wallet']);
  });

  it('shows moderation to moderators but not admin', () => {
    nav.setRole('moderator');
    expect(paths()).toContain('/moderation');
    expect(paths()).not.toContain('/admin');
  });

  it('shows every area to platform admins', () => {
    nav.setRole('admin');
    expect(paths()).toContain('/moderation');
    expect(paths()).toContain('/admin');
  });

  it('recomputes visibility when the role changes back', () => {
    nav.setRole('admin');
    nav.setRole('coordinator');
    expect(nav.canSee('/admin')).toBeFalse();
    expect(nav.canSee('/wallet')).toBeTrue();
  });
});

describe('NavigationService when signed in', () => {
  it('uses the verified role from the session and ignores the preview', async () => {
    TestBed.configureTestingModule({
      providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting()],
    });
    const nav = TestBed.inject(NavigationService);
    const session = TestBed.inject(SessionService);
    const http = TestBed.inject(HttpTestingController);

    const loading = session.load();
    http.expectOne('/api/v1/auth/me').flush({ id: 'm1', displayName: 'Andrés', roles: ['moderator'], permissions: [] });
    await loading;

    nav.setRole('admin');
    expect(nav.previewing()).toBeFalse();
    expect(nav.currentRole()).toBe('moderator');
    expect(nav.canSee('/moderation')).toBeTrue();
    expect(nav.canSee('/admin')).toBeFalse();
  });

  it('never hides pages outside the menu', () => {
    TestBed.configureTestingModule({
      providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting()],
    });
    const nav = TestBed.inject(NavigationService);
    expect(nav.isHidden('/profile')).toBeFalse();
    expect(nav.isHidden('/admin')).toBeTrue();
  });
});
