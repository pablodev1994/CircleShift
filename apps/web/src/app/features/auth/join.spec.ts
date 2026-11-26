import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { Join } from './join';

describe('Join', () => {
  let fixture: ComponentFixture<Join>;
  let http: HttpTestingController;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Join],
      providers: [provideZonelessChangeDetection(), provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    fixture = TestBed.createComponent(Join);
    http = TestBed.inject(HttpTestingController);
    el = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  });

  /** Lets the promise chain after an HTTP flush finish, then renders. */
  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  }

  afterEach(() => http.verify());

  function fill(id: string, value: string): void {
    const field = el.querySelector(`#${id}`) as HTMLInputElement;
    field.value = value;
    field.dispatchEvent(new Event('input'));
  }

  async function submit(): Promise<void> {
    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  it('prefills a time zone and requires the adult confirmation', async () => {
    expect((el.querySelector('#join-zone') as HTMLInputElement).value).not.toBe('');

    fill('join-email', 'new@example.org');
    fill('join-password', 'correct horse battery');
    fill('join-name', 'New Member');
    await submit();

    expect(el.querySelector('#join-adult-error')?.textContent).toContain('Adult confirmation is required.');
    http.expectNone('/api/v1/auth/csrf');
  });

  it('registers and continues to the profile step', async () => {
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fill('join-email', 'new@example.org');
    fill('join-password', 'correct horse battery');
    fill('join-name', 'New Member');
    fill('join-zone', 'America/Bogota');
    (el.querySelector('#join-adult') as HTMLInputElement).click();
    await submit();

    http.expectOne('/api/v1/auth/csrf').flush(null);
    await settle();
    const register = http.expectOne('/api/v1/auth/register');
    expect(register.request.body).toEqual({
      email: 'new@example.org',
      password: 'correct horse battery',
      displayName: 'New Member',
      timeZoneId: 'America/Bogota',
      confirmedAdult: true,
    });
    register.flush({ id: 'm1', displayName: 'New Member', roles: [], permissions: [] });
    await settle();

    expect(navigate).toHaveBeenCalledWith(['/profile'], { queryParams: { welcome: 1 } });
  });

  it('shows a taken email next to the email field', async () => {
    fill('join-email', 'taken@example.org');
    fill('join-password', 'correct horse battery');
    fill('join-name', 'New Member');
    (el.querySelector('#join-adult') as HTMLInputElement).click();
    await submit();

    http.expectOne('/api/v1/auth/csrf').flush(null);
    await settle();
    http
      .expectOne('/api/v1/auth/register')
      .flush(
        { code: 'account.email_taken', message: 'An account with this email already exists.', requestId: 'r1' },
        { status: 409, statusText: 'Conflict' },
      );
    await settle();

    expect(el.querySelector('#join-email-error')?.textContent).toContain('already exists');
  });
});
