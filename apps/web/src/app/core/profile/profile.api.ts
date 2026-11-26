import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export const VISIBILITIES = ['hidden', 'members', 'public'] as const;

export type Visibility = (typeof VISIBILITIES)[number];

/** What other members (or visitors, for public profiles) see. */
export interface MemberProfile {
  readonly id: string;
  readonly displayName: string;
  readonly timeZoneId: string;
  readonly bio: string | null;
  readonly languages: readonly string[];
  readonly areaLabel: string | null;
}

/** The signed-in member's own profile, including who can see it. */
export interface OwnProfile extends MemberProfile {
  readonly visibility: Visibility;
  readonly updatedAt: string;
}

export interface ProfileUpdate {
  readonly displayName: string;
  readonly timeZoneId: string;
  readonly bio: string | null;
  readonly languages: readonly string[];
  readonly areaLabel: string | null;
  readonly visibility: Visibility;
}

export interface DirectoryPage {
  readonly items: readonly MemberProfile[];
  readonly total: number;
  readonly limit: number;
  readonly offset: number;
}

export interface DirectorySearch {
  readonly q?: string;
  readonly language?: string;
  readonly limit?: number;
  readonly offset?: number;
}

export interface BlockedMember {
  readonly memberId: string;
  readonly displayName: string;
  readonly blockedAt: string;
}

/** `/api/v1/members`: profiles, the directory and blocks. */
@Injectable({ providedIn: 'root' })
export class ProfileApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/members';

  getOwn(): Promise<OwnProfile> {
    return firstValueFrom(this.http.get<OwnProfile>(`${this.base}/me/profile`));
  }

  updateOwn(update: ProfileUpdate): Promise<OwnProfile> {
    return firstValueFrom(this.http.put<OwnProfile>(`${this.base}/me/profile`, update));
  }

  get(memberId: string): Promise<MemberProfile> {
    return firstValueFrom(this.http.get<MemberProfile>(`${this.base}/${encodeURIComponent(memberId)}`));
  }

  search(search: DirectorySearch): Promise<DirectoryPage> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(search)) {
      if (value !== undefined && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return firstValueFrom(this.http.get<DirectoryPage>(this.base, { params }));
  }

  listBlocks(): Promise<readonly BlockedMember[]> {
    return firstValueFrom(this.http.get<readonly BlockedMember[]>(`${this.base}/me/blocks`));
  }

  block(memberId: string): Promise<unknown> {
    return firstValueFrom(this.http.put(`${this.base}/me/blocks/${encodeURIComponent(memberId)}`, null));
  }

  unblock(memberId: string): Promise<unknown> {
    return firstValueFrom(this.http.delete(`${this.base}/me/blocks/${encodeURIComponent(memberId)}`));
  }
}
