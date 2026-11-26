import { HttpContext, HttpContextToken } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Api } from '../api/api';
// Функции API импортируются по отдельности, а не из общего functions.ts: иначе в начальный бандл попадают все эндпоинты.
import { login } from '../api/fn/auth/login';
import { logout } from '../api/fn/auth/logout';
import { refresh } from '../api/fn/auth/refresh';
import { register } from '../api/fn/auth/register';
import { AuthResponse } from '../api/models/auth-response';
import { LoginRequest } from '../api/models/login-request';
import { RegisterRequest } from '../api/models/register-request';

/** Запрос не должен получать access-токен и не должен запускать refresh при 401 (сами запросы /auth/*). */
export const SKIP_AUTH = new HttpContextToken<boolean>(() => false);

const skipAuth = () => new HttpContext().set(SKIP_AUTH, true);

/** Обновляем access-токен заранее, если до истечения осталось меньше этого времени. */
const REFRESH_MARGIN_MS = 30_000;

export const ADMIN_ROLE = 'admin';

/**
 * Сессия пользователя. Access-токен хранится только в памяти; refresh-токен лежит в httpOnly cookie,
 * поэтому после перезагрузки страницы сессия восстанавливается вызовом /auth/refresh.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(Api);
  private readonly router = inject(Router);

  private readonly session = signal<AuthResponse | null>(null);
  private refreshInFlight: Promise<boolean> | null = null;

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly isAdmin = computed(() => this.user()?.roles.includes(ADMIN_ROLE) ?? false);

  accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  /** Токен есть, но скоро истечёт: перед запросом стоит обновить его. */
  accessTokenExpiresSoon(): boolean {
    const current = this.session();
    return current !== null && Date.parse(current.accessTokenExpiresAt) - Date.now() < REFRESH_MARGIN_MS;
  }

  async login(body: LoginRequest): Promise<void> {
    this.session.set(await this.api.invoke(login, { body }, skipAuth()));
  }

  async register(body: RegisterRequest): Promise<void> {
    this.session.set(await this.api.invoke(register, { body }, skipAuth()));
  }

  /** Обновляет access-токен по cookie. Одновременные вызовы ждут один и тот же запрос. */
  refresh(): Promise<boolean> {
    this.refreshInFlight ??= this.api
      .invoke(refresh, undefined, skipAuth())
      .then((response) => {
        this.session.set(response);
        return true;
      })
      .catch(() => {
        this.session.set(null);
        return false;
      })
      .finally(() => (this.refreshInFlight = null));
    return this.refreshInFlight;
  }

  /** Вызывается при старте приложения: если cookie есть, пользователь остаётся вошедшим. */
  async restore(): Promise<void> {
    await this.refresh();
  }

  async logout(): Promise<void> {
    try {
      await this.api.invoke(logout, undefined, skipAuth());
    } catch {
      // Даже если сервер недоступен, локально сессию завершаем.
    }
    this.session.set(null);
    await this.router.navigateByUrl('/login');
  }

  /** Сессия истекла (refresh не удался): отправляем на вход и возвращаем обратно после него. */
  sessionExpired(): void {
    this.session.set(null);
    const returnUrl = this.router.url;
    void this.router.navigate(['/login'], { queryParams: returnUrl.startsWith('/login') ? {} : { returnUrl } });
  }
}
