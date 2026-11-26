import type { AbstractControl, FormGroup } from '@angular/forms';

import type { ApiError } from '../api/api-error';
import { fieldForCode } from '../profile/profile-rules';

/** The message to show under a field, or `null` while it is untouched or valid. */
export function fieldMessage(control: AbstractControl, label: string): string | null {
  const errors = control.errors;
  if (!errors || !(control.touched || control.dirty)) {
    return null;
  }
  if (typeof errors['server'] === 'string') return errors['server'];
  if (errors['required']) return `${label} is required.`;
  if (errors['requiredTrue']) return `${label} is required.`;
  if (errors['email']) return 'Enter a valid email address.';
  if (errors['minlength']) return `${label} needs at least ${errors['minlength'].requiredLength} characters.`;
  if (errors['maxlength']) return `${label} can be up to ${errors['maxlength'].requiredLength} characters.`;
  if (errors['trimmedLength']) {
    return `${label} must be ${errors['trimmedLength'].min}–${errors['trimmedLength'].max} characters.`;
  }
  if (errors['controlCharacters']) return `${label} contains characters that are not allowed.`;
  if (errors['languageTag']) {
    return `Use language tags such as en, es or pt-BR (not: ${errors['languageTag'].invalid.join(', ')}).`;
  }
  if (errors['tooManyLanguages']) return `List at most ${errors['tooManyLanguages'].max} languages.`;
  if (errors['timeZone']) return 'Choose a time zone from the list, such as Europe/Madrid.';
  return `${label} is not valid.`;
}

/**
 * Puts an API validation error on the matching field and returns `true`, or
 * returns `false` when the error belongs to no field (show it as a form error).
 */
export function applyServerError(form: FormGroup, error: ApiError): boolean {
  const field = fieldForCode(error.code);
  const control = field ? form.get(field) : null;
  if (!control) {
    return false;
  }
  control.setErrors({ ...(control.errors ?? {}), server: error.message });
  control.markAsTouched();
  return true;
}
