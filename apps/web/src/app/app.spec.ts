import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { App } from './app';
import { routes } from './app.routes';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideZonelessChangeDetection(), provideRouter(routes)],
    }).compileComponents();
  });

  function render() {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  function navLabels(el: HTMLElement): string[] {
    return Array.from(el.querySelectorAll('nav[aria-label="Main"] a')).map(
      (a) => a.textContent?.trim() ?? '',
    );
  }

  it('renders member navigation by default', () => {
    const labels = navLabels(render());
    expect(labels).toContain('Wallet');
    expect(labels).not.toContain('Moderation');
  });

  it('shows the moderation link after switching the preview role', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const select = el.querySelector('select') as HTMLSelectElement;

    select.value = 'moderator';
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(navLabels(el)).toContain('Moderation');
  });

  it('states the adult-only demo scope in the footer', () => {
    expect(render().querySelector('footer')?.textContent).toContain('adults only (18+)');
  });
});
