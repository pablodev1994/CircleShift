import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { Role } from '../navigation/roles';

/** `GET /api/v1/auth/me`: the signed-in member with verified roles only. */
export interface Me {
  readonly id: string;
  readonly displayName: string;
  readonly roles: readonly string[];
  readonly permissions: readonly string[];
}

export interface Registration {
  readonly email: string;
  readonly password: string;
  readonly displayName: string;
  readonly timeZoneId: string;
  readonly confirmedAdult: boolean;
}

const ROLE_RANK: readonly Role[] = ['admin', 'moderator', 'coordinator'];

/**
 * The cookie session as Signals. HttpClient sends the `X-XSRF-TOKEN` header
 * from the `XSRF-TOKEN` cookie on unsafe requests; {@link csrf} makes sure
 * that cookie exists before the first one.
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  private readonly current = signal<Me | null>(null);
  private readonly loadedState = signal(false);
  private loading: Promise<Me | null> | null = null;

  readonly me = this.current.asReadonly();
  readonly loaded = this.loadedState.asReadonly();
  readonly signedIn = computed(() => this.current() !== null);

  /** The most privileged verified role, or `member`. Navigation only; the API decides access. */
  readonly role = computed<Role>(() => {
    const roles = this.current()?.roles ?? [];
    return ROLE_RANK.find((role) => roles.includes(role)) ?? 'member';
  });

  /** Loads the session once; later calls share the same request. */
  load(): Promise<Me | null> {
    this.loading ??= this.fetchMe();
    return this.loading;
  }

  /** Re-reads the session, for example after the display name changed. */
  refresh(): Promise<Me | null> {
    this.loading = this.fetchMe();
    return this.loading;
  }

  async register(registration: Registration): Promise<Me> {
    await this.csrf();
    return this.accept(await firstValueFrom(this.http.post<Me>('/api/v1/auth/register', registration)));
  }

  async signIn(email: string, password: string): Promise<Me> {
    await this.csrf();
    return this.accept(await firstValueFrom(this.http.post<Me>('/api/v1/auth/sign-in', { email, password })));
  }

  async signOut(): Promise<void> {
    await this.csrf();
    try {
      await firstValueFrom(this.http.post('/api/v1/auth/sign-out', {}));
    } finally {
      this.current.set(null);
      this.loading = Promise.resolve(null);
    }
  }

  private csrf(): Promise<unknown> {
    return firstValueFrom(this.http.get('/api/v1/auth/csrf'));
  }

  private accept(me: Me): Me {
    this.current.set(me);
    this.loadedState.set(true);
    this.loading = Promise.resolve(me);
    return me;
  }

  private async fetchMe(): Promise<Me | null> {
    try {
      return this.accept(await firstValueFrom(this.http.get<Me>('/api/v1/auth/me')));
    } catch (error) {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        // The API is down or failing: behave as signed out rather than blocking the app.
        console.warn('Could not load the session', error);
      }
      this.current.set(null);
      this.loadedState.set(true);
      return null;
    }
  }
}
