import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { Route, UrlSegment } from '@angular/router';
import { Router, UrlTree, provideRouter } from '@angular/router';

import { NavigationService } from './navigation.service';
import { requireRole } from './role.guard';

describe('requireRole', () => {
  const guard = requireRole('moderator', 'admin');
  const run = () =>
    TestBed.runInInjectionContext(() => guard({} as Route, [] as UrlSegment[]));

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideZonelessChangeDetection(), provideRouter([])],
    });
  });

  it('redirects members to home', () => {
    const result = run();
    expect(result).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/');
  });

  it('allows an allowed role', () => {
    TestBed.inject(NavigationService).setRole('moderator');
    expect(run()).toBeTrue();
  });
});
