import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';

export type ApiStatus = 'ok' | 'unavailable';

/** Проверка готовности API (/health/ready). Не входит в OpenAPI, поэтому вызывается напрямую. */
@Injectable({ providedIn: 'root' })
export class HealthService {
  private readonly http = inject(HttpClient);

  ready(): Observable<ApiStatus> {
    return this.http.get('/health/ready', { responseType: 'text' }).pipe(
      map((): ApiStatus => 'ok'),
      catchError(() => of<ApiStatus>('unavailable')),
    );
  }
}
