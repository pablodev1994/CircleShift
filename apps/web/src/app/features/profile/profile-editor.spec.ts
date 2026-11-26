import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { ProfileEditor } from './profile-editor';

describe('ProfileEditor', () => {
  let fixture: ComponentFixture<ProfileEditor>;
  let http: HttpTestingController;
  let el: HTMLElement;

  const own = {
    id: 'm1',
    displayName: 'Marisol',
    timeZoneId: 'Europe/Madrid',
    bio: null,
    languages: ['es'],
    areaLabel: null,
    visibility: 'hidden',
    updatedAt: '2026-11-26T10:00:00Z',
  };

  async function render(welcome?: string, blocks: unknown[] = []): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [ProfileEditor],
      providers: [provideZonelessChangeDetection(), provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    fixture = TestBed.createComponent(ProfileEditor);
    if (welcome) {
      fixture.componentRef.setInput('welcome', welcome);
    }
    http = TestBed.inject(HttpTestingController);
    el = fixture.nativeElement as HTMLElement;
    http.expectOne('/api/v1/members/me/profile').flush(own);
    http.expectOne('/api/v1/members/me/blocks').flush(blocks);
    await settle();
  }

  function input(id: string): HTMLInputElement {
    return el.querySelector(`#${id}`) as HTMLInputElement;
  }

  function type(id: string, value: string): void {
    const field = input(id);
    field.value = value;
    field.dispatchEvent(new Event('input'));
    field.dispatchEvent(new Event('blur'));
  }

  async function submit(): Promise<void> {
    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  /** Lets the promise chain after an HTTP flush finish, then renders. */
  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  }

  afterEach(() => http.verify());

  it('explains during onboarding that the profile starts hidden', async () => {
    await render('1');
    expect(el.querySelector('h1')?.textContent).toContain('Set up your profile');
    expect(el.querySelector('.notice')?.textContent).toContain('hidden');
    expect(input('visibility-hidden').checked).toBeTrue();
  });

  it('shows field errors and sends nothing while the form is invalid', async () => {
    await render();
    type('profile-name', ' a ');
    type('profile-languages', 'english');
    await submit();

    const errors = Array.from(el.querySelectorAll('.field-error')).map((node) => node.textContent?.trim());
    expect(errors).toContain('Display name must be 2–80 characters.');
    expect(errors.some((text) => text?.includes('not: english'))).toBeTrue();
    expect(input('profile-name').getAttribute('aria-invalid')).toBe('true');
    http.expectNone('/api/v1/members/me/profile');
  });

  it('saves normalized values and announces the new visibility', async () => {
    await render();
    type('profile-languages', 'ES, pt-br');
    type('profile-area', '  Madrid ');
    input('visibility-members').click();
    await submit();

    const request = http.expectOne((req) => req.method === 'PUT' && req.url === '/api/v1/members/me/profile');
    expect(request.request.body).toEqual({
      displayName: 'Marisol',
      timeZoneId: 'Europe/Madrid',
      bio: null,
      languages: ['es', 'pt-BR'],
      areaLabel: 'Madrid',
      visibility: 'members',
    });
    request.flush({ ...own, languages: ['es', 'pt-BR'], areaLabel: 'Madrid', visibility: 'members' });
    await settle();
    http.expectOne('/api/v1/auth/me').flush({ id: 'm1', displayName: 'Marisol', roles: [], permissions: [] });
    await settle();

    expect(el.querySelector('[role="status"]')?.textContent).toContain('Visibility: Members');
  });

  it('puts API validation errors next to the matching field', async () => {
    await render();
    await submit();

    http
      .expectOne((req) => req.method === 'PUT')
      .flush(
        { code: 'time_zone.invalid', message: 'Use an IANA time zone such as Europe/Madrid.', requestId: 'r1' },
        { status: 422, statusText: 'Unprocessable Entity' },
      );
    await settle();

    expect(el.querySelector('#profile-zone-error')?.textContent).toContain('Use an IANA time zone');
  });

  it('lists blocked members and unblocks them', async () => {
    await render(undefined, [{ memberId: 'm2', displayName: 'Bruno', blockedAt: '2026-11-26T10:00:00Z' }]);
    const button = el.querySelector('.blocked button') as HTMLButtonElement;
    expect(button.textContent).toContain('Unblock');

    button.click();
    const request = http.expectOne('/api/v1/members/me/blocks/m2');
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await settle();

    expect(el.querySelector('.blocked')?.textContent).toContain('You have not blocked anyone.');
  });
});
