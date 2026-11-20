import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { NavigationService } from './navigation.service';

describe('NavigationService', () => {
  let nav: NavigationService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection()] });
    nav = TestBed.inject(NavigationService);
  });

  const paths = () => nav.items().map((item) => item.path);

  it('starts as a member and hides moderation and admin', () => {
    expect(nav.currentRole()).toBe('member');
    expect(paths()).toEqual(['/', '/board', '/matches', '/exchanges', '/wallet']);
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
