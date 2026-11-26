import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Только для вошедших; остальных отправляет на вход с возвратом на исходную страницу. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated() || router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** Страницы входа и регистрации: вошедшему пользователю они не нужны. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return !auth.isAuthenticated() || router.createUrlTree(['/']);
};

export const adminGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isAdmin()) {
    return true;
  }
  return auth.isAuthenticated()
    ? router.createUrlTree(['/'])
    : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
