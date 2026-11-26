import type { OnChanges } from '@angular/core';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { toApiError } from '../../core/api/api-error';
import { SessionService } from '../../core/auth/session.service';
import type { MemberProfile as Profile } from '../../core/profile/profile.api';
import { ProfileApi } from '../../core/profile/profile.api';

/**
 * Someone's profile. Hidden, members-only (for visitors), blocked and missing
 * profiles all look the same here, because the API answers them the same way.
 */
@Component({
  selector: 'cs-member-profile',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (state() === 'loading') {
      <p aria-live="polite">Loading profile…</p>
    } @else if (state() === 'unavailable') {
      <section aria-labelledby="profile-missing-title">
        <h1 id="profile-missing-title">Profile not available</h1>
        <p>This profile does not exist or is not visible to you.</p>
        <a routerLink="/members">Back to members</a>
      </section>
    } @else if (profile(); as member) {
      <article aria-labelledby="member-name">
        <h1 id="member-name">{{ member.displayName }}</h1>
        <dl class="facts">
          <dt>Time zone</dt>
          <dd>{{ member.timeZoneId }}</dd>
          <dt>Languages</dt>
          <dd>{{ member.languages.join(', ') || 'None listed' }}</dd>
          @if (member.areaLabel) {
            <dt>Area</dt>
            <dd>{{ member.areaLabel }}</dd>
          }
        </dl>
        @if (member.bio) {
          <p class="bio">{{ member.bio }}</p>
        }

        @if (isSelf()) {
          <p><a routerLink="/profile">Edit your profile</a></p>
        } @else if (session.signedIn()) {
          <section class="block-panel" aria-labelledby="block-title">
            <h2 id="block-title">Block</h2>
            <p id="block-help">
              Neither of you will see the other's profile, posts or invitations. {{ member.displayName }} is not told.
              You can unblock from your profile page.
            </p>
            @if (error(); as message) {
              <p class="form-error" role="alert">{{ message }}</p>
            }
            @if (confirming()) {
              <button type="button" class="button-danger" (click)="block(member)" [disabled]="busy()">
                Yes, block {{ member.displayName }}
              </button>
              <button type="button" class="button-secondary" (click)="confirming.set(false)">Cancel</button>
            } @else {
              <button type="button" class="button-secondary" aria-describedby="block-help" (click)="confirming.set(true)">
                Block {{ member.displayName }}
              </button>
            }
          </section>
        }
      </article>
    }
  `,
})
export class MemberProfile implements OnChanges {
  private readonly api = inject(ProfileApi);
  private readonly router = inject(Router);
  protected readonly session = inject(SessionService);

  readonly id = input.required<string>();

  protected readonly profile = signal<Profile | null>(null);
  protected readonly state = signal<'loading' | 'ready' | 'unavailable'>('loading');
  protected readonly error = signal<string | null>(null);
  protected readonly confirming = signal(false);
  protected readonly busy = signal(false);
  protected readonly isSelf = computed(() => this.session.me()?.id === this.profile()?.id);

  ngOnChanges(): void {
    void this.load();
  }

  protected async block(member: Profile): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.api.block(member.id);
      await this.router.navigate(['/profile'], { fragment: 'blocked-title' });
    } catch (error) {
      this.error.set(toApiError(error).message);
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.state.set('loading');
    this.confirming.set(false);
    await this.session.load();
    try {
      this.profile.set(await this.api.get(this.id()));
      this.state.set('ready');
    } catch (error) {
      if (toApiError(error).status !== 404) {
        this.error.set(toApiError(error).message);
      }
      this.state.set('unavailable');
    }
  }
}
