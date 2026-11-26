import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { adminGuard, authGuard, guestGuard } from './auth.guards';
import { AuthService } from './auth.service';

const route = {} as ActivatedRouteSnapshot;
const state = (url: string) => ({ url }) as RouterStateSnapshot;

describe('auth guards', () => {
  let auth: AuthService;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    auth = TestBed.inject(AuthService);
    backend = TestBed.inject(HttpTestingController);
  });

  async function signInAs(roles: string[]): Promise<void> {
    const done = auth.login({ email: 'a@b.c', password: 'secret123' });
    backend.expectOne('/api/v1/auth/login').flush({
      accessToken: 't',
      accessTokenExpiresAt: new Date(Date.now() + 600_000).toISOString(),
      user: { id: 'u1', email: 'a@b.c', roles },
    });
    await done;
  }

  const run = (guard: typeof authGuard, url = '/x') => TestBed.runInInjectionContext(() => guard(route, state(url)));
  const serialize = (result: unknown) => TestBed.inject(Router).serializeUrl(result as UrlTree);

  it('authGuard sends a guest to login with returnUrl', () => {
    expect(serialize(run(authGuard, '/profile'))).toBe('/login?returnUrl=%2Fprofile');
  });

  it('authGuard lets a signed-in user through', async () => {
    await signInAs(['user']);
    expect(run(authGuard)).toBe(true);
  });

  it('guestGuard sends a signed-in user home', async () => {
    await signInAs(['user']);
    expect(serialize(run(guestGuard))).toBe('/');
  });

  it('adminGuard blocks a regular user and allows an admin', async () => {
    await signInAs(['user']);
    expect(serialize(run(adminGuard))).toBe('/');

    await signInAs(['admin']);
    expect(run(adminGuard)).toBe(true);
  });
});
