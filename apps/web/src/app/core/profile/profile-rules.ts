import type { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * The profile rules the API enforces (services/api: Member, LanguageTags,
 * TimeZones), mirrored so people see problems before they submit. The API
 * stays the authority; its 422 codes are mapped back with {@link fieldForCode}.
 */
export const PROFILE_LIMITS = {
  displayNameMin: 2,
  displayNameMax: 80,
  bioMax: 500,
  areaLabelMax: 60,
  languagesMax: 8,
  passwordMin: 12,
  passwordMax: 128,
} as const;

const LANGUAGE_TAG = /^[A-Za-z]{2,3}(-[A-Za-z]{4})?(-([A-Za-z]{2}|[0-9]{3}))?$/;
// eslint-disable-next-line no-control-regex
const CONTROL = /[\u0000-\u001f\u007f-\u009f]/;
// eslint-disable-next-line no-control-regex
const CONTROL_EXCEPT_NEWLINES = /[\u0000-\u0009\u000b\u000c\u000e-\u001f\u007f-\u009f]/;

/** Splits "es, pt-BR en" into tags; blanks are dropped. */
export function parseLanguages(text: string | null | undefined): string[] {
  return (text ?? '')
    .split(/[\s,]+/)
    .map((tag) => tag.trim())
    .filter((tag) => tag.length > 0);
}

/** Canonical case, like the API: `PT-br` → `pt-BR`, `ZH-hant` → `zh-Hant`. */
export function normalizeLanguageTag(tag: string): string {
  const [language = '', ...rest] = tag.split('-');
  return [
    language.toLowerCase(),
    ...rest.map((part) =>
      part.length === 4 ? part.charAt(0).toUpperCase() + part.slice(1).toLowerCase() : part.toUpperCase(),
    ),
  ].join('-');
}

export const languageList: ValidatorFn = (control: AbstractControl<string | null>): ValidationErrors | null => {
  const tags = parseLanguages(control.value);
  const invalid = tags.filter((tag) => tag.length > 16 || !LANGUAGE_TAG.test(tag));
  if (invalid.length > 0) {
    return { languageTag: { invalid } };
  }
  const unique = new Set(tags.map(normalizeLanguageTag));
  return unique.size > PROFILE_LIMITS.languagesMax ? { tooManyLanguages: { max: PROFILE_LIMITS.languagesMax } } : null;
};

/** Single-line text: no control characters at all. */
export const singleLine: ValidatorFn = (control: AbstractControl<string | null>): ValidationErrors | null =>
  CONTROL.test(control.value ?? '') ? { controlCharacters: true } : null;

/** Multi-line text: line breaks are fine, other control characters are not. */
export const multiLine: ValidatorFn = (control: AbstractControl<string | null>): ValidationErrors | null =>
  CONTROL_EXCEPT_NEWLINES.test(control.value ?? '') ? { controlCharacters: true } : null;

/** Counts trimmed length, as the API does, so "  a " is too short. */
export function trimmedLength(min: number, max: number): ValidatorFn {
  return (control: AbstractControl<string | null>): ValidationErrors | null => {
    const length = (control.value ?? '').trim().length;
    if (length === 0) {
      return null;
    }
    return length < min || length > max ? { trimmedLength: { min, max, actual: length } } : null;
  };
}

/** IANA zones the browser knows, plus UTC. */
export function timeZoneOptions(): readonly string[] {
  const intl = Intl as typeof Intl & { supportedValuesOf?: (key: 'timeZone') => string[] };
  const zones = intl.supportedValuesOf?.('timeZone') ?? [];
  return zones.includes('UTC') ? zones : [...zones, 'UTC'];
}

export function browserTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
}

export function ianaTimeZone(zones: readonly string[]): ValidatorFn {
  const known = new Set(zones);
  return (control: AbstractControl<string | null>): ValidationErrors | null => {
    const value = (control.value ?? '').trim();
    return value === '' || known.has(value) ? null : { timeZone: true };
  };
}

/** Which form field an API error code belongs to, so the message shows next to it. */
export function fieldForCode(code: string): string | null {
  if (code.startsWith('member.display_name')) return 'displayName';
  if (code === 'time_zone.invalid') return 'timeZoneId';
  if (code.startsWith('profile.bio')) return 'bio';
  if (code === 'profile.language_invalid' || code === 'profile.too_many_languages') return 'languages';
  if (code === 'profile.area_label_invalid') return 'areaLabel';
  if (code === 'profile.visibility_invalid') return 'visibility';
  if (code === 'account.email_invalid' || code === 'account.email_taken') return 'email';
  if (code === 'account.password_length') return 'password';
  if (code === 'member.adult_confirmation_required') return 'confirmedAdult';
  return null;
}
