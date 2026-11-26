import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { SessionService } from './core/auth/session.service';
import { NavigationService } from './core/navigation/navigation.service';
import { ROLES, ROLE_LABELS, isRole } from './core/navigation/roles';

@Component({
  selector: 'cs-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
})
export class App {
  protected readonly nav = inject(NavigationService);
  protected readonly session = inject(SessionService);
  private readonly router = inject(Router);

  protected readonly roles = ROLES;
  protected readonly roleLabels = ROLE_LABELS;
  protected readonly signingOut = signal(false);

  protected onRoleChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    if (!isRole(value)) {
      return;
    }
    this.nav.setRole(value);
    this.leaveHiddenPage();
  }

  protected async signOut(): Promise<void> {
    this.signingOut.set(true);
    try {
      await this.session.signOut();
    } finally {
      this.signingOut.set(false);
    }
    await this.router.navigateByUrl('/');
  }

  /** Leave a page the current role cannot see. */
  private leaveHiddenPage(): void {
    const path = this.router.url.split(/[?#]/)[0] ?? '/';
    if (this.nav.isHidden(path)) {
      void this.router.navigateByUrl('/');
    }
  }
}
