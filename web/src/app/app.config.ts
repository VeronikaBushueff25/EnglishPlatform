import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';
import { I18n } from './core/i18n/i18n';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    provideRouter(routes, withComponentInputBinding()),
    // До первой навигации пробуем восстановить сессию по refresh-cookie, чтобы guard'ы видели вход.
    provideAppInitializer(() => inject(AuthService).restore()),
    // Язык интерфейса из прошлого визита — до первой отрисовки, чтобы не мигал русский.
    provideAppInitializer(() => inject(I18n).init()),
  ],
};
