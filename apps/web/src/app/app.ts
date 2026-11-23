import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

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
  private readonly router = inject(Router);

  protected readonly roles = ROLES;
  protected readonly roleLabels = ROLE_LABELS;

  protected onRoleChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    if (!isRole(value)) {
      return;
    }
    this.nav.setRole(value);
    // Leave a page the new role cannot see.
    const path = this.router.url.split(/[?#]/)[0] ?? '/';
    if (!this.nav.canSee(path)) {
      void this.router.navigateByUrl('/');
    }
  }
}
