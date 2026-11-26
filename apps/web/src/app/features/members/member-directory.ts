import type { OnChanges } from '@angular/core';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { toApiError } from '../../core/api/api-error';
import { SessionService } from '../../core/auth/session.service';
import type { DirectoryPage } from '../../core/profile/profile.api';
import { ProfileApi } from '../../core/profile/profile.api';

const PAGE_SIZE = 20;

/** Visible profiles. Search state lives in the URL, so results can be shared and survive reloads. */
@Component({
  selector: 'cs-member-directory',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="directory-title">
      <h1 id="directory-title">Members</h1>
      @if (!session.signedIn()) {
        <p class="notice" role="note">You are seeing public profiles only. <a routerLink="/sign-in">Sign in</a> to see more.</p>
      }

      <form class="search" role="search" [formGroup]="form" (ngSubmit)="search()">
        <div class="field">
          <label for="directory-q">Name</label>
          <input id="directory-q" type="search" formControlName="q" maxlength="80" />
        </div>
        <div class="field">
          <label for="directory-language">Language tag</label>
          <input id="directory-language" type="text" formControlName="language" maxlength="16" placeholder="es" />
        </div>
        <button class="button" type="submit">Search</button>
      </form>

      @if (error(); as message) {
        <p class="form-error" role="alert">{{ message }}</p>
      } @else if (page(); as result) {
        <p role="status">{{ result.total }} {{ result.total === 1 ? 'member' : 'members' }} found</p>
        <ul class="plain-list members">
          @for (member of result.items; track member.id) {
            <li>
              <a [routerLink]="['/members', member.id]">{{ member.displayName }}</a>
              <span class="field-hint">
                {{ member.languages.join(', ') || 'No languages listed' }}@if (member.areaLabel) { · {{ member.areaLabel }}}
              </span>
            </li>
          }
        </ul>
        @if (result.total > result.limit) {
          <nav class="pager" aria-label="Pages">
            <button type="button" class="button-secondary" [disabled]="result.offset === 0" (click)="go(result.offset - result.limit)">
              Previous
            </button>
            <span>Page {{ currentPage() }} of {{ pageCount() }}</span>
            <button
              type="button"
              class="button-secondary"
              [disabled]="result.offset + result.limit >= result.total"
              (click)="go(result.offset + result.limit)"
            >
              Next
            </button>
          </nav>
        }
      } @else {
        <p aria-live="polite">Loading members…</p>
      }
    </section>
  `,
})
export class MemberDirectory implements OnChanges {
  private readonly api = inject(ProfileApi);
  private readonly router = inject(Router);
  protected readonly session = inject(SessionService);

  readonly q = input<string>();
  readonly language = input<string>();
  readonly offset = input<string>();

  protected readonly page = signal<DirectoryPage | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly currentPage = computed(() => {
    const page = this.page();
    return page ? Math.floor(page.offset / page.limit) + 1 : 1;
  });
  protected readonly pageCount = computed(() => {
    const page = this.page();
    return page ? Math.max(1, Math.ceil(page.total / page.limit)) : 1;
  });

  protected readonly form = inject(NonNullableFormBuilder).group({ q: '', language: '' });

  /** Runs whenever the query string changes (component input binding). */
  ngOnChanges(): void {
    this.form.setValue({ q: this.q() ?? '', language: this.language() ?? '' });
    void this.load();
  }

  protected search(): void {
    const { q, language } = this.form.getRawValue();
    void this.router.navigate([], { queryParams: { q: q.trim() || null, language: language.trim() || null, offset: null } });
  }

  protected go(offset: number): void {
    void this.router.navigate([], { queryParams: { offset: offset > 0 ? offset : null }, queryParamsHandling: 'merge' });
  }

  private async load(): Promise<void> {
    this.error.set(null);
    this.page.set(null);
    const offset = Number.parseInt(this.offset() ?? '0', 10);
    try {
      this.page.set(
        await this.api.search({
          q: this.q()?.trim(),
          language: this.language()?.trim(),
          limit: PAGE_SIZE,
          offset: Number.isFinite(offset) && offset > 0 ? offset : 0,
        }),
      );
    } catch (error) {
      this.error.set(toApiError(error).message);
    }
  }
}
