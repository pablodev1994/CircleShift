import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { Router, UrlTree, provideRouter } from '@angular/router';

import { requireSignedIn, requireVisitor, safeReturnUrl } from './auth.guards';

describe('auth guards', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideZonelessChangeDetection(), provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  const run = (guard: typeof requireSignedIn, url = '/profile') =>
    TestBed.runInInjectionContext(() =>
      guard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
    ) as Promise<boolean | UrlTree>;

  const serialize = (tree: UrlTree) => TestBed.inject(Router).serializeUrl(tree);

  it('sends visitors to sign in with a return URL', async () => {
    const result = run(requireSignedIn, '/profile?welcome=1');
    http.expectOne('/api/v1/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });

    const tree = await result;
    expect(tree).toBeInstanceOf(UrlTree);
    expect(serialize(tree as UrlTree)).toBe('/sign-in?returnUrl=%2Fprofile%3Fwelcome%3D1');
  });

  it('lets members in and keeps them out of the join page', async () => {
    const signedIn = run(requireSignedIn);
    http.expectOne('/api/v1/auth/me').flush({ id: 'm1', displayName: 'Ana', roles: [], permissions: [] });
    expect(await signedIn).toBeTrue();

    const visitorOnly = await run(requireVisitor);
    expect(serialize(visitorOnly as UrlTree)).toBe('/profile');
  });

  it('only returns to paths inside the app', () => {
    expect(safeReturnUrl('/members?q=ana')).toBe('/members?q=ana');
    expect(safeReturnUrl('//evil.example/x')).toBe('/profile');
    expect(safeReturnUrl('/\\evil.example')).toBe('/profile');
    expect(safeReturnUrl('https://evil.example')).toBe('/profile');
    expect(safeReturnUrl(undefined)).toBe('/profile');
  });
});
