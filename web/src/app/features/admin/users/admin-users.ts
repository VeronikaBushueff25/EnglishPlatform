import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, resource, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideChevronLeft, lucideChevronRight, lucideSearch } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmBadgeImports } from '@spartan-ng/helm/badge';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmNativeSelect, HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { HlmSpinnerImports } from '@spartan-ng/helm/spinner';
import { Api } from '../../../core/api/api';
import { adminListUsers, adminSetUserRole } from '../../../core/api/functions';
import { AdminUserDto } from '../../../core/api/models/admin-user-dto';
import { ADMIN_ROLE, AuthService } from '../../../core/auth/auth.service';
import { problemMessage } from '../../../core/http/problem';
import { ErrorAlert } from '../../../shared/error-alert';
import { AdminNav } from '../admin-nav';

const PAGE_SIZE = 20;
const SEARCH_DEBOUNCE_MS = 300;

export const ROLE_LABELS: Record<string, string> = {
  user: 'Пользователь',
  admin: 'Администратор',
};

@Component({
  selector: 'app-admin-users',
  imports: [
    DatePipe,
    AdminNav,
    NgIcon,
    ErrorAlert,
    HlmBadgeImports,
    HlmButtonImports,
    HlmCardImports,
    HlmInputImports,
    HlmNativeSelectImports,
    HlmSpinnerImports,
  ],
  providers: [provideIcons({ lucideChevronLeft, lucideChevronRight, lucideSearch })],
  templateUrl: './admin-users.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminUsers {
  private readonly api = inject(Api);
  private readonly currentUserId = inject(AuthService).user()?.id;

  protected readonly roles = Object.keys(ROLE_LABELS);
  protected readonly roleLabels = ROLE_LABELS;

  /** Текст в поле поиска; в запрос уходит с задержкой, чтобы не дёргать API на каждую букву. */
  protected readonly searchText = signal('');
  private readonly search = signal('');
  protected readonly page = signal(1);

  /** Список перезагружается сам при смене поиска или страницы; устаревшие ответы resource отбрасывает. */
  protected readonly users = resource({
    params: () => ({ search: this.search(), page: this.page() }),
    loader: ({ params }) =>
      this.api.invoke(adminListUsers, { search: params.search || undefined, page: params.page, pageSize: PAGE_SIZE }),
  });

  protected readonly items = computed(() => (this.users.hasValue() ? this.users.value().items : []));
  protected readonly totalCount = computed(() => (this.users.hasValue() ? Number(this.users.value().totalCount) : 0));
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / PAGE_SIZE)));
  protected readonly loadError = computed(() =>
    this.users.error() ? problemMessage(this.users.error(), 'Не удалось загрузить пользователей.') : null,
  );

  /** id пользователя, чья роль сейчас сохраняется. */
  protected readonly savingId = signal<string | null>(null);

  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.searchTimer));
  }

  protected onSearch(value: string): void {
    this.searchText.set(value);
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.page.set(1);
      this.search.set(value.trim());
    }, SEARCH_DEBOUNCE_MS);
  }

  protected primaryRole(user: AdminUserDto): string {
    return user.roles.includes(ADMIN_ROLE) ? ADMIN_ROLE : 'user';
  }

  protected isSelf(user: AdminUserDto): boolean {
    return user.id === this.currentUserId;
  }

  protected async changeRole(user: AdminUserDto, role: string, select: HlmNativeSelect): Promise<void> {
    const previous = this.primaryRole(user);
    if (role === previous) {
      return;
    }

    this.savingId.set(user.id);
    try {
      await this.api.invoke(adminSetUserRole, { id: user.id, body: { role } });
      this.users.update((page) =>
        page ? { ...page, items: page.items.map((u) => (u.id === user.id ? { ...u, roles: [role] } : u)) } : page,
      );
      toast.success(`${user.email}: ${ROLE_LABELS[role].toLowerCase()}`);
    } catch (e) {
      select.value.set(previous);
      toast.error(problemMessage(e, 'Не удалось изменить роль.'));
    } finally {
      this.savingId.set(null);
    }
  }
}
