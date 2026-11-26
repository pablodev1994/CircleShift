import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { toApiError } from '../../core/api/api-error';
import { SessionService } from '../../core/auth/session.service';
import { applyServerError, fieldMessage } from '../../core/forms/form-errors';
import type { BlockedMember, OwnProfile, Visibility } from '../../core/profile/profile.api';
import { ProfileApi } from '../../core/profile/profile.api';
import {
  PROFILE_LIMITS,
  ianaTimeZone,
  languageList,
  multiLine,
  normalizeLanguageTag,
  parseLanguages,
  singleLine,
  timeZoneOptions,
  trimmedLength,
} from '../../core/profile/profile-rules';

interface VisibilityOption {
  readonly value: Visibility;
  readonly label: string;
  readonly detail: string;
}

/** Onboarding step 2 and the place to edit your profile later, plus your block list. */
@Component({
  selector: 'cs-profile-editor',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './profile-editor.html',
})
export class ProfileEditor {
  private readonly api = inject(ProfileApi);
  private readonly session = inject(SessionService);

  /** `?welcome=1` right after joining. */
  readonly welcome = input<string>();

  protected readonly limits = PROFILE_LIMITS;
  protected readonly zones = timeZoneOptions();
  protected readonly visibilityOptions: readonly VisibilityOption[] = [
    { value: 'hidden', label: 'Hidden', detail: 'Only you. You will not appear in the directory or in matches.' },
    { value: 'members', label: 'Members', detail: 'Signed-in members, except anyone you block or who blocks you.' },
    { value: 'public', label: 'Public', detail: 'Anyone, including visitors who are not signed in.' },
  ];

  protected readonly profile = signal<OwnProfile | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly saved = signal<string | null>(null);
  protected readonly formError = signal<string | null>(null);
  protected readonly attempt = signal(0);
  protected readonly blocks = signal<readonly BlockedMember[]>([]);
  protected readonly blockError = signal<string | null>(null);
  protected readonly isWelcome = computed(() => this.welcome() === '1');
  protected readonly message = fieldMessage;

  protected readonly form = inject(NonNullableFormBuilder).group({
    displayName: [
      '',
      [Validators.required, trimmedLength(PROFILE_LIMITS.displayNameMin, PROFILE_LIMITS.displayNameMax), singleLine],
    ],
    timeZoneId: ['', [Validators.required, ianaTimeZone(this.zones)]],
    bio: ['', [Validators.maxLength(PROFILE_LIMITS.bioMax), multiLine]],
    languages: ['', languageList],
    areaLabel: ['', [Validators.maxLength(PROFILE_LIMITS.areaLabelMax), singleLine]],
    visibility: ['hidden' as Visibility, Validators.required],
  });

  /** Mirrors the bio length for the live counter. */
  protected readonly bioLength = signal(0);

  constructor() {
    this.form.controls.bio.valueChanges.subscribe((value) => this.bioLength.set(value.length));
    void this.load();
  }

  protected async save(): Promise<void> {
    this.saved.set(null);
    this.formError.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.attempt.update((n) => n + 1);
      this.focusFirstInvalid();
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    try {
      const profile = await this.api.updateOwn({
        displayName: value.displayName.trim(),
        timeZoneId: value.timeZoneId.trim(),
        bio: value.bio.trim() || null,
        languages: parseLanguages(value.languages).map(normalizeLanguageTag),
        areaLabel: value.areaLabel.trim() || null,
        visibility: value.visibility,
      });
      this.fill(profile);
      this.saved.set(`Profile saved. Visibility: ${this.labelFor(profile.visibility)}.`);
      // The header shows the display name from the session.
      void this.session.refresh();
    } catch (error) {
      const apiError = toApiError(error);
      if (!applyServerError(this.form, apiError)) {
        this.formError.set(apiError.message);
      }
      this.attempt.update((n) => n + 1);
      this.focusFirstInvalid();
    } finally {
      this.saving.set(false);
    }
  }

  protected async unblock(member: BlockedMember): Promise<void> {
    this.blockError.set(null);
    try {
      await this.api.unblock(member.memberId);
      this.blocks.update((list) => list.filter((entry) => entry.memberId !== member.memberId));
    } catch (error) {
      this.blockError.set(toApiError(error).message);
    }
  }

  private async load(): Promise<void> {
    try {
      const [profile, blocks] = await Promise.all([this.api.getOwn(), this.api.listBlocks()]);
      this.fill(profile);
      this.blocks.set(blocks);
    } catch (error) {
      this.loadError.set(toApiError(error).message);
    }
  }

  private fill(profile: OwnProfile): void {
    this.form.reset({
      displayName: profile.displayName,
      timeZoneId: profile.timeZoneId,
      bio: profile.bio ?? '',
      languages: profile.languages.join(', '),
      areaLabel: profile.areaLabel ?? '',
      visibility: profile.visibility,
    });
    this.profile.set(profile);
  }

  private labelFor(visibility: Visibility): string {
    return this.visibilityOptions.find((option) => option.value === visibility)?.label ?? visibility;
  }

  private focusFirstInvalid(): void {
    queueMicrotask(() => document.querySelector<HTMLElement>('cs-profile-editor form .ng-invalid:not(form):not(fieldset)')?.focus());
  }
}
