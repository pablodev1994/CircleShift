import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { toApiError } from '../../core/api/api-error';
import { safeReturnUrl } from '../../core/auth/auth.guards';
import { SessionService } from '../../core/auth/session.service';
import { fieldMessage } from '../../core/forms/form-errors';

@Component({
  selector: 'cs-sign-in',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="form-page" aria-labelledby="sign-in-title">
      <h1 id="sign-in-title">Sign in</h1>

      @if (formError(); as error) {
        <p class="form-error" role="alert">{{ error }}</p>
      }

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate [attr.data-attempt]="attempt()">
        <div class="field">
          <label for="sign-in-email">Email</label>
          <input id="sign-in-email" type="email" formControlName="email" autocomplete="username" />
          @if (message(form.controls.email, 'Email'); as error) {
            <p class="field-error" role="alert">{{ error }}</p>
          }
        </div>
        <div class="field">
          <label for="sign-in-password">Password</label>
          <input id="sign-in-password" type="password" formControlName="password" autocomplete="current-password" />
          @if (message(form.controls.password, 'Password'); as error) {
            <p class="field-error" role="alert">{{ error }}</p>
          }
        </div>
        <button class="button" type="submit" [disabled]="submitting()">
          {{ submitting() ? 'Signing in…' : 'Sign in' }}
        </button>
      </form>

      <p>New here? <a routerLink="/join">Join CircleShift</a></p>
    </section>
  `,
})
export class SignIn {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);

  /** From the query string, set by the sign-in guard. */
  readonly returnUrl = input<string>();

  protected readonly submitting = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly attempt = signal(0);
  protected readonly message = fieldMessage;

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected async submit(): Promise<void> {
    this.formError.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.attempt.update((n) => n + 1);
      return;
    }

    this.submitting.set(true);
    try {
      const { email, password } = this.form.getRawValue();
      await this.session.signIn(email, password);
      await this.router.navigateByUrl(safeReturnUrl(this.returnUrl()));
    } catch (error) {
      // The API gives one answer for unknown email, wrong password and lockout.
      this.formError.set(toApiError(error).message);
      this.form.controls.password.reset();
    } finally {
      this.submitting.set(false);
    }
  }
}
