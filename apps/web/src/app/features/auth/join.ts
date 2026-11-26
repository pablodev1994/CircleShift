import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { toApiError } from '../../core/api/api-error';
import { SessionService } from '../../core/auth/session.service';
import { applyServerError, fieldMessage } from '../../core/forms/form-errors';
import {
  PROFILE_LIMITS,
  browserTimeZone,
  ianaTimeZone,
  singleLine,
  timeZoneOptions,
  trimmedLength,
} from '../../core/profile/profile-rules';

/** Onboarding step 1: the account. Step 2 is the profile editor. */
@Component({
  selector: 'cs-join',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './join.html',
})
export class Join {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);

  protected readonly limits = PROFILE_LIMITS;
  protected readonly zones = timeZoneOptions();
  protected readonly submitting = signal(false);
  protected readonly formError = signal<string | null>(null);
  /** Bumped after each submit so OnPush templates re-read control errors set asynchronously. */
  protected readonly attempt = signal(0);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    password: [
      '',
      [Validators.required, Validators.minLength(PROFILE_LIMITS.passwordMin), Validators.maxLength(PROFILE_LIMITS.passwordMax)],
    ],
    displayName: [
      '',
      [Validators.required, trimmedLength(PROFILE_LIMITS.displayNameMin, PROFILE_LIMITS.displayNameMax), singleLine],
    ],
    timeZoneId: [browserTimeZone(), [Validators.required, ianaTimeZone(this.zones)]],
    confirmedAdult: [false, Validators.requiredTrue],
  });

  protected readonly message = fieldMessage;

  protected async submit(): Promise<void> {
    this.formError.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.attempt.update((n) => n + 1);
      this.focusFirstInvalid();
      return;
    }

    this.submitting.set(true);
    try {
      await this.session.register(this.form.getRawValue());
      await this.router.navigate(['/profile'], { queryParams: { welcome: 1 } });
    } catch (error) {
      const apiError = toApiError(error);
      if (!applyServerError(this.form, apiError)) {
        this.formError.set(apiError.message);
      }
      this.attempt.update((n) => n + 1);
      this.focusFirstInvalid();
    } finally {
      this.submitting.set(false);
    }
  }

  private focusFirstInvalid(): void {
    queueMicrotask(() => document.querySelector<HTMLElement>('cs-join .ng-invalid:not(form)')?.focus());
  }
}
