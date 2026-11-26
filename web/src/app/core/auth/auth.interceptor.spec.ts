import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthResponse } from '../api/models/auth-response';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

function session(token: string, expiresInMs = 15 * 60_000): AuthResponse {
  return {
    accessToken: token,
    accessTokenExpiresAt: new Date(Date.now() + expiresInMs).toISOString(),
    user: { id: 'u1', email: 'a@b.c', roles: ['user'] },
  };
}

/** HttpClient отдаёт управление асинхронно (Promise в AuthService), поэтому ждём микрозадачи. */
const flushMicrotasks = () => new Promise<void>((resolve) => setTimeout(resolve));

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => backend.verify());

  async function signIn(response: AuthResponse): Promise<void> {
    const done = auth.login({ email: 'a@b.c', password: 'secret123' });
    backend.expectOne('/api/v1/auth/login').flush(response);
    await done;
  }

  it('adds the bearer token to API requests', async () => {
    await signIn(session('token-1'));

    const result = firstValueFrom(http.get('/api/v1/me'));
    await flushMicrotasks();
    const request = backend.expectOne('/api/v1/me');
    expect(request.request.headers.get('Authorization')).toBe('Bearer token-1');
    request.flush({});
    await result;
  });

  it('does not add the token to auth endpoints or non-API URLs', async () => {
    await signIn(session('token-1'));

    const health = firstValueFrom(http.get('/health/ready', { responseType: 'text' }));
    await flushMicrotasks();
    const request = backend.expectOne('/health/ready');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush('Healthy');
    await health;
  });

  it('refreshes once on 401 and retries with the new token', async () => {
    await signIn(session('old'));

    const result = firstValueFrom(http.get<{ ok: boolean }>('/api/v1/me'));
    await flushMicrotasks();
    backend.expectOne('/api/v1/me').flush(null, { status: 401, statusText: 'Unauthorized' });

    await flushMicrotasks();
    backend.expectOne('/api/v1/auth/refresh').flush(session('new'));

    await flushMicrotasks();
    const retry = backend.expectOne('/api/v1/me');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer new');
    retry.flush({ ok: true });

    expect(await result).toEqual({ ok: true });
  });

  it('sends the user to login when refresh fails', async () => {
    await signIn(session('old'));
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    const result = firstValueFrom(http.get('/api/v1/me'));
    await flushMicrotasks();
    backend.expectOne('/api/v1/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await flushMicrotasks();
    backend.expectOne('/api/v1/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(result).rejects.toMatchObject({ status: 401 });
    expect(auth.isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/login'], expect.anything());
  });

  it('refreshes before the request when the token is about to expire', async () => {
    await signIn(session('expiring', 5_000));

    const result = firstValueFrom(http.get('/api/v1/me'));
    await flushMicrotasks();
    backend.expectOne('/api/v1/auth/refresh').flush(session('fresh'));

    await flushMicrotasks();
    const request = backend.expectOne('/api/v1/me');
    expect(request.request.headers.get('Authorization')).toBe('Bearer fresh');
    request.flush({});
    await result;
  });

  it('shares one refresh call between parallel requests', async () => {
    await signIn(session('old'));

    const first = firstValueFrom(http.get('/api/v1/me'));
    const second = firstValueFrom(http.get('/api/v1/admin/users'));
    await flushMicrotasks();
    backend.expectOne('/api/v1/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/v1/admin/users').flush(null, { status: 401, statusText: 'Unauthorized' });

    await flushMicrotasks();
    backend.expectOne('/api/v1/auth/refresh').flush(session('new'));

    await flushMicrotasks();
    backend.expectOne('/api/v1/me').flush({});
    backend.expectOne('/api/v1/admin/users').flush({});
    await Promise.all([first, second]);
  });
});
