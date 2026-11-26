import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, defer, from, of, switchMap, throwError } from 'rxjs';
import { AuthService, SKIP_AUTH } from './auth.service';

/**
 * Добавляет access-токен к запросам API. Если токен скоро истечёт, сначала обновляет его;
 * на 401 пробует обновить один раз и повторяет запрос, иначе отправляет на страницу входа.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/') || request.context.get(SKIP_AUTH)) {
    return next(request);
  }

  const auth = inject(AuthService);
  const withToken = (r: HttpRequest<unknown>) => {
    const token = auth.accessToken();
    return token ? r.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : r;
  };

  const ensureFresh = defer(() => (auth.accessTokenExpiresSoon() ? from(auth.refresh()) : of(true)));

  return ensureFresh.pipe(
    switchMap(() => next(withToken(request))),
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      return from(auth.refresh()).pipe(
        switchMap((refreshed) => {
          if (!refreshed) {
            auth.sessionExpired();
            return throwError(() => error);
          }
          return next(withToken(request));
        }),
      );
    }),
  );
};
