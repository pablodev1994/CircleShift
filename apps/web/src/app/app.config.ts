import { provideHttpClient, withFetch } from '@angular/common/http';
import type { ApplicationConfig } from '@angular/core';
import {
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';

import { routes } from './app.routes';
import { SessionService } from './core/auth/session.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZonelessChangeDetection(),
    provideRouter(routes, withComponentInputBinding()),
    // Same-origin `/api` calls (proxied to the API in development) get the
    // X-XSRF-TOKEN header from the XSRF-TOKEN cookie, Angular's defaults.
    provideHttpClient(withFetch()),
    // Know whether someone is signed in before the first navigation.
    provideAppInitializer(() => inject(SessionService).load()),
  ],
};
