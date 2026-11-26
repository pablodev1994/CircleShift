import { FormControl } from '@angular/forms';

import {
  fieldForCode,
  ianaTimeZone,
  languageList,
  multiLine,
  normalizeLanguageTag,
  parseLanguages,
  singleLine,
  trimmedLength,
} from './profile-rules';

describe('profile rules', () => {
  it('splits languages on commas and spaces and drops blanks', () => {
    expect(parseLanguages(' es, pt-BR  en,,')).toEqual(['es', 'pt-BR', 'en']);
    expect(parseLanguages(null)).toEqual([]);
  });

  it('normalizes tag case like the API', () => {
    expect(normalizeLanguageTag('PT-br')).toBe('pt-BR');
    expect(normalizeLanguageTag('ZH-hant-tw')).toBe('zh-Hant-TW');
    expect(normalizeLanguageTag('es-419')).toBe('es-419');
  });

  it('rejects malformed tags and lists the bad ones', () => {
    expect(languageList(new FormControl('es, en'))).toBeNull();
    expect(languageList(new FormControl('english, es'))).toEqual({ languageTag: { invalid: ['english'] } });
  });

  it('allows eight distinct languages but not nine, counting case variants once', () => {
    const eight = 'en es fr de it pt nl sv';
    expect(languageList(new FormControl(`${eight} EN Es`))).toBeNull();
    expect(languageList(new FormControl(`${eight} da`))).toEqual({ tooManyLanguages: { max: 8 } });
  });

  it('measures trimmed length and ignores empty values (required handles those)', () => {
    const rule = trimmedLength(2, 80);
    expect(rule(new FormControl(''))).toBeNull();
    expect(rule(new FormControl('  a  '))).toEqual({ trimmedLength: { min: 2, max: 80, actual: 1 } });
    expect(rule(new FormControl('Ana'))).toBeNull();
  });

  it('allows line breaks only in multi-line text', () => {
    expect(multiLine(new FormControl('one\ntwo\r\nthree'))).toBeNull();
    expect(multiLine(new FormControl('bell\u0007'))).toEqual({ controlCharacters: true });
    expect(singleLine(new FormControl('one\ntwo'))).toEqual({ controlCharacters: true });
  });

  it('accepts only known time zones', () => {
    const rule = ianaTimeZone(['Europe/Madrid', 'UTC']);
    expect(rule(new FormControl('Europe/Madrid'))).toBeNull();
    expect(rule(new FormControl('GMT+2'))).toEqual({ timeZone: true });
  });

  it('maps API error codes to form fields', () => {
    expect(fieldForCode('member.display_name_length')).toBe('displayName');
    expect(fieldForCode('profile.too_many_languages')).toBe('languages');
    expect(fieldForCode('member.adult_confirmation_required')).toBe('confirmedAdult');
    expect(fieldForCode('auth.csrf_invalid')).toBeNull();
  });
});
