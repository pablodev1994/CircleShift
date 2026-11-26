import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { SessionService } from './session.service';

describe('SessionService', () => {
  let session: SessionService;
  let http: HttpTestingController;

  const me = { id: 'm1', displayName: 'Camila', roles: ['coordinator', 'admin'], permissions: ['roles.manage'] };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting()],
    });
    session = TestBed.inject(SessionService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('treats 401 from /me as signed out', async () => {
    const loading = session.load();
    http.expectOne('/api/v1/auth/me').flush({ code: 'auth.required' }, { status: 401, statusText: 'Unauthorized' });

    expect(await loading).toBeNull();
    expect(session.signedIn()).toBeFalse();
    expect(session.loaded()).toBeTrue();
    expect(session.role()).toBe('member');
  });

  it('loads once and exposes the most privileged verified role', async () => {
    const first = session.load();
    const second = session.load();
    http.expectOne('/api/v1/auth/me').flush(me);

    expect(await first).toEqual(me);
    expect(await second).toEqual(me);
    expect(session.role()).toBe('admin');
  });

  it('fetches a CSRF cookie before registering', async () => {
    const registering = session.register({
      email: 'new@example.org',
      password: 'correct horse battery',
      displayName: 'New Member',
      timeZoneId: 'Europe/Madrid',
      confirmedAdult: true,
    });

    http.expectOne('/api/v1/auth/csrf').flush(null, { status: 204, statusText: 'No Content' });
    await Promise.resolve();
    const register = http.expectOne('/api/v1/auth/register');
    expect(register.request.method).toBe('POST');
    expect(register.request.body.confirmedAdult).toBeTrue();
    register.flush({ ...me, roles: [] });

    await registering;
    expect(session.signedIn()).toBeTrue();
    expect(session.role()).toBe('member');
  });

  it('clears the session on sign-out even if the request fails', async () => {
    const signingIn = session.signIn('camila@example.org', 'correct horse battery');
    http.expectOne('/api/v1/auth/csrf').flush(null);
    await Promise.resolve();
    http.expectOne('/api/v1/auth/sign-in').flush(me);
    await signingIn;

    const signingOut = session.signOut();
    http.expectOne('/api/v1/auth/csrf').flush(null);
    await Promise.resolve();
    http.expectOne('/api/v1/auth/sign-out').flush({ code: 'x' }, { status: 500, statusText: 'Server Error' });

    await expectAsync(signingOut).toBeRejected();
    expect(session.signedIn()).toBeFalse();
    expect(await session.load()).toBeNull();
  });
});
